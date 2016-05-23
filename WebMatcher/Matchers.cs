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
            UnWatch(Parent, "Parent");
            UnWatch(Matchers, "Matchers");

            Parent?.Models.Remove(this);
        }
        public ObservableCollection<Matcher> Matchers { get; } = new ObservableCollection<Matcher>();

        public string Name
        {
            get { return GetProperty<string>(); }
            set { SetProperty(value); }
        }

        public string Expression
        {
            get { return GetProperty<string>(); }
            set { SetProperty(value); }
        }

        public int Count
        {
            get { return GetProperty<int>(); }
            private set { SetProperty(value); }
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


    public class Matchers : RegistryNotifier
    {
        public ObservableCollection<MatchersGroup> Groups { get; } = new ObservableCollection<MatchersGroup>();
        public ObservableCollection<Model> Models { get; } = new ObservableCollection<Model>();

        private readonly DispatcherTimer _timer = new DispatcherTimer();

        public Matchers()
        {
            _timer.Tick += _timer_Tick;
            Watch(Groups, "Groups");
            Watch(Models, "Models");
        }

        DateTime _lastCheck = DateTime.MinValue;

        private void _timer_Tick(object sender, EventArgs e)
        {
            _lastCheck = DateTime.Now;
            CheckMatchers();
        }

        public event NotifyHandler TrayNotification;

        public void OnNotify(Matcher matcher)
        {
            TrayNotification?.Invoke(matcher);
        }


        public override string RootKey => "SOFTWARE\\" + System.Windows.Forms.Application.CompanyName + "\\" +
                                          System.Windows.Forms.Application.ProductName;


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
                using (RegistryKey k = GetRegistryKey())
                {
                    k.SetValue("MaxNbThreads", value, RegistryValueKind.DWord);
                }
            }
        }

        public bool? ViewAll
        {
            get { return GetProperty<bool?>(); }
            set { SetProperty(value ?? false); }
        }
        [DependsOn("init_ViewAll")]
        private void InitViewAll()
        {
            ViewAll = true;
        }

        public bool Enabled
        {
            get { return GetProperty<bool>(); }
            set
            {
                if (SetProperty(value))
                {
                    using (RegistryKey k = GetRegistryKey())
                    {
                        k.SetValue("Enabled", value ? 1 : 0, RegistryValueKind.DWord);
                    }
                }
            }
        }

        public TimeSpan Interval
        {
            get { return GetProperty<TimeSpan>(); }
            set
            {
                if (SetProperty(value))
                {
                    using (RegistryKey k = GetRegistryKey())
                    {
                        k.SetValue("Interval", (int)value.TotalMinutes, RegistryValueKind.DWord);
                    }
                }
            }
        }

        [DependsOn("init_Interval")]
        private void InitInterval()
        {
            Interval = TimeSpan.FromMinutes(60);
        }

        public bool LoadAtStartup
        {
            get
            {
                if (GetProperty<bool?>() == null)
                {
                    string startup;
                    using (
                        var k =
                            Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                    {
                        startup = k?.GetValue(System.Windows.Forms.Application.ProductName, "").ToString();
                    }
                    SetProperty(startup == System.Windows.Forms.Application.ExecutablePath);
                }
                return GetProperty<bool?>()??false;
            }

            //            [PrincipalPermission(SecurityAction.Demand, Role = @"BUILTIN\Administrators")]
            set
            {
                try
                {
                    SetProperty(value);
                    {
                        using (var k = Registry.CurrentUser.OpenSubKey
                            ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                        {
                            if (k == null) return;
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
                catch (SecurityException)
                {

                }
            }
        }

        public void LoadMatchers()
        {
            using (var k = GetRegistryKey())
            {
                Interval = TimeSpan.FromMinutes((int)k.GetValue("Interval", 60));
                MaxNbThreads = (int)k.GetValue("MaxNbThreads", 10);
                Enabled = (int)k.GetValue("Enabled", 1) == 1;

                var keys = k.GetSubKeyNames();

                foreach (var s in keys)
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



        public bool ChangedState
        {
            get { return GetProperty<bool>(); }
            set { SetProperty(value); }
        }

        [DependsOn("Groups.ChangedState")]
        public void UpdateChangedState()
        {
            ChangedState = Groups.Any(g => g.ChangedState);
        }

        public int Count
        {
            get { return GetProperty<int>(); }
            private set { SetProperty(value); }
        }

        [DependsOn("Groups.Matchers")]
        public void UpdateCount(string s)
        {
            Count = Groups.Sum(g => g.Count);
        }

        public int CheckedCount
        {
            get { return GetProperty<int>(); }
            private set { SetProperty(value); }
        }

        [DependsOn("Groups.CheckedCount")]
        public void UpdateCheckedCount()
        {
            CheckedCount = Groups.Sum(g => g.CheckedCount);
        }
    }
}
