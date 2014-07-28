using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Threading;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Linq;
using System.Reflection;
using System.IO;
using Microsoft.Research.AuDotNet;

namespace Sean
{
    public enum PSHelperState
    {
        Invalid,
        Idle,
        Executing,
        Stopping
    };

    /// <summary>
    /// Handle interaction with the running powershell.
    /// </summary>
    public class PowerShellHelper
    {
        /// <summary>
        /// Holds a reference to the runspace for this interpeter.
        /// </summary>
        internal Runspace myRunSpace;

        /// <summary>
        /// Indicator to tell the host application that it should exit.
        /// </summary>
        private bool shouldExit;

        /// <summary>
        /// The exit code that the host application will use to exit.
        /// </summary>
        private int exitCode;

        /// <summary>
        /// Holds a reference to the PSHost implementation for this interpreter.
        /// </summary>
        private MyPSHost myHost;

        /// <summary>
        /// Holds a reference to the PSHostUserInterface implementation for this interpreter.
        /// </summary>
        private PSHostUserInterface myUI;

        public PSHostUserInterface UI { get { return myUI; } }

        /// <summary>
        /// Holds a reference to the currently executing pipeline so that it can be
        /// stopped by the control-C handler.
        /// </summary>
        private PowerShell mycurrentPowerShell;
        private PowerShell CurrentPowerShell
        {
            get
            {
                Debug.Assert(() => mycurrentPowerShell != null);
                return mycurrentPowerShell;
            }
        }

        /// <summary>
        /// Used to serialize access to instance data.
        /// </summary>
        private object instanceLock = new object();

        /// <summary>
        /// Allows clients to query current state
        /// </summary>
        public PSHelperState State { get { return state; } }
        PSHelperState state = PSHelperState.Invalid;

        /// <summary>
        /// Gets or sets a value indicating whether the host application 
        /// should exit.
        /// </summary>
        public bool ShouldExit
        {
            get { return this.shouldExit; }
            set { this.shouldExit = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the host application 
        /// should exit.
        /// </summary>
        public int ExitCode
        {
            get { return this.exitCode; }
            set { this.exitCode = value; }
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public PowerShellHelper(PSHostUserInterface ui)
        {
            // Create the host and runspace instances for this interpreter. 
            // Note that this application does not support console files so 
            // only the default snap-ins will be available.
            this.myUI = ui;
            this.myHost = new MyPSHost(this);
            this.myRunSpace = RunspaceFactory.CreateRunspace(this.myHost);
            this.myRunSpace.ApartmentState = ApartmentState.MTA; // Need this to fix bug#1
            this.myRunSpace.ThreadOptions = PSThreadOptions.UseCurrentThread;
            this.myRunSpace.Open();
            this.mycurrentPowerShell = PowerShell.Create();
            this.mycurrentPowerShell.Runspace = myRunSpace;
            this.state = PSHelperState.Idle;
        }

        ~PowerShellHelper()
        {
            this.mycurrentPowerShell.Dispose();
        }

        public void InitProfile(bool load_user_profile)
        {
            // Create a PowerShell object to run the commands used to create 
            // $profile and load the profiles.
            state = PSHelperState.Executing;

            try
            {
                {
                    CurrentPowerShell.Commands.Clear();
                    CurrentPowerShell.Commands.AddScript("set-executionpolicy -force -scope currentUser RemoteSigned");
                    CurrentPowerShell.Invoke();
                }

                if (load_user_profile)
                {
                    PSCommand[] profileCommands = PSUtils.GetProfileCommands("Sean", false);
                    foreach (PSCommand command in profileCommands)
                    {
                        MainWindow.StaticDebugWrite("Profile command: " + PSUtils.Print(command) + "\n");
                        CurrentPowerShell.Commands = command;
                        try
                        {
                            CurrentPowerShell.Invoke();
                        }
                        catch (PSSecurityException)
                        {
                            CurrentPowerShell.Commands.Clear();
                            CurrentPowerShell.Commands.AddScript("write-error 'Script disabled. use set-executionpolicy remotesigned'");
                            CurrentPowerShell.Invoke();
                        }
                    }
                }

                // Add exe directory to end of path to pick up sean-get-completions etc
                string exe_dir = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
                {
                    string sean_scripts_dir = exe_dir + "Scripts";
                    CurrentPowerShell.Commands.Clear();
                    CurrentPowerShell.Commands.AddScript("$Env:PATH += ';" + sean_scripts_dir + "'");
                    CurrentPowerShell.Invoke();
                }

                // At debug time, we also want to add the source scripts dir,
                // so that editing them in Visual Studio directly affects the running
                // shell.
                //
                // They go at the start of the path, as we want to ensure they take 
                // precedence over a user's customized ones
                //
                // FIXME: Horrible hack to find if we're debugginh is that we strip elements 
                // from exe_dir until we run out, or find one containing "Sean.csproj"
                string dir = exe_dir;
                while (!String.IsNullOrEmpty(dir))
                {
                    string seanproj = Path.Combine(dir, "Sean", "Sean.csproj");
                    if (File.Exists(seanproj))
                    {
                        string sean_scripts_dir = Path.Combine(dir, "Sean", "Scripts");

                        MainWindow.StaticDebugWrite("Found sean project [" + seanproj + "], Adding [" + sean_scripts_dir + "] to path\n");

                        CurrentPowerShell.Commands.Clear();
                        CurrentPowerShell.Commands.AddScript(
                            "$Env:PATH = '" + sean_scripts_dir + ";' + $Env:PATH;" +
                            "write-host 'AppBase=[" + exe_dir + "]'");
                        CurrentPowerShell.Invoke();

                        break;
                    }

                    dir = Path.GetDirectoryName(dir);
                }




            }
            finally
            {
                state = PSHelperState.Idle;
            }
        }

        /// <summary>
        /// Delegate to be called within awf_execute
        /// </summary>
        /// <param name="ps">PS object to which commands should be added</param>
        public delegate void AddCommandsDelegate(PSCommand ps);

        /// <summary>
        /// A helper class that builds and executes a pipeline, and returns the results as a string
        /// Any exceptions that are thrown are 
        /// just passed to the caller.
        /// </summary>
        /// <param name="cmd">The script to run.</param>
        /// <param name="input">Any input arguments to pass to the script. 
        /// If null then nothing is passed in.</param>
        public Collection<PSObject> Execute(AddCommandsDelegate add_commands, object input = null, bool add_to_history = false)
        {
            try
            {
                state = PSHelperState.Executing;

                // Add a script and command to the pipeline and then run the pipeline. Place 
                // the results in the currentPowerShell variable so that the pipeline can be 
                // stopped.
                //Debug.Assert(() => CurrentPowerShell.Runspace == this.myRunSpace);

                PSInvocationSettings pis = new PSInvocationSettings();
                pis.ApartmentState = ApartmentState.MTA;
                pis.AddToHistory = add_to_history;
                CurrentPowerShell.Commands.Clear();
                add_commands(CurrentPowerShell.Commands);

                // If there is any input pass it in, otherwise just invoke 
                // the pipeline.
                PSDataCollection<object> inputs = null;
                if (input != null)
                {
                    inputs = new PSDataCollection<object>();
                    inputs.Add(input);
                }

                Collection<PSObject> psos = new Collection<PSObject>();
                CurrentPowerShell.Invoke(inputs, psos, pis);

                foreach (ErrorRecord er in CurrentPowerShell.Streams.Error)
                    ReportException(er);
                CurrentPowerShell.Streams.Error.Clear();

                return psos;
            }
            catch (Exception rte)
            {
                try
                {
                    state = PSHelperState.Executing;
                    this.ReportException(rte);

                    return null;
                }
                finally
                {
                    state = PSHelperState.Idle;
                }
            }
            finally
            {
                state = PSHelperState.Idle;
            }

        }

        /// <summary>
        /// To display an exception using the display formatter, 
        /// run a second pipeline passing in the error record.
        /// The runtime will bind this to the $input variable,
        /// which is why $input is being piped to the Out-String
        /// cmdlet. The WriteErrorLine method is called to make sure 
        /// the error gets displayed in the correct error color.
        /// </summary>
        /// <param name="e">The exception to display.</param>
        private void ReportException(Exception e)
        {
            if (e != null)
            {
                ErrorRecord error;
                IContainsErrorRecord icer = e as IContainsErrorRecord;
                if (icer != null)
                {
                    error = icer.ErrorRecord;
                }
                else
                {
                    error = new ErrorRecord(e, "Host.ReportException", ErrorCategory.NotSpecified, null);
                }
                ReportException(error);
            }
        }

        private void ReportException(ErrorRecord er)
        {
            CurrentPowerShell.Commands.Clear();
            CurrentPowerShell.AddScript("$input").AddCommand("out-string");

            // Do not merge errors, this function will swallow errors.
            PSDataCollection<object> inputCollection = new PSDataCollection<object>();
            inputCollection.Add(er);
            inputCollection.Complete();
            Collection<PSObject> result = CurrentPowerShell.Invoke(inputCollection);

            if (result.Count > 0)
            {
                string str = result[0].BaseObject as string;
                if (!string.IsNullOrEmpty(str))
                {
                    // Remove \r\n, which is added by the Out-String cmdlet.
                    this.myHost.UI.WriteErrorLine(str.Substring(0, str.Length - 2));
                }
            }
        }

        void KillProcessAndChildren(int pid)
        {
          using (var searcher = new System.Management.ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid))
          using (System.Management.ManagementObjectCollection moc = searcher.Get())
          {
            foreach (System.Management.ManagementObject mo in moc)
            {
              KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
          }
          try
          {
            MainWindow.StaticShriek("KILL[" + pid + "].\n");
            var proc = System.Diagnostics.Process.GetProcessById(pid);
            proc.Kill();
          }
          catch (ArgumentException)
          {
            MainWindow.StaticShriek("HMMM[" + pid + "].\n");
            /* process already exited */ 
          }
        }

        /// <summary>
        /// Method used to handle control-C's from the user. It calls the
        /// pipeline Stop() method to stop execution. If any exceptions occur
        /// they are printed to the console but otherwise ignored.
        /// </summary>
        public void CtrlC(Action callback)
        {
            try
            {
                MainWindow.StaticShriek("CTRL C [" + CurrentPowerShell.Commands.Commands[0].CommandText + "].\n");
                if (state == PSHelperState.Stopping)
                {
                    // Second strike...
                    MainWindow.StaticShriek("Second CTRLC.  Killing all subprocesses.\n");

                    System.Diagnostics.Process me = System.Diagnostics.Process.GetCurrentProcess();

                    using (var searcher = new System.Management.
                        ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + me.Id))
                    using (System.Management.ManagementObjectCollection moc = searcher.Get())
                    {
                        foreach (System.Management.ManagementObject mo in moc)
                        {
                          string procname = mo["Name"].ToString();
                          if (procname != "conhost.exe")
                          {
                            int id = Convert.ToInt32(mo["ProcessID"]);
                            MainWindow.StaticShriek("Killing [" + procname + "], id " + id);
                            KillProcessAndChildren(id);
                          }
                          else
                            MainWindow.StaticShriek("Not killing conhost.");
                        }
                    }
                    MainWindow.StaticShriek("\n");

                    this.mycurrentPowerShell.Dispose();
                    this.mycurrentPowerShell = PowerShell.Create();
                    this.mycurrentPowerShell.Runspace = myRunSpace;
                    this.state = PSHelperState.Idle;

                }
                else
                    MainWindow.StaticShriek("Hit CTRL-C again to forcibly kill all descendant processes.\n");

                state = PSHelperState.Stopping;
                CurrentPowerShell.BeginStop(new AsyncCallback((res) => this.EndStop(res, callback)), null);
            }
            catch (Exception exception)
            {
                this.myHost.UI.WriteErrorLine(exception.ToString());
                state = PSHelperState.Idle;
            }
        }

        public void EndStop(IAsyncResult res, Action callback)
        {
            state = PSHelperState.Idle;
            callback.Invoke();
        }

        /// <summary>
        /// Get a string representing the prompt.
        /// </summary>
        public string GetPrompt()
        {
            string prompt = Execute((PSCommand cmds) => cmds.AddCommand("prompt")).Single().ToString();
            if (this.myHost.IsRunspacePushed)
            {
                prompt = string.Format("[{0}] ", this.myRunSpace.ConnectionInfo.ComputerName) + prompt;
            }
            return prompt;
        }

        public PSObject[] GetHistory()
        {
            Collection<PSObject> psos = Execute((PSCommand cmds) => cmds.AddScript("get-history -count 10000"));
            return psos.ToArray();
        }
    }

    public class PSUtils
    {
        internal static string GetApplicationBase(string shellId)
        {
            string name = @"Software\Microsoft\PowerShell\" + /*PSVersionInfo.RegistryVersionKey*/ "1" + @"\PowerShellEngine";
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(name))
            {
                if (key != null)
                {
                    return (key.GetValue("ApplicationBase") as string);
                }
            }
            Assembly entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                return Path.GetDirectoryName(entryAssembly.Location);
            }
            entryAssembly = Assembly.GetAssembly(typeof(PSObject));
            if (entryAssembly != null)
            {
                return Path.GetDirectoryName(entryAssembly.Location);
            }
            return "";
        }





        private static string GetAllUsersFolderPath(string shellId)
        {
            string applicationBase = string.Empty;
            try
            {
                applicationBase = GetApplicationBase(shellId);
            }
            catch (Exception exception)
            {
                throw exception;
            }
            return applicationBase;

        }

        internal static string GetFullProfileFileName(string shellId, bool forCurrentUser, bool useTestProfile)
        {
            string allUsersFolderPath = null;
            if (forCurrentUser)
            {
                allUsersFolderPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                        "WindowsPowerShell"/*awf*/);
            }
            else
            {
                allUsersFolderPath = GetAllUsersFolderPath(shellId);
                if (string.IsNullOrEmpty(allUsersFolderPath))
                {
                    System.Console.WriteLine("could not locate all users folder", new object[0]);
                    return "";
                }
            }
            string str2 = useTestProfile ? "profile_test.ps1" : "profile.ps1";
            if (!string.IsNullOrEmpty(shellId))
            {
                str2 = shellId + "_" + str2;
            }
            return (allUsersFolderPath = Path.Combine(allUsersFolderPath, str2));

        }

        internal static PSObject GetDollarProfile(string allUsersAllHosts, string allUsersCurrentHost, string currentUserAllHosts, string currentUserCurrentHost)
        {
            PSObject obj2 = new PSObject(currentUserCurrentHost);
            obj2.Properties.Add(new PSNoteProperty("AllUsersAllHosts", allUsersAllHosts));
            obj2.Properties.Add(new PSNoteProperty("AllUsersCurrentHost", allUsersCurrentHost));
            obj2.Properties.Add(new PSNoteProperty("CurrentUserAllHosts", currentUserAllHosts));
            obj2.Properties.Add(new PSNoteProperty("CurrentUserCurrentHost", currentUserCurrentHost));
            return obj2;
        }



        internal static PSCommand[] GetProfileCommands(string shellId, bool useTestProfile)
        {
            List<PSCommand> list = new List<PSCommand>();
            string allUsersAllHosts = GetFullProfileFileName(null, false, useTestProfile);
            string allUsersCurrentHost = GetFullProfileFileName(shellId, false, useTestProfile);
            string currentUserAllHosts = GetFullProfileFileName(null, true, useTestProfile);
            string currentUserCurrentHost = GetFullProfileFileName(shellId, true, useTestProfile);
            PSObject obj2 = GetDollarProfile(allUsersAllHosts, allUsersCurrentHost, currentUserAllHosts, currentUserCurrentHost);
            PSCommand item = new PSCommand();
            item.AddCommand("set-variable");
            item.AddParameter("Name", "profile");
            item.AddParameter("Value", obj2);
            item.AddParameter("Option", ScopedItemOptions.None);
            list.Add(item);
            string[] strArray = new string[] { allUsersAllHosts, allUsersCurrentHost, currentUserAllHosts, currentUserCurrentHost };
            foreach (string str5 in strArray)
            {
                if (File.Exists(str5))
                {
                    item = new PSCommand();
                    item.AddCommand(str5, false);
                    list.Add(item);
                }
            }
            return list.ToArray();
        }

        public static string Print(PSCommand command)
        {
            return command.Commands.Aggregate("",
                (s, x) => s + x.CommandText + " " +
                    x.Parameters.Aggregate("", (s1, p) => s1 + " -" + p.Name + " '" + p.Value + "'") + "; ");
        }

    }
}

// Don't know if atexit is a good plan...
//foreach(PSObject pso in awf_execute("atexit")) {
//    Console.WriteLine(pso.ToString());
//    Console.ReadKey();
//}

// Exit with the desired exit code that was set by the exit command.
// The exit code is set in the host by the MyHost.SetShouldExit() method.
//Environment.Exit(this.ExitCode);