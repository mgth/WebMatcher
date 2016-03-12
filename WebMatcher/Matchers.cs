using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using NotifyChange;

namespace WebMatcher
{
    public class Model : Notifier
    {
        public Matchers Parent { get; }
        public Model(Matchers parent, string name)
        {
            Parent = parent;
            Name = name;
            Parent?.Models.Add(this);

            Watch(Parent, "Parent");
            Watch(Matchers, "Matchers");
        }
        ~Model()
        {
            UnWatch(Parent);
            UnWatch(Matchers);

            Parent?.Models.Remove(this);
        }
        public ObservableCollection<Matcher> Matchers { get; } = new ObservableCollection<Matcher>();

        private string _name = "";
        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        private string _expression = "";
        public string Expression
        {
            get { return _expression; }
            set { SetProperty(ref _expression, value); }
        }

        private int _count = 0;
        public int Count
        {
            get { return _count; }
            private set { SetProperty(ref _count, value); }
        }


        [DependsOn("Matchers")]
        public void UpdateCount()
        {
            Count = Matchers.Count;
            if (Count == 0) Parent?.Models.Remove(this);
        }

        public override string ToString()
        {
            return Name;
        }
    }


    public class Matchers : Notifier
    {
        public ObservableCollection<MatchersGroup> Groups { get; } = new ObservableCollection<MatchersGroup>();
        public ObservableCollection<Model> Models { get; } = new ObservableCollection<Model>();

        private readonly DispatcherTimer _timer = new DispatcherTimer();

        public Matchers()
        {
            _timer.Tick += _timer_Tick;
            Watch(Groups, "Groups");
            Watch(Groups, "Models");
        }

        DateTime _lastCheck = DateTime.MinValue;

        private void _timer_Tick(object sender, EventArgs e)
        {
            _lastCheck = DateTime.Now;
            CheckMatchers();
        }

        public event NotifyHandler Notify;

        public void OnNotify(Matcher matcher)
        {
            Notify?.Invoke(matcher);
        }

        public static RegistryKey GetRootKey()
        {
            return
                Registry.CurrentUser.CreateSubKey("SOFTWARE\\" + System.Windows.Forms.Application.CompanyName + "\\" +
                                                  System.Windows.Forms.Application.ProductName);
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
            set { SetProperty(ref _viewAll, value ?? false); }
        }

        bool _enabled = true;

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (SetProperty(ref _enabled, value))
                {
                    using (RegistryKey k = GetRootKey())
                    {
                        k.SetValue("Enabled", value ? 1 : 0, RegistryValueKind.DWord);
                    }
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
                if (SetProperty(ref _interval, value))
                {
                    using (RegistryKey k = GetRootKey())
                    {
                        k.SetValue("Interval", (int)value.TotalMinutes, RegistryValueKind.DWord);
                    }
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
                    using (
                        RegistryKey k =
                            Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
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
                    SetProperty(ref _loadAtStartup, value);
                    {
                        using (RegistryKey k = Registry.CurrentUser.OpenSubKey
                            ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                        {
                            if (k != null)
                            {
                                if (value)
                                {
                                    k.SetValue(System.Windows.Forms.Application.ProductName,
                                        System.Windows.Forms.Application.ExecutablePath.ToString());
                                }
                                else
                                {
                                    k.DeleteValue(System.Windows.Forms.Application.ProductName, false);
                                }
                            }
                        }
                    }
                }
                catch (SecurityException)
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
                    new Matcher(this, s);
                }
            }
        }

        public MatchersGroup GetGroup(string name)
            => Groups.FirstOrDefault(g => g.Name == name) ?? new MatchersGroup(this, name);

        public Model GetModel(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return Models.FirstOrDefault(m => m.Name == name) ?? new Model(this, name);
        }

        public void CheckMatchers()
        {
            MatchersGroup g;

            DateTime minDue = DateTime.MaxValue;

            int i = 0;
            while (i < Groups.Count)
            {
                g = Groups[i];
                DateTime due = g.CheckMatchers();

                if (due < minDue) minDue = due;

                i = Groups.IndexOf(g) + 1;
            }

            Schedule(minDue);
        }

        public void Schedule(DateTime due)
        {
            if (_timer.IsEnabled)
            {
                if (due == DateTime.MaxValue)
                    _timer.Stop();
                else
                {
                    if (_lastCheck + _timer.Interval > due)
                    {
                        _timer.Interval = due - _lastCheck;
                    }
                }
            }
            else
            {
                if (due < DateTime.MaxValue)
                {
                    _lastCheck = DateTime.Now;
                    _timer.Interval = due - _lastCheck;
                    _timer.Start();
                }
            }
        }

        public void ForceCheck()
        {
            foreach (MatchersGroup group in Groups) group.ForceCheck();
        }



        private bool _changedState = false;
        private int _count = 0;
        private int _checkedCount = 0;

        public bool ChangedState
        {
            get { return _changedState; }
            set { SetProperty(ref _changedState, value); }
        }

        [DependsOn("Groups", "Groups.ChangedState")]
        public void UpdateChangedState()
        {
            ChangedState = Groups.Any(g => g.ChangedState);
        }

        public int Count
        {
            get { return _count; }
            private set { SetProperty(ref _count,value); }
        }

        [DependsOn("Groups", "Groups.Matchers")]
        public void UpdateCount(string s)
        {
            Count = Groups.Sum(g => g.Count);
        }

        public int CheckedCount
        {
            get { return _checkedCount; }
            private set { SetProperty(ref _checkedCount,value); }
        }

        [DependsOn("Groups", "Groups.CheckedCount")]
        public void UpdateCheckedCount()
        {
            CheckedCount = Groups.Sum(g => g.CheckedCount);
        }
    }
}
