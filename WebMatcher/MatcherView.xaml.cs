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
    /// Logique d'interaction pour MatcherView.xaml
    /// </summary>
    public partial class MatcherView : UserControl
    {
        public MatcherView()
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
            try
            {
                Clipboard.SetText(Matcher.Html);
            }
            catch(Exception ex)
            {
                
            }
        }

        private void CmdOpen(object sender, RoutedEventArgs e)
        {
            Matcher?.Open();
        }

        private void CmdDel(object sender, RoutedEventArgs e)
        {
            if(Matcher != null)
            {
                if (MessageBox.Show(string.Format(FindResource("str_AskDelete").ToString(), Matcher.Name),
                 FindResource("str_Confirmation").ToString(), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Matcher.Delete();
                }
            }
        }

        private void CmdOpenDblClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;

            Matcher?.Open();
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

