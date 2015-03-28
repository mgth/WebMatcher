using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebMatcher
{
    public class Matchers : INotifyPropertyChanged
    {
        private ObservableCollection<MatchersGroup> _groups = new ObservableCollection<MatchersGroup>();
        public ObservableCollection<MatchersGroup> Groups { get { return _groups; } }


        public event NotifyHandler Notify;
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        public void OnNotify(Matcher matcher)
        {
            CheckChanged("ChangedState", ref _changedState);
            if (Notify != null) Notify(matcher);
        }
        public RegistryKey GetRootKey()
        {
            return Registry.CurrentUser.CreateSubKey("SOFTWARE\\" + System.Windows.Forms.Application.CompanyName + "\\" + System.Windows.Forms.Application.ProductName);
        }
        public int MaxNbThreads
        {
            get
            {
                int nb;
                int nb2;

                ThreadPool.GetMaxThreads(out nb, out nb2);
                return nb;
            }
            set
            {
                ThreadPool.SetMaxThreads(value, value);
                using (RegistryKey k = GetRootKey())
                {
                    k.SetValue("MaxNbThreads", value, RegistryValueKind.DWord);
                }
            }
        }

        bool _viewAll = true;
        public bool? ViewAll
        {
            get { return _viewAll; }
            set
            {
                if (_viewAll != value) { _viewAll = value ?? false; OnPropertyChanged("ViewAll"); }
            }
        }

        bool _enabled = true;
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                using (RegistryKey k = GetRootKey())
                {
                    k.SetValue("Enabled", value ? 1 : 0, RegistryValueKind.DWord);
                }
            }
        }

        // Time Span between to watcher check.
        private TimeSpan _interval = TimeSpan.FromMinutes(60);
        public TimeSpan Interval
        {
            get { return _interval; }
            set
            {
                _interval = value;
                using (RegistryKey k = GetRootKey())
                {
                    k.SetValue("Interval", (int)value.TotalMinutes, RegistryValueKind.DWord);
                }
            }
        }

        private static Boolean? _loadAtStartup = null;
        public Boolean LoadAtStartup
        {
            get
            {
                if (_loadAtStartup == null)
                {
                    String startup = "";
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                    {
                        startup = k.GetValue(System.Windows.Forms.Application.ProductName, "").ToString();
                    }
                    if (startup == System.Windows.Forms.Application.ExecutablePath.ToString())
                        _loadAtStartup = true;
                    else
                        _loadAtStartup = false;
                }
                return _loadAtStartup ?? false;
            }

            //            [PrincipalPermission(SecurityAction.Demand, Role = @"BUILTIN\Administrators")]
            set
            {
                try
                {
                    _loadAtStartup = value;
                    {
                        using (RegistryKey k = Registry.CurrentUser.OpenSubKey
                            ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                        {
                            if (k != null)
                            {
                                if (value)
                                {
                                    k.SetValue(System.Windows.Forms.Application.ProductName, System.Windows.Forms.Application.ExecutablePath.ToString());
                                }
                                else
                                {
                                    k.DeleteValue(System.Windows.Forms.Application.ProductName, false);
                                }
                            }
                        }
                    }

                }
                catch (SecurityException ex)
                {

                }
            }
        }
        public void LoadMatchers()
        {
            using (RegistryKey k = GetRootKey())
            {
                Interval = TimeSpan.FromMinutes((int)k.GetValue("Interval", 60));
                MaxNbThreads = (int)k.GetValue("MaxNbThreads", 10);
                Enabled = (int)k.GetValue("Enabled", 1) == 1;

                String[] keys = k.GetSubKeyNames();

                foreach (String s in keys)
                {
                    Matcher w = new Matcher(this);
                    w.Load(s);
                }

            }
        }
        public MatchersGroup GetGroup(string name)
        {
            foreach (MatchersGroup group in Groups)
            {
                if (group.Name == name) return group;
            }

            return new MatchersGroup(this, name);
        }

        public void CheckMatchers()
        {
            MatchersGroup g;

            while (Enabled)
            {
                int queued = 0;
                int i = 0;
                while (i < Groups.Count)
                {
                    g = Groups[i];
                    queued += g.CheckMatchers();

                    i = Groups.IndexOf(g) + 1;
                }
                if (queued == 0) Thread.Sleep(1000);
            }
        }
        public void ForceCheck()
        {
            foreach (MatchersGroup group in Groups) group.ForceCheck();
        }


        public double LabelSize
        {
            get
            {
                double s = 0;
                foreach (MatchersGroup group in Groups)
                {
                    if(group.Visible)
                    {
                        double gs = group.LabelSize;
                        if(gs>s) s=gs;
                    }
                }
                return s;
            }
        }
        private bool CheckChanged(string property, ref bool value)
        {
            bool old = value;
            value = (bool)this.GetType().GetProperty(property).GetValue(this);
            if (old != value)
            {
                OnPropertyChanged(property);
                return true;
            }
            return false;
        }

        private bool _changedState = false;
        public bool ChangedState
        {
            get
            {
                foreach (MatchersGroup group in Groups) { if (group.ChangedState) return true; }
                return false;
            }
        }

    }
}
