using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using System.Windows.Media;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Collections.ObjectModel;

namespace Sure
{
    class PSDispatcher : DispatcherObject
    {
    };

    class PowerShellThread
    {
        public PSDispatcher dispobj;
        public PowerShellHelper psh;
        public Thread thread;
        MainWindow mainwin;
        MyPSHostUserInterface ui;
        static bool load_user_profile = true;
        static List<string> init_commands = new List<string>();

        public Dispatcher Dispatcher { 
            get { return dispobj.Dispatcher; } 
        }

        static public PowerShellThread Init(MainWindow mainwin, string[] commandline_args)
        {
            int i = 0; 
            int n = commandline_args.Length;
            while (i < n)
            {
                string s = commandline_args[i];

                if (s == "-noprofile")
                {
                    load_user_profile = false;
                }
                else if (s == "-command")
                {
                    if (++i >= n)
                    {
                        MainWindow.StaticDebugWrite("CommandLine: [-command] as last arg -- it needs a command...");
                        break;
                    }
                    init_commands.Add(commandline_args[i]);
                }

                ++i;
            }
            
            PowerShellThread pst = new PowerShellThread(mainwin);
            
            pst.thread = new Thread(() => pst.Start());
            pst.thread.Name = "PowerShellDispatcher Thread";
            pst.thread.SetApartmentState(ApartmentState.MTA);
            pst.thread.Priority = ThreadPriority.BelowNormal;
            pst.thread.Start();

            return pst;
        }

        PowerShellThread(MainWindow mainwin)
        {
            this.mainwin = mainwin;
            this.ui = null;
            this.psh = null;
        }

        public void Start()
        {
            // New thread starts here
            dispobj = new PSDispatcher();
            
            ui = new MyPSHostUserInterface(mainwin);
            psh = new PowerShellHelper(ui);
            psh.InitProfile(load_user_profile);
            foreach (string cmd in init_commands)
            {
                MainWindow.StaticDebugWrite("Running init command [" + cmd + "]");
                psh.Execute((PSCommand ps) => ps.AddScript(cmd));
            }

            // Tell MainWin we're not busy
            mainwin.Dispatcher.BeginInvoke(new Action(() => mainwin.SetReadyForInput()), DispatcherPriority.ApplicationIdle);

            // Wait for invokes....
            Dispatcher.Run();
        }

        public PSHelperState State
        {
            get { if (psh != null) return psh.State; else return PSHelperState.Invalid; }
        }

        public void GenerateLines()
        {
            string[] words = new string[]{
                ".",
                "the", 
                "ploughman", 
                "homeward", 
                "plods",
                "his",
                "weary", 
                "way"
            };
            string myline = "";
            System.Random rng = new System.Random();
            int count = 0;
            while (true) {
                int w;
                while (true)
                {
                    w = rng.Next(words.Length);
                    if (w != 0)
                        break;
                    if (myline.Length > 30)
                        break;
                    if (rng.NextDouble() > .7)
                        break;
                }
                
                myline += words[w] + " ";
                if (w == 0) {
                    ui.WriteLine(myline);
                    myline = "";
                    ++count;
                    if (count % 100 == 0)
                        return;
                }

            }
        }

        public void Execute(PowerShellHelper.AddCommandsDelegate add_commands, object input = null, bool add_to_history = false)
        {
            dispobj.VerifyAccess();
            // We'll block the psthread here until done.
            psh.Execute(add_commands, input, add_to_history);
        }


        public void ExecuteAndPostResult(string command, Action callback, DispatcherObject callback_dispatcher)
        {
            dispobj.VerifyAccess();
            // We'll block the psthread here until done.
            PowerShellHelper.AddCommandsDelegate addcmd = (PSCommand cmds) =>
            {
                cmds.AddScript(command);
                cmds.AddCommand("out-default");
                cmds.Commands[0].MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);
            };
            psh.Execute(addcmd, null, true);
            callback_dispatcher.Dispatcher.BeginInvoke(callback);
        }



        /// <summary>
        /// Delegate type for async callbacks
        /// </summary>
        /// <param name="psos"></param>
        public delegate void ExecuteCallback(Collection<PSObject> psos);


        public void ExecuteAsync(PowerShellHelper.AddCommandsDelegate add_commands, 
                                 ExecuteCallback onDone, object input = null, bool add_to_history = false)
        {
            if (dispobj.CheckAccess())
            {
                // Not async within the PS thread
                Collection<PSObject> psos = psh.Execute(add_commands, input, add_to_history);

                // And now callback on the UI thread
                mainwin.Dispatcher.Invoke(new Action(() =>
                {
                    System.Diagnostics.Debug.WriteLine("returning from async");
                    onDone(psos);
                }));
            }
            else
            {
                DispatcherOperation op = dispobj.Dispatcher.BeginInvoke(
                    new Action(() => ExecuteAsync(add_commands, onDone, input, add_to_history)));


                //DispatcherOperationStatus status = op.Status;
                //while (status != DispatcherOperationStatus.Completed)
                //{
                //    status = op.Wait(TimeSpan.FromMilliseconds(1000));
                //    if (status == DispatcherOperationStatus.Aborted)
                //    {
                //        // Alert Someone
                //    }
                //}
            }
        }

        public void CtrlC()
        {
            // Will be called on the UI thread, and will bring down the running command
            psh.CtrlC(CtrlC_Done);
        }

        void CtrlC_Done()
        {
            mainwin.Dispatcher.BeginInvoke((Action)(() => mainwin.CtrlC_Done()), DispatcherPriority.Send);
        }

        public string GetPrompt()
        {
            if (psh.State == PSHelperState.Idle) // xx no lock on the setting of this state, so will sometimes hang waiting for command to complete before returning prompt.   Is "state" useful at all?
                return dispobj.CheckAccess() ? 
                    psh.GetPrompt() : 
                    (string)Dispatcher.Invoke(new Func<string>(() => psh.GetPrompt()), DispatcherPriority.Normal);
            else
                return "[BUSY]>";
        }

        public PSObject[] GetHistory()
        {
            // xx no lock on the setting of this state, so will sometimes hang waiting for command to complete before returning history.   Is "state" useful at all?
            if (this == null || psh == null || psh.State != PSHelperState.Idle)
                return null;

            return dispobj.CheckAccess() ? psh.GetHistory() :
                (PSObject[])Dispatcher.Invoke(new Func<PSObject[]>(() => psh.GetHistory()), DispatcherPriority.Normal);
        }


    }
}
