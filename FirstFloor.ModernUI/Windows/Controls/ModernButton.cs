using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ModernButton : Button {
        public static readonly DependencyProperty EllipseDiameterProperty = DependencyProperty.Register("EllipseDiameter", typeof(double), typeof(ModernButton),
                new PropertyMetadata(22D));

        public static readonly DependencyProperty EllipseStrokeThicknessProperty = DependencyProperty.Register("EllipseStrokeThickness", typeof(double),
                typeof(ModernButton), new PropertyMetadata(1D));

        public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register("IconData", typeof(Geometry), typeof(ModernButton));

        public static readonly DependencyProperty IconHeightProperty = DependencyProperty.Register("IconHeight", typeof(double), typeof(ModernButton),
                new PropertyMetadata(12D));

        public static readonly DependencyProperty IconWidthProperty = DependencyProperty.Register("IconWidth", typeof(double), typeof(ModernButton),
                new PropertyMetadata(12D));

        public ModernButton() {
            this.DefaultStyleKey = typeof(ModernButton);
        }

        public double EllipseDiameter {
            get => GetValue(EllipseDiameterProperty) as double? ?? default(double);
            set => SetValue(EllipseDiameterProperty, value);
        }

        public double EllipseStrokeThickness {
            get => GetValue(EllipseStrokeThicknessProperty) as double? ?? default(double);
            set => SetValue(EllipseStrokeThicknessProperty, value);
        }

        public Geometry IconData {
            get => (Geometry)GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }

        public double IconHeight {
            get => GetValue(IconHeightProperty) as double? ?? default(double);
            set => SetValue(IconHeightProperty, value);
        }

        public double IconWidth {
            get => GetValue(IconWidthProperty) as double? ?? default(double);
            set => SetValue(IconWidthProperty, value);
        }
    }
}
