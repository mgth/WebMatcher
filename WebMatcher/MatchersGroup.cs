using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebMatcher
{
    public class MatchersGroup :  ObservableCollection<Matcher>
    {

        public MatchersGroup(Matchers parent,string name)
        {
            _parent = parent;
            _name = name;
            _parent.Add(this);
        }
        private void changed(String name)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(name));
        }

        String _name = "";
        public String Name
        {
            get { return _name; }
        }

        Matchers _parent;
        public Matchers Parent
        {
            get { return _parent; }
        }

        private void MatchersGroup_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);
            CheckChanged("Expanded",ref _expanded);
            if (CheckChanged("ChangedState", ref _changedState))
            {
                changed("Visibility");
            }
        }

        private bool CheckChanged(string property,ref bool value)
        {
            bool old = value;
            value = (bool)this.GetType().GetProperty(property).GetValue(this);
            if (old!=value)
            {
                changed(property);
                return true;
            }
            return false;
        }

        public System.Windows.Visibility Visibility
        {
            get
            {
                if (Expanded) return System.Windows.Visibility.Visible;
                else return System.Windows.Visibility.Collapsed;

            }
        }

        bool _expanded = false;
        public bool Expanded
        {
            get
            {
                foreach (Matcher m in this) { if (m.IsNew || m.ChangedState) return true; }
                return false;
            }
        }

        private bool _changedState = false;
        public bool ChangedState
        {
            get
            {
                foreach (Matcher m in this) { if (m.ChangedState) return true; }
                return false;
            }
        }


        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);

            if(Count == 0)
            {
                Parent.Remove(this);
            }
        }
        public int CheckMatchers()
        {
            Matcher m;
            int i = 0;
            int queued = 0;

            while(i<Count)
            {
                m = this[i];
                if (m.TimeToCheck)
                {
                    m.Queued = true;
                    queued++;
                    ThreadPool.QueueUserWorkItem(m.Check);
                }
                i = IndexOf(m) + 1;
            }
            return queued;
        }
        public void ForceCheck()
        {
            foreach (Matcher m in this) m.ForcedCheck = true;
        }
    }
}
