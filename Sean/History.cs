using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;

namespace Sure
{
    class History
    {
        // bool navigating_history
        int index = 0;
        int begin = 0;
        int end = 0;
        bool loaded = false;
        public bool Loaded { get { return loaded; } } 
        const int HISTORY_SIZE = 3000;
        string[] history = new string[HISTORY_SIZE];

        public void Init(PowerShellThread psthread)
        {
            if (!loaded)
            {
                PSObject[] psos = psthread.GetHistory();
                if (psos == null)
                {
                    return;
                }
                int n = psos.Length;
                if (n > HISTORY_SIZE - 1) n = HISTORY_SIZE - 1;
                for (int i = 0; i < n; ++i)
                {
                    string s = (from p in psos[i].Properties where p.Name == "CommandLine" select p.Value).Single() as string;
                    history[begin + i] = s;
                }
                end = n;
                loaded = true;
            }

            Reset();
        }

        public void Add(string line)
        {
            history[end] = line;
            end = succ(end);
            if (end == begin)
                begin = succ(begin);
        }
                
        public void Reset()
        {
            index = end;
            history[index] = null;
        }

        bool atEnd(int i)
        {
            return succ(i) == end;
        }

        int succ(int i)
        {
            if (i == HISTORY_SIZE - 1)
                return 0;
            else
                return i + 1;
        }

        int pred(int i)
        {
            if (i == 0)
                return HISTORY_SIZE - 1;
            else
                return i - 1;
        }

        /// <summary>
        /// Move forward in history, returning null if we meet the end
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public string Forward(string prefix)
        {
            while (index != end)
            {
                index = succ(index);
                if (index == end)
                   break;
                if (history[index].StartsWith(prefix))
                   break;
            }
            return history[index];
        }

        /// <summary>
        /// Move backward in history, returning null if we hit the start.
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public string Back(string prefix)
        {
            while (index != begin)
            {
                index = pred(index);
                if (history[index].StartsWith(prefix))
                {
                    return history[index];
                }
            }
            return null;
        }

    }
}
