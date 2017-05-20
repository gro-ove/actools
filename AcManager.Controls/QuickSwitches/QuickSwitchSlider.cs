using System.Windows;
using System.Windows.Media;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls.QuickSwitches {
    public class QuickSwitchSlider : RoundSlider {
        static QuickSwitchSlider() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(QuickSwitchSlider), new FrameworkPropertyMetadata(typeof(QuickSwitchSlider)));
        }
        
        public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(nameof(IconData), typeof(Geometry),
                typeof(QuickSwitchSlider));

        public Geometry IconData {
            get { return (Geometry)GetValue(IconDataProperty); }
            set { SetValue(IconDataProperty, value); }
        }

        public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(nameof(DisplayValue), typeof(string),
                typeof(QuickSwitchSlider));

        public string DisplayValue {
            get { return (string)GetValue(DisplayValueProperty); }
            set { SetValue(DisplayValueProperty, value); }
        }
    }
}