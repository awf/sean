using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using System.Management.Automation;

using Au = Microsoft.Research.AuDotNet;
using System.Windows.Media.Animation;
using System.ComponentModel;
using Sure.Properties;

namespace Sure
{
    /// <summary>
    /// These are the flags to indicate which control each command is bound to.
    /// The lowercase names are chosen to match the field names in MainWindow
    /// </summary>
    public enum ControlBinding
    {
        inputbox = 1,
        scroller = 2,
        window = 4
    };

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        PowerShellThread psthread;
        public int maxlines = 1024;
        TabCompletion tabCompletion;

        public MainWindow()
        {
            InitializeComponent();

            inputbox.Focus();

            var ctrlmap = new Dictionary<Control, ControlBinding>();
            ctrlmap.Add(inputbox, ControlBinding.inputbox);
            ctrlmap.Add(scroller, ControlBinding.scroller);
            ctrlmap.Add(this,     ControlBinding.window);
            SureCommandBindingAttribute.AddBindingsToControls(this, typeof(MainWindow), ctrlmap);
        }

        private void mainwin_Loaded(object sender, RoutedEventArgs e)
        {
            consoledoc.Blocks.Remove(designtimedata);
            completions.Children.Clear();
            completions_scroller.Visibility = System.Windows.Visibility.Collapsed;
            WriteLine();

            // Start up powershell
            psthread = PowerShellThread.Init(this, Environment.GetCommandLineArgs());
            tabCompletion = new TabCompletion(this, psthread);
            //grid.RowDefinitions[0].Height = Settings.Default.DebugPaneHeight;
        }

        private void mainwin_Closed(object sender, EventArgs e)
        {
            //Settings.Default.DebugPaneHeight = grid.RowDefinitions[0].ActualHeight;
            Settings.Default.Save(); 

            // xx. Do this more cleanly?
            Environment.Exit(0);
            // psthread.thread.Abort();
        }

        public bool PshIsBusy
        {
            get { return psthread == null || psthread.State != PSHelperState.Idle; }
        }

        public void SetReadyForInput()
        {
            FlushOutput();

            if (PshIsBusy)
                SetPromptBusy();
            else
            {
                prompt.Text = psthread.GetPrompt();
                Brush inputbox_bg = FindResource("inputbox_bg") as Brush;
                input.Background = inputbox_bg;
            }

            tabCompletion.Clear();
            ResetHistory();
        }

        public void SetPromptBusy()
        {
            prompt.Text = "[busy]>";
            tabCompletion.Clear(); 
        }

        /// <summary>
        /// Ring the bell
        /// </summary>
        public void Shriek(string msg)
        {
            DebugWrite(msg);
            Storyboard visualbell_storyboard = FindResource("visualbell_storyboard") as Storyboard;
            visualbell_storyboard.Begin();
            //input.BeginStoryboard(visualbell_storyboard, HandoffBehavior.Compose);
        }

        public static void StaticShriek(string msg)
        {
            (App.Current.MainWindow as MainWindow).Shriek(msg);
        }

        internal string GetInputBuffer()
        {
            return inputbox.Text;
        }

        internal int GetInputBufferPos()
        {
            return inputbox.SelectionStart;
        }

        internal void SetBuffer(string s)
        {
            inputbox.Text = s;
            ScrollToEnd();
        }

        #region General input and window-level command bindings
          /*
        private void inputbox_KeyDown(object sender, KeyEventArgs e)
        {
            //DebugWrite("[key " + e.Key + "]");
        }

        private void inputbox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //DebugWrite("[prev " + e.Key + "]");
        }
            */

        private void inputbox_GotFocus(object sender, RoutedEventArgs e)
        {
            ScrollToEnd();
        }

        [SureCommandBinding("Window_Tab", "Tab", ControlBinding.window)]
        public void WindowTab()
        {
            inputbox.Focus();
        }

        #endregion

        #region Command-line Enter
        [SureCommandBinding("CLI_Enter", "Return", ControlBinding.inputbox)]
        public void Enter()
        {
            if  (PshIsBusy)
            {
                Shriek("PS is busy\n");
                return;
            }

            string commandline = inputbox.Text;

            // Strip any trailing backticks from tab completion
            if (commandline.EndsWith("`") && !commandline.EndsWith("``"))
              commandline = commandline.TrimEnd(new char[] { '`' });

            // Post current line into console buffer
            Run run = input.PreviousBlock.ContentEnd.Paragraph.Inlines.LastInline as Run;
            if (run != null && run.Text.Length > 0)
                WriteLine();
            Write(prompt.Foreground, consoledoc.Background, prompt.Text + " " + commandline);
            WriteLine();

            // Clear the inputbox
            inputbox.Text = "";
            SetPromptBusy();

            // Special-case empty commands: don't add to the history, don't even run.
            if (commandline.Length > 0)
            {
                // Add to history
                AddToHistory(commandline);

                Action callback = new Action(() => this.EnterDone());
                Action begin_exec = new Action(() => psthread.ExecuteAndPostResult(commandline, callback, mainwin));
                psthread.Dispatcher.BeginInvoke(begin_exec, DispatcherPriority.ApplicationIdle);
            }
            else
                EnterDone();
        }

        void EnterDone()
        {
            if (psthread.psh.ShouldExit)
                App.Current.Shutdown();

            SetReadyForInput();
        }
        #endregion

        #region Control-C handling
        [SureCommandBinding("CLI_CtrlC", "CTRL+C", ControlBinding.inputbox)]
        public void CtrlC()
        {
            if (inputbox.SelectionLength == 0)
                psthread.CtrlC();
            else
                inputbox.Copy();
        }

        bool discarding = false;
        int ndiscarded = 0;
        public void CtrlC_Done()
        {
            // Clear our dispatcher of console writes..
            discarding = true;
            ndiscarded = 0;

            // And schedule the next step for after that's done
            // The low priority means it will happen after the writes.
            Dispatcher.BeginInvoke((Action)CtrlC_Done_Part2, DispatcherPriority.ApplicationIdle);
        }

        void CtrlC_Done_Part2()
        {
            discarding = false;
            DebugWrite("Discarded " + ndiscarded + " lines of output\n");
            SetReadyForInput();
        }
        #endregion
                                                                     
        #region History
        History history = new History();
        string prefix = null;
        string currentline = null;

        /// <summary>
        /// Set up history for a new line.
        /// </summary>
        private void ResetHistory()
        {
            history.Reset();
            prefix = null;
            currentline = null;
        }

        public void HistoryNavigate(bool forward)
        {
            if (!history.Loaded)
                history.Init(psthread);
            int current = inputbox.SelectionStart;
            string newprefix = inputbox.Text.Substring(0, current);
            if (newprefix != prefix)
            {
                currentline = inputbox.Text;
                prefix = newprefix;
            }
            string histline = forward ? history.Forward(prefix) : history.Back(prefix);
            SetBuffer(histline != null ? histline : currentline);
            
            
            if (current > inputbox.Text.Length)
                inputbox.SelectionStart = inputbox.Text.Length;
            else
                inputbox.SelectionStart = current;
        }

        private void AddToHistory(string s)
        {
            history.Add(s);
        }

        [SureCommandBinding("ForwardHistory", "Down", ControlBinding.inputbox)]
        public void ForwardHistory()
        {
            HistoryNavigate(true);
        }

        [SureCommandBinding("BackHistory", "Up", ControlBinding.inputbox)]
        public void BackHistory()
        {
            HistoryNavigate(false);
        }
        #endregion History

        #region Tab Completion
        [SureCommandBinding("CLI_Tab", "Tab", ControlBinding.inputbox)]
        public void InputboxTab()
        {
            if (PshIsBusy)
                Shriek("PS is busy\n");
            else
                tabCompletion.OnTab();
        }
        #endregion
       
        #region Console Write

        string runText = "";

        public void Write(Brush foreground, Brush background, string s)
        {
            if (discarding)
                return;

            if (psthread != null && psthread.State == PSHelperState.Stopping)
            {
                DebugWrite(s);
                return;
            }

            // Split into '\n's
            Run run = input.PreviousBlock.ContentEnd.Paragraph.Inlines.LastInline as Run;
            if (run == null || run.Foreground != foreground || run.Background != background)
            {
                run = new Run(s);
                input.PreviousBlock.ContentEnd.Paragraph.Inlines.Add(run);
                run.Foreground = foreground;
                run.Background = background;
                run.Text = s;
                runText = s;
            }
            else
            {
                runText += s;
            }
        }

        int nskip = 10;

        public void WriteLine()
        {
            if (discarding)
            {
                ++ndiscarded;
                return;
            }

            if (psthread != null && psthread.State == PSHelperState.Stopping)
            {
                DebugWrite("\n");
                return;
            }

            if (nskip++ < 10)
            {
                runText += "\n";
                AddIdleProc();
                return;
            }
            nskip = 0;

            FlushOutput();

            Run run = new Run();
            runText = "";
            Paragraph para = new Paragraph(run);
            para.Margin = new Thickness(0);
            consoledoc.Blocks.InsertBefore(input, para);
            if (consoledoc.Blocks.Count >= maxlines)
                consoledoc.Blocks.Remove(consoledoc.Blocks.FirstBlock);
            ScrollToEnd();
        }

        void FlushOutput()
        {
            if (!string.IsNullOrEmpty(runText))
            {
                Run lastrun = input.PreviousBlock.ContentEnd.Paragraph.Inlines.LastInline as Run;
                if (lastrun == null)
                    DebugWrite("Nonempty runtext without lastrun [" + runText + "]");
                else
                {
                    lastrun.Text = runText;
                }

                ScrollToEnd();
            }
        }

        bool added_idleproc = false;
        void AddIdleProc()
        {
            if (!added_idleproc)
            {
                Dispatcher.BeginInvoke((Action)IdleProc, DispatcherPriority.ApplicationIdle);
                added_idleproc = true;
            }
        }

        void IdleProc()
        {
            FlushOutput();
            added_idleproc = false;
        }

        public UIElement InsertXaml(string s)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(s);
            System.IO.MemoryStream stream = new System.IO.MemoryStream(byteArray);
            object fe;
            try
            {
                fe = System.Windows.Markup.XamlReader.Load(stream);
            }
            catch (Exception ex)
            {
                DebugWrite("XamlReaderError: [" + ex.Message + "]\n");
                return null;
            }

            UIElement uie = fe as UIElement;
            if (uie == null)
                DebugWrite("Bad XAML: [" + s + "]\n");
            else
            {
                try
                {
                    InlineUIContainer block = new InlineUIContainer(uie);
                    consoledoc.Blocks.InsertBefore(input, new Paragraph(block));
                }
                catch (Exception ex)
                {
                    DebugWrite("Error inserting element: [" + ex.Message + "]\n");
                    return null;
                }

            }
            return uie;
        }

        public void InsertUIElement(UIElement uie)
        {
            try
            {
                InlineUIContainer block = new InlineUIContainer(uie);
                consoledoc.Blocks.InsertBefore(input, new Paragraph(block));
            }
            catch (Exception ex)
            {
                DebugWrite("Error inserting element: [" + ex.Message + "]\n");
            }
        }

        #endregion

        #region Console Read
        public delegate void ReadLineCallback(string s);
        public delegate void ReadLineCallbackSecure(System.Security.SecureString s);

        public void BeginReadLine(ReadLineCallback cb)
        {
            BeginReadLineInternal(cb, false);
        }
        public void BeginReadLineSecure(ReadLineCallbackSecure cb, bool secure = false)
        {
            BeginReadLineInternal(cb, true);
        }

        void BeginReadLineInternal(Delegate cb, bool secure)
        {
            FlushOutput();
            //PasswordBox 
            Control readlinebox;
            if (secure)
                readlinebox = new PasswordBox();
            else
                readlinebox = new TextBox();
            readlinebox.BorderThickness = new Thickness(0);
            readlinebox.Margin = new Thickness(0);
            readlinebox.KeyDown += new KeyEventHandler(
                (Action<object, KeyEventArgs>)((o,e) => readlinebox_KeyDown(o,e,cb)));
            readlinebox.Background = Brushes.DarkKhaki;
            readline_inline = new InlineUIContainer(readlinebox);
            readline_paragraph = input.PreviousBlock.ContentEnd.Paragraph;
            readline_paragraph.Inlines.Add(readline_inline);
            Dispatcher.BeginInvoke((Action)(()=>readlinebox.Focus()), DispatcherPriority.ApplicationIdle);
        }

        Paragraph readline_paragraph;
        Inline readline_inline;

        void readlinebox_KeyDown(object sender, KeyEventArgs e, Delegate cb)
        {
            if (e.Key == Key.Enter) {
                if (sender is PasswordBox)
                {
                    System.Security.SecureString s = ((PasswordBox)sender).SecurePassword;

                    readline_paragraph.Inlines.Remove(readline_inline);
                    string stars = new string('*', s.Length);
                    readline_paragraph.ContentEnd.InsertTextInRun(stars + "\n");

                    ((ReadLineCallbackSecure)cb)(s);
                }
                else
                {
                    string s = ((TextBox)sender).Text;

                    readline_paragraph.Inlines.Remove(readline_inline);
                    readline_paragraph.ContentEnd.InsertTextInRun(s + "\n");

                    ((ReadLineCallback)cb)(s);
                }

                inputbox.Focus();
            }
        }


        #endregion

        internal void ReportProgress(string Activity, string StatusDescription, string CurrentOperation, int PercentComplete)
        {
            progresstext.Text = Activity + "|" + StatusDescription;
            if (!string.IsNullOrWhiteSpace(CurrentOperation))
                progresstext.Text += "\n" + CurrentOperation;

            progress.Value = PercentComplete;
            if (PercentComplete >= 100)
                progressbox.Visibility = System.Windows.Visibility.Collapsed;
            else
                progressbox.Visibility = System.Windows.Visibility.Visible;
        }

        public void DebugWrite(string s)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke((Action)(() => this.DebugWrite(s)), DispatcherPriority.Render);
                return;
            }
            debug.ContentEnd.InsertTextInRun(s);
            Au.Utils.ScrollToEnd(debug.Parent as FlowDocumentScrollViewer, 0);
        }

        static public void StaticDebugWrite(string s)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() => {
                (Application.Current.MainWindow as MainWindow).DebugWrite(s);
            }));
        }

        #region GenerateLines (Test routine)
        [SureCommandBinding("GenerateLines", "CTRL+ALT+G", ControlBinding.inputbox)]
        public void GenerateLines()
        {
            DebugWrite("\nGenerate ");
            if (op == null)
            {
                DebugWrite("starting");
                op = psthread.Dispatcher.BeginInvoke(
                    new Action(() => psthread.GenerateLines()),
                    DispatcherPriority.ApplicationIdle);
                op.Completed += new EventHandler(op_Completed);
            }
            else
            {
                DebugWrite("in flight [" + op.Status + "]");
                if (op.Status == DispatcherOperationStatus.Executing ||
                    op.Status == DispatcherOperationStatus.Pending)
                {
                    DebugWrite("[Aborting " + op.Abort() + "]");
                }
                else
                {
                    DebugWrite("Already on its way out");
                }
            }
        }

        DispatcherOperation op = null;

        void op_Completed(object sender, EventArgs e)
        {
            DebugWrite("Op: " + op.Status + " result " + op.Result);
            op = null;
        }

        #endregion

        bool isalphanumeric(Key k)
        {
            return (k >= Key.D0 && k <= Key.D9) || 
                   (k >= Key.A && k <= Key.Z) || 
                   (k >= Key.NumPad0 && k <= Key.NumPad9);
        }

        private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
        }

        // Text input in the grid is forwarded to the inputbox
        private void Grid_TextInput(object sender, TextCompositionEventArgs e)
        {
            DebugWrite("GTI[" + e.Text + "]");

            inputbox.Focus();

            if (String.IsNullOrEmpty(e.Text))
                return; // E.g. Ctrl-V in the FlowDoc

            TextCompositionEventArgs tc = new TextCompositionEventArgs(e.Device, e.TextComposition);
            tc.RoutedEvent = TextBox.TextInputEvent;
            inputbox.Dispatcher.BeginInvoke((Action)(() => inputbox.RaiseEvent(tc)), DispatcherPriority.ContextIdle);
            // TODO: pass this event to the inputbox so the first char typed isn't lost
        }

        private void ScrollToEnd()
        {
            if (scroller != null) 
                Au.Utils.ScrollToEnd(scroller, 1000);
        }

        internal void SetTitle(string title)
        {
            this.Title = title;
        }

    }


}
