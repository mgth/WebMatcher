using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace WebMatcher
{
    /// <summary>
    /// Logique d'interaction pour MatcherUI.xaml
    /// </summary>
    public partial class MatcherUI : UserControl
    {
        public MatcherUI()
        {
            InitializeComponent();
        }



        private Matcher Matcher => DataContext as Matcher;

        private void cmdOk_Click(object sender, RoutedEventArgs e)
        {
            if (Matcher != null)
            {
                Matcher.Expanded = false;
                Matcher.Save(true);
            }
        }

        private void cmdCancel_Click(object sender, RoutedEventArgs e)
        {
            if (Matcher != null)
            {
                if (Matcher.IsNew)
                {
                    Matcher.Expanded = false;
                    Matcher.Delete();
                }
                else
                {
                    Matcher.Load();
                    Matcher.Expanded = false;
                }
            }
        }

        private void cmdDetailCheck_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(Matcher.Html);
        }

        private void cmdOpen(object sender, RoutedEventArgs e)
        {
            if (Matcher != null) Matcher.Open();
        }

        private void cmdDel(object sender, RoutedEventArgs e)
        {
            if(Matcher != null)
            {
                if (MessageBox.Show(string.Format(FindResource("str_AskDelete").ToString(), Matcher.Name),
                 FindResource("str_Confirmation").ToString(), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Matcher.Delete();
                }
            }

            /*            bool all = false;

                        if (LstMatchers.SelectedItems.Count > 1)
                        {
                            MessageBoxResult r = MessageBox.Show(string.Format(FindResource("str_AskDeleteMany").ToString(), LstMatchers.SelectedItems.Count), FindResource("str_Confirmation").ToString(), MessageBoxButton.YesNo);
                            switch (r)
                            {
                                case MessageBoxResult.Yes: all = true; break;
                                case MessageBoxResult.No: return;
                            }
                        }

                        while (LstMatchers.SelectedItems.Count > 0)
                        {
                            Matcher m = LstMatchers.SelectedItem as Matcher;
                            if (all || MessageBox.Show(string.Format(FindResource("str_AskDelete").ToString(), m.Name),
                             FindResource("str_Confirmation").ToString(), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                m.Group.Remove(m);
                                m.delete();
                            }
                        }*/
        }

        private void cmdOpenDblClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (Matcher != null)
                {
                    Matcher.Open();
                }
            }
        }


        private void cmdEdit(object sender, RoutedEventArgs e)
        {
            Matcher.Expanded = true;
        }

        private LossyThread _expressionChangedThread = new LossyThread();

        private void txtExpr_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Matcher != null)
            {
                Matcher m = Matcher;
                string expr = txtExpr.Text;

                _expressionChangedThread.Add(() =>
                    {
                        string txt = m.Result(expr);

                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            txtCheck.Text = txt;
                        }));
                    }
                    );
            }
        }

        private void cmdCheck(object sender, RoutedEventArgs e)
        {
            Matcher.Enqueue();
        }
    }
}

