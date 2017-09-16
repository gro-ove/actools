using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AcManager.Controls {
    public class StatEntry : Control {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(StatEntry));

        public string Title {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string),
                typeof(StatEntry));

        public string Value {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueUnitsProperty = DependencyProperty.Register(nameof(ValueUnits), typeof(string),
                typeof(StatEntry));

        public string ValueUnits {
            get => (string)GetValue(ValueUnitsProperty);
            set => SetValue(ValueUnitsProperty, value);
        }

        public static readonly DependencyProperty CarIdProperty = DependencyProperty.Register(nameof(CarId), typeof(string),
                typeof(StatEntry));

        public string CarId {
            get => (string)GetValue(CarIdProperty);
            set => SetValue(CarIdProperty, value);
        }

        public static readonly DependencyProperty TrackIdProperty = DependencyProperty.Register(nameof(TrackId), typeof(string),
                typeof(StatEntry));

        public string TrackId {
            get => (string)GetValue(TrackIdProperty);
            set => SetValue(TrackIdProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(Geometry),
                typeof(StatEntry));

        public Geometry Icon {
            get => (Geometry)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }
}