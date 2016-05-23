using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Globalization;
using System.Diagnostics;
using System.Drawing;

namespace WebMatcher
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>


    public partial class MainWindow : Window
   {


        private System.Windows.Forms.NotifyIcon _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = Properties.Resources.App,
            Visible = true,
        };

        public ViewModel ViewModel = new ViewModel();

        public MainWindow()
        {
            App.SelectCulture(CultureInfo.CurrentCulture.IetfLanguageTag);
            //App.SelectCulture("en-US");

            _notifyIcon.Click +=
                delegate(object sender, EventArgs args)
                {
                    try
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                    }
                    catch (ArgumentException)
                    {

                    }
                };

            ViewModel.Matchers.TrayNotification += Matchers_Notify;

            _notifyIcon.BalloonTipClicked += NotifyIconBalloonTipClicked;
            _notifyIcon.BalloonTipClosed += NotifyIconBalloonTipClosed;

            ViewModel.Matchers.LoadMatchers();

            InitializeComponent();

            DataContext = ViewModel;
            
            LstMatchers.Items.SortDescriptions.Add(new SortDescription("Name",ListSortDirection.Ascending));

            ServicePointManager.DefaultConnectionLimit = 1600;

            ViewModel.Matchers.CheckMatchers();

            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            SetSize();
        }

        private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            SetSize();
        }


        void SetSize()
        {
            Top = ViewModel.AutoTop;
            Left = ViewModel.AutoLeft;
            Width = ViewModel.AutoWidth;
            Height = ViewModel.AutoHeight;
        }
 
        private void NotifyIconBalloonTipClosed(object sender, EventArgs e)
        {
            _ballonMatcher = null;
        }

        private Matcher _ballonMatcher = null;
        private void Matchers_Notify(Matcher matcher)
        {
            if (matcher.ChangedState && !string.IsNullOrEmpty(matcher.Value))
            {
            _ballonMatcher = matcher;
            _notifyIcon.ShowBalloonTip(30,matcher.Name,matcher.Value,System.Windows.Forms.ToolTipIcon.Info);
            }
            else _ballonMatcher = null;
            SetAppIcon(matcher.Parent);
        }

        private void NotifyIconBalloonTipClicked(object sender, EventArgs e)
        {
            _ballonMatcher?.Open();
        }

        private void cmdExit(object sender, RoutedEventArgs e)
        {
            this.Close();
            Process.GetCurrentProcess().Kill();
        }
        void ClosingEvent(object sender, CancelEventArgs e)
        {
            DisableNotifyIcon();
        }

        //private void Notify(Matcher m)
        //{
        //    _notifyIcon?.ShowBalloonTip(10000, m.Name, (string.IsNullOrEmpty(m.Value)) ? "..." : m.Value, System.Windows.Forms.ToolTipIcon.Info);
        //}

        private void cmdAdd_Click(object sender, RoutedEventArgs e)
        {
            Matcher m = new Matcher(ViewModel.Matchers);
            //LstMatchers.SelectedItem = m.Group; 
            //
            //LstMatchers.ScrollIntoView(m.Group);
            //ListBoxItem itemGroup = (ListBoxItem)(LstMatchers.ItemContainerGenerator.ContainerFromItem(m.Group));

            m.Expanded = true;
            LstMatchers.UpdateLayout();


            m.Name = FindResource("str_NewName").ToString();


            if (!Clipboard.ContainsText(TextDataFormat.Text)) return;

            string txt = Clipboard.GetText(TextDataFormat.Text);
            if (!txt.StartsWith("http")) return;
            m.Url = new Uri(txt);

            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(txt);
                request.Method = "GET";
                Matcher.SetHeader(request, "User-Agent", "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)");

                // make request for web page
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader websrc = new StreamReader(response.GetResponseStream());
                String src = websrc.ReadToEnd();
                response.Close();

                Match match = Regex.Match(src, "<meta[^>]*?name=\"description\"[^>]*?content=\"([^\"]*)\"", RegexOptions.Singleline);
                if (match.Success) m.Name = match.Groups[1].Value;

            }
            catch (Exception)
            {
                // ignored
            }
        }


        private void cmdCheck_Click(object sender, EventArgs e)
        {
            ViewModel.Matchers.ForceCheck();
        }



        //public static TchildItem FindVisualChild<TchildItem>(DependencyObject obj) where TchildItem : DependencyObject
        //{
        //    for (int i = 0; i <= VisualTreeHelper.GetChildrenCount(obj) - 1; i++)
        //    {
        //        DependencyObject child = VisualTreeHelper.GetChild(obj, i);

        //        if (child != null && child is TchildItem)
        //            return (TchildItem)child;
        //        else
        //        {
        //            TchildItem childOfChild = FindVisualChild<TchildItem>(child);

        //            if (childOfChild != null)
        //                return childOfChild;
        //        }
        //    }

        //    return null;
        //}

        //private object RowControl(String Name, Matcher m=null)
        //{
        //    DataGridRow row = (DataGridRow)(LstMatchers.ItemContainerGenerator.ContainerFromItem(m==null?LstMatchers.SelectedItem:m));
        //    DataGridDetailsPresenter presenter = FindVisualChild<DataGridDetailsPresenter>(row);

        //    DataTemplate template = presenter.ContentTemplate;

        //    return template.FindName(Name, presenter);
        //}






        private void cmdOptions_Click(object sender, RoutedEventArgs e)
        {
            if (pnlOptions.Visibility == Visibility.Collapsed) pnlOptions.Visibility = Visibility.Visible;
            else pnlOptions.Visibility = Visibility.Collapsed;
        }

        private void cmdUp_MaxNbThreads_Click(object sender, RoutedEventArgs e) { ViewModel.Matchers.MaxNbThreads++; }
        private void cmdDown_MaxNbThreads_Click(object sender, RoutedEventArgs e) { ViewModel.Matchers.MaxNbThreads--; }

        private void cmdUp_Hours_Click(object sender, RoutedEventArgs e) {
            try { ViewModel.Matchers.Interval += TimeSpan.FromHours(1); }
            catch (ArgumentOutOfRangeException) { }
        }
        private void cmdDown_Hours_Click(object sender, RoutedEventArgs e) {
            try { ViewModel.Matchers.Interval -= TimeSpan.FromHours(1); }
            catch (ArgumentOutOfRangeException) { }
        }

        public int IntervalHours => (int)ViewModel.Matchers.Interval.TotalHours;

        private void cmdUp_Minutes_Click(object sender, RoutedEventArgs e)
        {
            try { ViewModel.Matchers.Interval += TimeSpan.FromMinutes(1); }
            catch (ArgumentOutOfRangeException) { }
        }
        private void cmdDown_Minutes_Click(object sender, RoutedEventArgs e)
        {
            try { ViewModel.Matchers.Interval -= TimeSpan.FromMinutes(1); }
            catch (ArgumentOutOfRangeException) { }

        }



        //public void LstMatchersRefresh()
        //{
        //    var view = CollectionViewSource.GetDefaultView(ViewModel.Matchers);
        //    view?.Refresh();
        //    // TODO : add something to expand where changed
        //}


        public void SetAppIcon(Matchers matchers)
        {
                var icn = (matchers.ChangedState)?Properties.Resources.AppOk : Properties.Resources.App;
                if (_notifyIcon.Icon != icn) _notifyIcon.Icon = icn;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            DisableNotifyIcon();
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            DisableNotifyIcon();
        }

        private void DisableNotifyIcon()
        {
            if (_notifyIcon == null) return;

            _notifyIcon.Icon = null;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
