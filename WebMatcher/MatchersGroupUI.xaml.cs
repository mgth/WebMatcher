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
        }

        private void DockPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Expander.Visibility == Visibility.Collapsed)
                Expander.Visibility = Visibility.Visible;
            else
                Expander.Visibility = Visibility.Collapsed;
        }
    }
    //: 
    public class BollToVisibilityConverter : System.Windows.Markup.MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool bValue = (bool)value;
            if (bValue)
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;

            if (visibility == Visibility.Visible)
                return true;
            else
                return false;
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
