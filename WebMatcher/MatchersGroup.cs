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
            UnWatch(Parent);
            UnWatch(Matchers);

            Parent?.Groups.Remove(this);
        }

        public string Name { get; }

        public Matchers Parent { get; }

        public void OnNotify(Matcher matcher)
        {
            Parent.OnNotify(matcher);
        }


        private bool _expanded;
        public bool Expanded
        {
            get { return _expanded; }
            set { SetProperty(ref _expanded, value); }
        }

        [DependsOn("Matchers.Expanded", "Matchers.ChangedState")]
        public void UpdateExpanded()
        {
            if (Matchers.Any(m => m.Expanded || m.ChangedState))
            {
                Expanded = true;
            }
        }

        private bool _visible = true;
        public bool Visible
        {
            get { return _visible; }

            private set { SetProperty(ref _visible, value); }
        }

        [DependsOn("Matchers.Visible")]
        public void UpdateVisible()
        {
            Visible = Matchers.Any(m => m.Visible);
        }

        private bool _changedState = false;
        private int _count;

        public bool ChangedState
        {
            get { return _changedState; }
            private set { SetProperty(ref _changedState, value); }
        }

        [DependsOn("Matchers.ChangedState")]
        public void UpdateChangedState()
        {
            ChangedState = Matchers.Any(m => m.ChangedState);
        }


        public DateTime CheckMatchers()
        {
            Matcher m;
            int i = 0;

            DateTime minDue = DateTime.MaxValue;

            while (i<Matchers.Count)
            {
                m = Matchers[i];

                DateTime due = m.LastChecked + Parent.Interval;

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
            get { return _count; }
            private set { SetProperty(ref _count, value); }
        }


        [DependsOn("Matchers")]
        public void UpdateCount()
        {
            Count = Matchers.Count;
            if (Count == 0) Parent?.Groups.Remove(this);
        }

       private int _checkedCount;
        public int CheckedCount
        {
            get { return _checkedCount; }
            set { SetProperty(ref _checkedCount, value); }
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
