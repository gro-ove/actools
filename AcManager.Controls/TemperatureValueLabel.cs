using System;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.Converters;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Controls {
    public class TemperatureBlock : TextBlock {
        static TemperatureBlock() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TemperatureBlock), new FrameworkPropertyMetadata(typeof(TemperatureBlock)));
        }

        public static readonly DependencyProperty SpaceProperty = DependencyProperty.Register(nameof(Space), typeof(string),
                typeof(TemperatureBlock), new FrameworkPropertyMetadata("\u202A", OnModeChanged));

        public string Space {
            get => (string)GetValue(SpaceProperty);
            set => SetValue(SpaceProperty, value);
        }

        public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(nameof(Prefix), typeof(string),
                typeof(TemperatureBlock), new FrameworkPropertyMetadata("", OnModeChanged));

        public string Prefix {
            get => (string)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public static readonly DependencyProperty RoundingProperty = DependencyProperty.Register(nameof(Rounding), typeof(double),
                typeof(TemperatureBlock), new FrameworkPropertyMetadata(1d));

        public double Rounding {
            get => GetValue(RoundingProperty) as double? ?? 0d;
            set => SetValue(RoundingProperty, value);
        }

        public new static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(double),
                typeof(TemperatureBlock), new PropertyMetadata(0d, (o, e) => {
                    ((TemperatureBlock)o)._text = (double)e.NewValue;
                    ((TemperatureBlock)o).Update();
                }));

        private double _text;

        public new double Text {
            get => _text;
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(TemperatureUnitMode),
                typeof(TemperatureBlock), new PropertyMetadata(OnModeChanged));

        public TemperatureUnitMode Mode {
            get => GetValue(ModeProperty) as TemperatureUnitMode? ?? default;
            set => SetValue(ModeProperty, value);
        }

        private static void OnModeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((TemperatureBlock)o).Update();
        }

        private void Update() {
            var space = Space;
            var rounding = Rounding;
            switch (Mode) {
                case TemperatureUnitMode.Celsius:
                    base.Text = $"{Prefix}{Text.Round(rounding)}{space}{ControlsStrings.Common_CelsiusPostfix}";
                    break;
                case TemperatureUnitMode.Fahrenheit:
                    base.Text = $"{Prefix}{TemperatureToFahrenheitConverter.ToFahrenheit(Text).Round(rounding)}{space}{ControlsStrings.Common_FahrenheitPostfix}";
                    break;
                case TemperatureUnitMode.Both:
                    base.Text = $"{Prefix}{Text.Round(rounding)}{space}{ControlsStrings.Common_CelsiusPostfix}, {TemperatureToFahrenheitConverter.ToFahrenheit(Text).Round(rounding)}{space}{ControlsStrings.Common_FahrenheitPostfix}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public class TemperatureValueLabel : ValueLabel {
        static TemperatureValueLabel() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TemperatureValueLabel), new FrameworkPropertyMetadata(typeof(TemperatureValueLabel)));
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(TemperatureUnitMode),
                typeof(TemperatureValueLabel));

        public TemperatureUnitMode Mode {
            get => GetValue(ModeProperty) as TemperatureUnitMode? ?? default;
            set => SetValue(ModeProperty, value);
        }

        public static readonly DependencyProperty FahrenheitPostfixProperty = DependencyProperty.Register(nameof(FahrenheitPostfix), typeof(string),
                typeof(TemperatureValueLabel));

        public string FahrenheitPostfix {
            get => (string)GetValue(FahrenheitPostfixProperty);
            set => SetValue(FahrenheitPostfixProperty, value);
        }
    }

    public class DoubleTemperatureValueLabel : DoubleValueLabel {
        static DoubleTemperatureValueLabel() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DoubleTemperatureValueLabel), new FrameworkPropertyMetadata(typeof(DoubleTemperatureValueLabel)));
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(TemperatureUnitMode),
                typeof(DoubleTemperatureValueLabel));

        public TemperatureUnitMode Mode {
            get => GetValue(ModeProperty) as TemperatureUnitMode? ?? default;
            set => SetValue(ModeProperty, value);
        }

        public static readonly DependencyProperty FahrenheitPostfixProperty = DependencyProperty.Register(nameof(FahrenheitPostfix), typeof(string),
                typeof(DoubleTemperatureValueLabel));

        public string FahrenheitPostfix {
            get => (string)GetValue(FahrenheitPostfixProperty);
            set => SetValue(FahrenheitPostfixProperty, value);
        }
    }
}
