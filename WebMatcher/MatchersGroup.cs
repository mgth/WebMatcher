using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using NotifyChange;

namespace WebMatcher
{
    public class MatchersGroup : Notifier
    {

        public ObservableCollection<Matcher> Matchers { get; } = new ObservableCollection<Matcher>();

        public MatchersGroup(Matchers parent,string name)
        {
            Parent = parent;
            Name = name;
            Parent?.Groups.Add(this);

            Watch(Parent,"Parent");
            Watch(Matchers, "Matchers");
        }

        ~MatchersGroup()
        {
            UnWatch(Parent, "Parent");
            UnWatch(Matchers, "Matchers");

            Parent?.Groups.Remove(this);
        }

        public string Name { get; }

        public Matchers Parent { get; }

        public void OnNotify(Matcher matcher)
        {
            Parent.OnNotify(matcher);
        }


        public bool Expanded
        {
            get { return GetProperty<bool>(); }
            set { SetProperty(value); }
        }

        [DependsOn("Matchers.Expanded", "Matchers.ChangedState")]
        public void UpdateExpanded()
        {
            if (Matchers.Any(m => m.Expanded || m.ChangedState))
            {
                Expanded = true;
            }
        }

        public bool Visible
        {
            get { return GetProperty<bool>(); }

            private set { SetProperty(value); }
        }

        [DependsOn("Matchers.Visible")]
        public void UpdateVisible()
        {
            Visible = Matchers.Any(m => m.Visible);
        }

        public bool ChangedState
        {
            get { return GetProperty<bool>(); }
            private set { SetProperty(value); }
        }

        [DependsOn("Matchers.ChangedState")]
        public void UpdateChangedState()
        {
            ChangedState = Matchers.Any(m => m.ChangedState);
        }


        public DateTime CheckMatchers()
        {
            var i = 0;

            var minDue = DateTime.MaxValue;

            while (i<Matchers.Count)
            {
                var m = Matchers[i];

                var due = m.LastChecked + Parent.Interval;

                if (due<DateTime.Now)
                {
                    m.Enqueue();
                    due = DateTime.Now + Parent.Interval;
                }

                if (due < minDue)
                {
                    minDue = due;
                }
                i = Matchers.IndexOf(m) + 1;
            }
            return minDue;
        }
        public void ForceCheck()
        {
            foreach (Matcher m in Matchers) m.Enqueue();
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
            if (Count == 0) Parent?.Groups.Remove(this);
        }

        public int CheckedCount
        {
            get { return GetProperty<int>(); }
            set { SetProperty(value); }
        }

        [DependsOn("Matchers", "Matchers.Status")]
        public void UpdateCheckedCount()
        {
           CheckedCount = Matchers.Count(matcher => matcher.RunningStatus != State.Running && matcher.Status != State.NotChecked);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
