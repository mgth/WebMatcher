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
    public class MatchersGroup : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        private ObservableCollection<Matcher> _matchers = new ObservableCollection<Matcher>();
        public ObservableCollection<Matcher> Matchers { get { return _matchers; } }

        public MatchersGroup(Matchers parent,string name)
        {
            _parent = parent;
            _name = name;
            _parent.Groups.Add(this);
            _parent.PropertyChanged += _parent_PropertyChanged;
            _parent.Groups.CollectionChanged += Groups_CollectionChanged;
        }

        private void Groups_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {

            if (Matchers.Count == 0)
            {
                Parent.Groups.Remove(this);
            }
            else
            {
                CheckChanged("Expanded", ref _expanded);
                CheckChanged("ChangedState", ref _changedState);
                CheckChanged("Visible", ref _visible);
            }
        }

        private void _parent_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName=="CheckAll")
                CheckChanged("Visible", ref _visible);
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


        private bool CheckChanged(string property,ref bool value)
        {
            bool old = value;
            value = (bool)this.GetType().GetProperty(property).GetValue(this);
            if (old!=value)
            {
                OnPropertyChanged(property);
                return true;
            }
            return false;
        }

        bool _expanded = false;
        public bool Expanded
        {
            get
            {
                foreach (Matcher m in Matchers) { if (m.IsNew || m.ChangedState) return true; }
                return false;
            }
        }

        bool _visible = false;
        public bool Visible
        {
            get
            {
                foreach (Matcher m in Matchers) { if (m.IsNew || m.ChangedState || (Parent.ViewAll??false) ) return true; }
                return false;
            }
        }

        private bool _changedState = false;
        public bool ChangedState
        {
            get
            {
                foreach (Matcher m in Matchers) { if (m.ChangedState) return true; }
                return false;
            }
        }


        public int CheckMatchers()
        {
            Matcher m;
            int i = 0;
            int queued = 0;

            while(i<Matchers.Count)
            {
                m = Matchers[i];
                if (m.TimeToCheck)
                {
                    m.Queued = true;
                    queued++;
                    ThreadPool.QueueUserWorkItem(m.Check);
                }
                i = Matchers.IndexOf(m) + 1;
            }
            return queued;
        }
        public void ForceCheck()
        {
            foreach (Matcher m in Matchers) m.ForcedCheck = true;
        }
    }
}
