using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.Drawing;

namespace WebMatcher
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>


    public partial class MainWindow : Window, INotifyPropertyChanged
   {
       public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(String name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        DispatcherTimer _checkerTimer = new DispatcherTimer();


        Matchers _matchers = new WebMatcher.Matchers();
        public Matchers Matchers
        { get { return _matchers; } }



        public double AutoHeight
        {
            get
            {
                return System.Windows.SystemParameters.WorkArea.Height;
            }
        }

        public double AutoWidth { get { return 525; } }

        public double AutoTop
        {
            get
            {
                return System.Windows.SystemParameters.WorkArea.Height - Height;
            }
        }
        public double AutoLeft { get { return System.Windows.SystemParameters.WorkArea.Width - Width; } }

        public double AutoListHeight { get { return System.Windows.SystemParameters.WorkArea.Height - 32; } }

       public void Resize()
        {
            Height = AutoHeight;
            Width = AutoWidth;
            Top=AutoTop;
            Left = AutoLeft;
        }
        System.Windows.Forms.NotifyIcon _notify = new System.Windows.Forms.NotifyIcon
        {
            Icon = Properties.Resources.App,
            Visible = true,
        };

        public MainWindow()
        {
            App.SelectCulture(CultureInfo.CurrentCulture.IetfLanguageTag);
            //App.SelectCulture("en-US");

            _notify.Click +=
                delegate(object sender, EventArgs args)
                {
                    try
                    {
                        Resize();
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                    }
                    catch (ArgumentException ex)
                    {

                    }
                };

            Matchers.Notify += Matchers_Notify;
            _notify.BalloonTipClicked += _notify_BalloonTipClicked;
            _notify.BalloonTipClosed += _notify_BalloonTipClosed;

            Matchers.LoadMatchers();

            InitializeComponent();
            
            lstMatchers.Items.SortDescriptions.Add(new SortDescription("Name",ListSortDirection.Ascending));

            //lstMatchers.Items.GroupDescriptions.Add(new PropertyGroupDescription("Group"));

            System.Net.ServicePointManager.DefaultConnectionLimit = 1600;

            Matchers.CheckMatchers();
//            new Thread(Matchers.CheckMatchers).Start();
        }

        private void _notify_BalloonTipClosed(object sender, EventArgs e)
        {
            _ballonMatcher = null;
        }

        private Matcher _ballonMatcher = null;
        private void Matchers_Notify(Matcher matcher)
        {
            if (matcher.ChangedState)
            {
            _ballonMatcher = matcher;
            _notify.ShowBalloonTip(30,matcher.Name,matcher.Value,System.Windows.Forms.ToolTipIcon.Info);
            }
            else _ballonMatcher = null;
            SetAppIcon();
        }

        private void _notify_BalloonTipClicked(object sender, EventArgs e)
        {
            if (_ballonMatcher!=null)
                _ballonMatcher.Open();
        }

        private void cmdExit(object sender, RoutedEventArgs e)
        {
            this.Close();
            Process.GetCurrentProcess().Kill();
        }
        void ClosingEvent(object sender, CancelEventArgs e)
        {
            _notify.Icon = null;
            _notify.Dispose();
            _notify = null;
        }

        void ResetDataGrid()
        {
            var temp = lstMatchers.ItemsSource;
            lstMatchers.ItemsSource = null;
            lstMatchers.ItemsSource = temp;
        }

        void Notify(Matcher m)
        {
                if (_notify != null)
                {
                    _notify.ShowBalloonTip(10000, m.Name, (m.Value == "") ? "..." : m.Value, System.Windows.Forms.ToolTipIcon.Info);
                }
        }

    private void cmdAdd_Click(object sender, RoutedEventArgs e)
        {
            Matcher m = new Matcher(Matchers);
            lstMatchers.SelectedItem = m; 
            lstMatchers.UpdateLayout();
            lstMatchers.ScrollIntoView(m);
            DataGridRow row = (DataGridRow)(lstMatchers.ItemContainerGenerator.ContainerFromItem(m));
            if (row != null)
            {
                row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
//                lstMatchers.ScrollIntoView(m);

                row.DetailsVisibility = Visibility.Visible;
                lstMatchers.UpdateLayout();
                lstMatchers.ScrollIntoView(m);
                (RowControl("txtName", m) as TextBox).Text = FindResource("str_NewName").ToString();
                if (Clipboard.ContainsText(TextDataFormat.Text))
                {
                    String txt = Clipboard.GetText(TextDataFormat.Text);
                    if (txt.StartsWith("http"))
                    {
                        (RowControl("txtUrl",m) as TextBox).Text = txt;

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
                            if (match.Success) (RowControl("txtName",m) as TextBox).Text = match.Groups[1].Value;

                        }
                        catch (Exception ex)
                        {
                        }

                    }
                }

                TextBox tn = (TextBox)RowControl("txtName",m); if (tn != null) { tn.Focus(); tn.SelectAll(); }
           }
        }


        private void cmdCheck_Click(object sender, EventArgs e)
        {
            Matchers.ForceCheck();
        }



        public static childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i <= VisualTreeHelper.GetChildrenCount(obj) - 1; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);

                if (child != null && child is childItem)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);

                    if (childOfChild != null)
                        return childOfChild;
                }
            }

            return null;
        }

        private object RowControl(String Name, Matcher m=null)
        {
            DataGridRow row = (DataGridRow)(lstMatchers.ItemContainerGenerator.ContainerFromItem(m==null?lstMatchers.SelectedItem:m));
            DataGridDetailsPresenter presenter = FindVisualChild<DataGridDetailsPresenter>(row);

            DataTemplate template = presenter.ContentTemplate;

            return template.FindName(Name, presenter);
        }






        private void cmdOptions_Click(object sender, RoutedEventArgs e)
        {
            if (pnlOptions.Visibility == Visibility.Collapsed) pnlOptions.Visibility = Visibility.Visible;
            else pnlOptions.Visibility = Visibility.Collapsed;
        }

        private void cmdUp_MaxNbThreads_Click(object sender, RoutedEventArgs e) { Matchers.MaxNbThreads++; }
        private void cmdDown_MaxNbThreads_Click(object sender, RoutedEventArgs e) { Matchers.MaxNbThreads--; }

        private void cmdUp_Hours_Click(object sender, RoutedEventArgs e) {
            try { Matchers.Interval += TimeSpan.FromHours(1); }
            catch (ArgumentOutOfRangeException ex) { }
        }
        private void cmdDown_Hours_Click(object sender, RoutedEventArgs e) {
            try { Matchers.Interval -= TimeSpan.FromHours(1); }
            catch (ArgumentOutOfRangeException ex) { }
        }

        public int IntervalHours { get { return (int)Matchers.Interval.TotalHours; } }

        private void cmdUp_Minutes_Click(object sender, RoutedEventArgs e)
        {
            try { Matchers.Interval += TimeSpan.FromMinutes(1); }
            catch (ArgumentOutOfRangeException ex) { }
        }
        private void cmdDown_Minutes_Click(object sender, RoutedEventArgs e)
        {
            try { Matchers.Interval -= TimeSpan.FromMinutes(1); }
            catch (ArgumentOutOfRangeException ex) { }

        }



        public void lstMatchers_Refresh()
        {
            var view = CollectionViewSource.GetDefaultView(Matchers);
            if (view != null)
            {
                view.Refresh();
                // TODO : add something to expand where changed
            }
        }

        private bool _pinned =false;
        public bool Pinned { get { return _pinned; } set { _pinned = value; OnPropertyChanged("Pinned"); } }

        public void SetAppIcon()
        {
                    Icon icn = (Matchers.ChangedState)?Properties.Resources.AppOk : Properties.Resources.App;
                    if (_notify.Icon != icn) _notify.Icon = icn;
        }
    }
}
