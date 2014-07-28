using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sure
{
    [AttributeUsage(AttributeTargets.All)]
    public class SureCommandBindingAttribute : Attribute
    {
        string commandname;
        string defaultkey;
        ControlBinding target_mask;

        delegate void Exec();
        Exec exec;
        Control target;

        public SureCommandBindingAttribute(string commandname, string defaultkey, ControlBinding target_mask)
        {
            this.commandname = commandname;
            this.defaultkey = defaultkey;
            this.target_mask = target_mask;
        }

        /// <summary>
        /// Search for all methods on <paramref name="instance"/> which have command binding attributes
        /// defined, and add them to the controls defined in <paramref name="ctrlmap"/>
        /// </summary>
        /// <param name="instance">The object whose methods are to be searched</param>
        /// <param name="rctype">Owner type of the RoutedCommand (e.g. <code>typeof(instance)</code>)</param>
        /// <param name="ctrlmap">Table mapping Controls to ControlBindings used in the attributes</param>
        public static void AddBindingsToControls(object instance, Type rctype, Dictionary<Control, ControlBinding> ctrlmap)
        {
            foreach(var v in ctrlmap.Keys)
                AddBindingsToControl(instance, rctype, v, ctrlmap[v]);
        }

        /// <summary>
        /// Search for all methods on <paramref name="instance"/> which have command bindings
        /// defined, and add them to the controls indicated by <paramref name="target_mask"/>
        /// </summary>
        /// <param name="instance">The object whose methods are to be searched</param>
        /// <param name="rctype">Owner type of the RoutedCommand (e.g. <code>typeof(instance)</code>)</param>
        /// <param name="target">The control which is to listen for the command</param>
        /// <param name="target_mask">The mask corresponding to <paramref name="target"/></param>
        public static void AddBindingsToControl(object instance, Type rctype, Control target, ControlBinding target_mask)
        {
            // Set up command bindings based on attributes from ReadLine
            foreach (MethodInfo method in instance.GetType().GetMethods())
            {
                //System.Diagnostics.Debug.WriteLine(method.ToString());
                foreach (SureCommandBindingAttribute a in
                         method.GetCustomAttributes(typeof(SureCommandBindingAttribute), true))
                 if ((a.target_mask & target_mask) > 0) {
                    a.exec = (Exec)Delegate.CreateDelegate(typeof(Exec), instance, method);
                    a.target = target;
                    
                    RoutedCommand cmd = new RoutedCommand(a.commandname, rctype);
                    CommandBinding cb = new CommandBinding(cmd, a.Execute, a.CanExecute);
                    InputBinding ib = new InputBinding(cmd,
                        (KeyGesture)(new KeyGestureConverter().ConvertFromString(a.defaultkey)));
                    //(Application.Current.MainWindow as MainWindow).DebugWrite("BIND " + a.commandname + " " + ib.Gesture.ToString() + "\n");
                    a.target.CommandBindings.Add(cb);
                    a.target.InputBindings.Add(ib);
                }
            }
        }

        private void CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            
        }

        private void Execute(object sender, ExecutedRoutedEventArgs e)
        {
            // (Application.Current.MainWindow as MainWindow).DebugWrite("EXEC[ " + commandname + "]");
            exec();
        }

        public override string ToString()
        {
            return "{CmdBind " + commandname + "|" + defaultkey + "}";
        }
    }
}
