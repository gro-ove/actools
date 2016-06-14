using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AcManager.Controls.QuickSwitches {
    public class QuickSwitchComboBox : ComboBox {
        static QuickSwitchComboBox() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(QuickSwitchComboBox), new FrameworkPropertyMetadata(typeof(QuickSwitchComboBox)));
        }

        public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(nameof(IconData), typeof(Geometry),
                typeof(QuickSwitchComboBox));

        public Geometry IconData {
            get { return (Geometry)GetValue(IconDataProperty); }
            set { SetValue(IconDataProperty, value); }
        }
    }
}