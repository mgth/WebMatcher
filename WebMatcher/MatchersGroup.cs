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

        bool _expanded = false; 
        private void MatchersGroup_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);

            bool oldExpanded = _expanded;
            _expanded = Expanded;

            if (oldExpanded != _expanded)
            {
                changed("Expanded");
            }
        }


        public bool Expanded
        {
            get
            {
                foreach (Matcher m in this) { if (m.IsNew || m.ChangedState) return true; }
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
