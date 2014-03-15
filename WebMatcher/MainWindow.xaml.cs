using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Drawing;
using System.Windows.Controls.Primitives;
using System.Globalization;
using System.Threading;
using System.Diagnostics;

namespace WebMatcher
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>


   public partial class MainWindow : Window, INotifyPropertyChanged
   {
       public event PropertyChangedEventHandler PropertyChanged;

       private Matcher tmpMatcher;
       private String tmpHtml;
        private void changed(String name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        DispatcherTimer _checkerTimer = new DispatcherTimer();

        public ObservableCollection<Matcher> Matchers
        { get { return Matcher.Matchers; } }

        public int MaxNbThreads
        {
            get { return Matcher.MaxNbThreads; }
            set { Matcher.MaxNbThreads = value; changed("MaxNbThreads"); }
        }

        public TimeSpan Interval
        {
            get { return Matcher.Interval; }
            set { Matcher.Interval = value; changed("Interval"); changed("IntervalHours");  }
        }

        public Boolean LoadAtStartup
        {
            get { return Matcher.LoadAtStartup; }
            set {
                Matcher.LoadAtStartup = value;
                changed("LoadAtStartup");
            }
        }

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

        public MainWindow()
        {
            App.SelectCulture(CultureInfo.CurrentCulture.IetfLanguageTag);
            //App.SelectCulture("en-US");

            Matcher.Notify = new System.Windows.Forms.NotifyIcon();
            Matcher.Notify.Icon = WebMatcher.Properties.Resources.App;
            Matcher.Notify.Visible = true;
            Matcher.Notify.Click +=
                delegate(object sender, EventArgs args)
                {
                    Resize();
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                };
            Matcher.Win = this;
            Matcher.LoadMatchers();

            InitializeComponent();
            
            lstMatchers.Items.SortDescriptions.Add(new SortDescription("Name",ListSortDirection.Ascending));

            lstMatchers.Items.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            lstMatchers.Items.Filter = lstMatchers_Filter;

            System.Net.ServicePointManager.DefaultConnectionLimit = 1600;


            new Thread(Matcher.CheckMatchers).Start();

        }

        private bool lstMatchers_Filter(object obj)
        {
            Matcher m = (Matcher)obj;
            return (chkViewAll.IsChecked??false) || m.Changed || m.IsNew;
        }

        private void cmdOpen(object sender, RoutedEventArgs e)
        {
            Matcher w = lstMatchers.SelectedItem as Matcher;
            if (w != null) w.Open();
        }

        private void cmdEdit(object sender, RoutedEventArgs e)
        {
            //Matcher w = lstMatchers.SelectedItem as Matcher;
            //if (w != null) w.Edit();
            DataGridRow row = (DataGridRow)(lstMatchers.ItemContainerGenerator.ContainerFromItem(lstMatchers.SelectedItem));
            if (row!=null)
            {
                 row.DetailsVisibility = Visibility.Visible;
                 tmpMatcher = (lstMatchers.SelectedItem as Matcher).Clone();
                 tmpMatcher.AutoRefresh = false;
                 //tmpMatcher.GetHtml();
                 //tmpMatcher.GetResult();
                 //(RowControl("txtCheck") as TextBox).Text = tmpMatcher.Value ;

           }
        }
        private void cmdDel(object sender, RoutedEventArgs e)
        {
            bool all = false;
            
            if (lstMatchers.SelectedItems.Count>1)
            {
                MessageBoxResult r = MessageBox.Show(string.Format(FindResource("str_AskDeleteMany").ToString(), lstMatchers.SelectedItems.Count), FindResource("str_Confirmation").ToString(), MessageBoxButton.YesNo);
                switch (r)
                {
                    case MessageBoxResult.Yes: all = true; break;
                    case MessageBoxResult.No: return;
                }
            }

            while(lstMatchers.SelectedItems.Count>0)
            {
                Matcher m = lstMatchers.SelectedItem as Matcher;
                if (all || MessageBox.Show(string.Format(FindResource("str_AskDelete").ToString(), m.Name) ,
                 FindResource("str_Confirmation").ToString(), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Matchers.Remove(m);
                    m.delete();
                }
            }
        }

        private void cmdExit(object sender, RoutedEventArgs e)
        {
            this.Close();
            Process.GetCurrentProcess().Kill();
        }
        void ClosingEvent(object sender, CancelEventArgs e)
        {
            Matcher.Notify.Icon = null;
            Matcher.Notify.Dispose();
            Matcher.Notify = null;
        }

        void ResetDataGrid()
        {
            var temp = lstMatchers.ItemsSource;
            lstMatchers.ItemsSource = null;
            lstMatchers.ItemsSource = temp;
        }


        private void cmdAdd_Click(object sender, RoutedEventArgs e)
        {
            Matcher m = new Matcher();
            Matchers.Add(m);
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
            foreach (Matcher m in Matchers) m.ForcedCheck = true;
        }

        private void lstMatchers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow obj = sender as DataGridRow;
            Matcher w = obj.Item as Matcher;
            w.Open();
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



        private void cmdOk_Click(object sender, RoutedEventArgs e)
        {

            (RowControl("txtName") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateSource();
            (RowControl("cboGroup") as ComboBox).GetBindingExpression(ComboBox.TextProperty).UpdateSource();
            (RowControl("txtUrl") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateSource();
            (RowControl("txtExpr") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateSource();
            (RowControl("txtPost") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateSource();
            (RowControl("txtReferer") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateSource();

            Matcher m = lstMatchers.SelectedItem as Matcher;
            if (m != null)
            {
                m.Save();
                lstMatchers_Refresh();
                m.ForcedCheck = true;
            }

            DataGridRow row = (DataGridRow)(lstMatchers.ItemContainerGenerator.ContainerFromItem(lstMatchers.SelectedItem));
            if (row != null)
                row.DetailsVisibility = Visibility.Collapsed;

        }

        private void cmdCancel_Click(object sender, RoutedEventArgs e)
        {//__EVENTTARGET=btnOk
            (RowControl("txtName") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            (RowControl("txtUrl") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            (RowControl("txtExpr") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            (RowControl("txtPost") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            (RowControl("txtReferer") as TextBox).GetBindingExpression(TextBox.TextProperty).UpdateTarget();

            DataGridRow row = (DataGridRow)(lstMatchers.ItemContainerGenerator.ContainerFromItem(lstMatchers.SelectedItem));
            DataGridDetailsPresenter presenter = FindVisualChild<DataGridDetailsPresenter>(row);
            if (row != null)
                row.DetailsVisibility = Visibility.Collapsed;

            Matcher w = lstMatchers.SelectedItem as Matcher;
            if (w.Key == null) Matchers.Remove(w);
        }

        private void txtUrl_TextChanged(object sender, RoutedEventArgs e)
        {
            if (tmpMatcher != null)
            {
                String txt = ((TextBox)sender).Text;
                Thread t=new Thread(
                delegate() {
                    tmpMatcher.URL = txt;
                    tmpMatcher.GetHtml();
                    tmpMatcher.GetResult();
                    String value = tmpMatcher.Value;

                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => (RowControl("txtCheck") as TextBox).Text = value));
                });
                t.Start();
            }
        }
        private void txtExpr_TextChanged(object sender, RoutedEventArgs e)
        {
            if (tmpMatcher!=null)
            {
                if (tmpMatcher.Html == null)
                {
                    txtUrl_TextChanged(sender, e);
                }
                else
                {
                    tmpMatcher.Expression = ((TextBox)sender).Text;
                    tmpMatcher.GetResult();
                    (RowControl("txtCheck") as TextBox).Text = tmpMatcher.Value;
                }
            }
        }
        private void txtPost_TextChanged(object sender, RoutedEventArgs e)
        {
            tmpMatcher.Post = ((TextBox)sender).Text;
            tmpMatcher.GetHtml();
            tmpMatcher.GetResult();
            (RowControl("txtCheck") as TextBox).Text = tmpMatcher.Value;
        }
        private void txtReferer_TextChanged(object sender, RoutedEventArgs e)
        {
            tmpMatcher.Referer = ((TextBox)sender).Text;
            tmpMatcher.GetHtml();
            tmpMatcher.GetResult();
            (RowControl("txtCheck") as TextBox).Text = tmpMatcher.Value;
        }

        private void cmdDetailCheck_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(tmpMatcher.Html);
        }

        private void cmdOptions_Click(object sender, RoutedEventArgs e)
        {
            if (pnlOptions.Visibility == Visibility.Collapsed) pnlOptions.Visibility = Visibility.Visible;
            else pnlOptions.Visibility = Visibility.Collapsed;
        }

        private void cmdUp_MaxNbThreads_Click(object sender, RoutedEventArgs e) { MaxNbThreads++; }
        private void cmdDown_MaxNbThreads_Click(object sender, RoutedEventArgs e) { MaxNbThreads--; }

        private void cmdUp_Hours_Click(object sender, RoutedEventArgs e) {
            try { Interval += TimeSpan.FromHours(1); }
            catch (ArgumentOutOfRangeException ex) { }
        }
        private void cmdDown_Hours_Click(object sender, RoutedEventArgs e) {
            try { Interval -= TimeSpan.FromHours(1); }
            catch (ArgumentOutOfRangeException ex) { }
        }

        public int IntervalHours { get { return (int)Interval.TotalHours; } }

        private void cmdUp_Minutes_Click(object sender, RoutedEventArgs e)
        {
            try { Interval += TimeSpan.FromMinutes(1); }
            catch (ArgumentOutOfRangeException ex) { }
        }
        private void cmdDown_Minutes_Click(object sender, RoutedEventArgs e)
        {
            try { Interval -= TimeSpan.FromMinutes(1); }
            catch (ArgumentOutOfRangeException ex) { }

        }

        private void txtNum_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void txtHours_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void txtMinutes_TextChanged(object sender, TextChangedEventArgs e)
        {

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

        private void chkViewAll_Click(object sender, RoutedEventArgs e)
        {
            lstMatchers_Refresh();
        }

        private bool _pinned =false;
        public bool Pinned { get { return _pinned; } set { _pinned = value; changed("Pinned"); } }
    }
}
