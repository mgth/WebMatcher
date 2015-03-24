using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace WebMatcher
{
    /// <summary>
    /// Logique d'interaction pour MatchersGroupUI.xaml
    /// </summary>
    public partial class MatchersGroupUI : UserControl
    {
        public MatchersGroupUI()
        {
            InitializeComponent();

            ListBox.Visibility = Visibility.Collapsed;
        }

        private void DockPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ListBox.Visibility == Visibility.Collapsed)
                ListBox.Visibility = Visibility.Visible;
            else
                ListBox.Visibility = Visibility.Collapsed;
        }
    }
}
