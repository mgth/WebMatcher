using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using NotifyChange;

namespace WebMatcher
{
    public class ViewModel : Notifier
    {
        public ViewModel()
        {
            SystemParameters.StaticPropertyChanged += delegate(object sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName=="WorkArea") RaiseProperty("WorkArea");
            };
        }
        public Matchers Matchers { get; } = new Matchers();

        [DependsOn("WorkArea")]
        public  double AutoHeight => SystemParameters.WorkArea.Height;

        [DependsOn("WorkArea")]
        public  double AutoWidth => 525;

        [DependsOn("WorkArea", "AutoHeight")]
        public double AutoTop => SystemParameters.WorkArea.Top + SystemParameters.WorkArea.Height - AutoHeight;
        [DependsOn("WorkArea", "AutoWidth")]
        public double AutoLeft => SystemParameters.WorkArea.Left + SystemParameters.WorkArea.Width - AutoWidth;
        [DependsOn("WorkArea")]
        public  double AutoListHeight => SystemParameters.WorkArea.Height - 32;

        private bool _pinned = false;
        public bool Pinned { get { return _pinned; } set { SetProperty(ref _pinned, value); } }

        public void SetSize()
        {
            RaiseProperty("WorkArea");
        }

    }
}
