using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Controls.Converters {
    [ValueConversion(typeof(int), typeof(Brush))]
    public class PlaceToBrushConverter : DependencyObject, IValueConverter {
        public static readonly DependencyProperty FirstPlaceBrushProperty = DependencyProperty.Register(nameof(FirstPlaceBrush), typeof(Brush),
                typeof(PlaceToBrushConverter), new PropertyMetadata(null, (o, e) => ((PlaceToBrushConverter)o)._firstPlaceBrush = (Brush)e.NewValue));
        private Brush _firstPlaceBrush;
        public Brush FirstPlaceBrush {
            get => _firstPlaceBrush;
            set => SetValue(FirstPlaceBrushProperty, value);
        }

        public static readonly DependencyProperty SecondPlaceBrushProperty = DependencyProperty.Register(nameof(SecondPlaceBrush), typeof(Brush),
                typeof(PlaceToBrushConverter), new PropertyMetadata(null, (o, e) => ((PlaceToBrushConverter)o)._secondPlaceBrush = (Brush)e.NewValue));
        private Brush _secondPlaceBrush;
        public Brush SecondPlaceBrush {
            get => _secondPlaceBrush;
            set => SetValue(SecondPlaceBrushProperty, value);
        }

        public static readonly DependencyProperty ThirdPlaceBrushProperty = DependencyProperty.Register(nameof(ThirdPlaceBrush), typeof(Brush),
                typeof(PlaceToBrushConverter), new PropertyMetadata(null, (o, e) => ((PlaceToBrushConverter)o)._thirdPlaceBrush = (Brush)e.NewValue));
        private Brush _thirdPlaceBrush;
        public Brush ThirdPlaceBrush {
            get => _thirdPlaceBrush;
            set => SetValue(ThirdPlaceBrushProperty, value);
        }

        public static readonly DependencyProperty ForthPlaceBrushProperty = DependencyProperty.Register(nameof(ForthPlaceBrush), typeof(Brush),
                typeof(PlaceToBrushConverter), new PropertyMetadata(null, (o, e) => ((PlaceToBrushConverter)o)._forthPlaceBrush = (Brush)e.NewValue));
        private Brush _forthPlaceBrush;
        public Brush ForthPlaceBrush {
            get => _forthPlaceBrush;
            set => SetValue(ForthPlaceBrushProperty, value);
        }

        public static readonly DependencyProperty DefaultBrushProperty = DependencyProperty.Register(nameof(DefaultBrush), typeof(Brush),
                typeof(PlaceToBrushConverter), new PropertyMetadata(null, (o, e) => ((PlaceToBrushConverter)o)._defaultBrush = (Brush)e.NewValue));
        private Brush _defaultBrush;
        public Brush DefaultBrush {
            get => _defaultBrush;
            set => SetValue(DefaultBrushProperty, value);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            switch (value.AsInt()) {
                case 1:
                    return FirstPlaceBrush;
                case 2:
                    return SecondPlaceBrush;
                case 3:
                    return ThirdPlaceBrush;
                case 4:
                    return ForthPlaceBrush ?? DefaultBrush;
                default:
                    return DefaultBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }
}