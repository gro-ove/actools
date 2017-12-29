using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Attached;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BetterTextBox : TextBox {
        public BetterTextBox() {
            DefaultStyleKey = typeof(BetterTextBox);
        }

        public static readonly DependencyProperty PlaceholderOpacityProperty = DependencyProperty.Register(nameof(PlaceholderOpacity), typeof(double),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(0.5));

        public double PlaceholderOpacity {
            get => GetValue(PlaceholderOpacityProperty) as double? ?? 0d;
            set => SetValue(PlaceholderOpacityProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(BetterTextBox));

        public string Placeholder {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape:
                    e.Handled = this.RemoveFocus();
                    break;

                case Key.Enter:
                    if (!AcceptsReturn) {
                        e.Handled = this.MoveFocus();
                    }
                    break;

                case Key.Tab:
                    if (Keyboard.Modifiers == ModifierKeys.Shift && this.MoveFocus(FocusNavigationDirection.Previous)) {
                        e.Handled = true;
                    }
                    break;

                case Key.Up:
                case Key.Down:
                    if (GetMode(this) != SpecialMode.None) {
                        var processed = ProcessText(Text, e.Key == Key.Up ? 1d : -1d);
                        if (processed != null) SetTextBoxText(this, processed);
                        e.Handled = true;
                    }
                    break;
            }

            if (!e.Handled) {
                base.OnPreviewKeyDown(e);
            }
        }

        public static INumberInputConverter GetConverter(DependencyObject obj) {
            return (INumberInputConverter)obj.GetValue(ConverterProperty);
        }

        public static void SetConverter(DependencyObject obj, INumberInputConverter value) {
            obj.SetValue(ConverterProperty, value);
        }

        public static readonly DependencyProperty ConverterProperty = DependencyProperty.RegisterAttached("Converter", typeof(INumberInputConverter),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));


        public static SpecialMode GetMode(DependencyObject obj) {
            return obj.GetValue(ModeProperty) as SpecialMode? ?? default(SpecialMode);
        }

        public static void SetMode(DependencyObject obj, SpecialMode value) {
            obj.SetValue(ModeProperty, value);
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached("Mode", typeof(SpecialMode),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(SpecialMode.None, FrameworkPropertyMetadataOptions.Inherits));

        public static string GetModeLabel(DependencyObject obj) {
            return (string)obj.GetValue(ModeLabelProperty);
        }

        public static void SetModeLabel(DependencyObject obj, string value) {
            obj.SetValue(ModeLabelProperty, value);
        }

        public static readonly DependencyProperty ModeLabelProperty = DependencyProperty.RegisterAttached("ModeLabel", typeof(string),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));


        public static double GetModeLabelValue(DependencyObject obj) {
            return obj.GetValue(ModeLabelValueProperty) as double? ?? 0d;
        }

        public static void SetModeLabelValue(DependencyObject obj, double value) {
            obj.SetValue(ModeLabelValueProperty, value);
        }

        public static readonly DependencyProperty ModeLabelValueProperty = DependencyProperty.RegisterAttached("ModeLabelValue", typeof(double),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.Inherits));


        public static double GetMinimum(DependencyObject obj) {
            return obj.GetValue(MinimumProperty) as double? ?? 0d;
        }

        public static void SetMinimum(DependencyObject obj, double value) {
            obj.SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.RegisterAttached("Minimum", typeof(double),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(double.NegativeInfinity, FrameworkPropertyMetadataOptions.Inherits));


        public static double GetMaximum(DependencyObject obj) {
            return obj.GetValue(MaximumProperty) as double? ?? 0d;
        }

        public static void SetMaximum(DependencyObject obj, double value) {
            obj.SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.RegisterAttached("Maximum", typeof(double),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.Inherits));


        internal static void SetTextBoxText(TextBox textBox, string text) {
            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;
            textBox.Text = text;
            textBox.SelectionStart = selectionStart;
            textBox.SelectionLength = selectionLength;
        }

        private int GetLabelValue() {
            switch (GetMode(this)) {
#pragma warning disable 612
                case SpecialMode.IntegerOrMinusOneLabel:
#pragma warning restore 612
                    return -1;
#pragma warning disable 612
                case SpecialMode.IntegerOrZeroLabel:
#pragma warning restore 612
                    return 0;
                case SpecialMode.IntegerOrLabel:
                    return (int)Math.Round(GetModeLabelValue(this));
                default:
                    return 0;
            }
        }

        internal string ProcessText(string text, double delta) {
            var mode = GetMode(this);
            var converter = GetConverter(this) ?? NumberInputConverter.Default;
            var minValue = GetMinimum(this);
            var maxValue = GetMaximum(this);

            switch (mode) {
                case SpecialMode.Number: {
                    var valueNullable = converter.TryToParse(text);
                    if (!valueNullable.HasValue) return null;
                    var value = valueNullable.Value;

                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                        delta *= 0.1;
                    }

                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                        delta *= 10.0;
                    }

                    if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                        delta *= 2.0;
                    }

                    value += delta;
                    value = Math.Max(Math.Min(value, maxValue), minValue);
                    return converter.BackToString(value) ?? FlexibleParser.ReplaceDouble(text, value);
                }

                case SpecialMode.Integer:
#pragma warning disable 612
                case SpecialMode.Positive: {
#pragma warning restore 612
                    var valueNullable = converter.TryToParse(text);
                    if (!valueNullable.HasValue) return null;
                    var value = Math.Round(valueNullable.Value);

                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                        delta *= 10.0;
                    }

                    if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                        delta *= 2.0;
                    }

                    value = (int)Math.Max(Math.Min((int)(value + delta), maxValue), minValue);
#pragma warning disable 612
                    if (mode == SpecialMode.Positive && value < 1) {
#pragma warning restore 612
                        value = 1;
                    }

                    return converter.BackToString(value) ?? FlexibleParser.ReplaceDouble(text, value);
                }

                case SpecialMode.IntegerOrLabel:
#pragma warning disable 612
                case SpecialMode.IntegerOrMinusOneLabel:
                case SpecialMode.IntegerOrZeroLabel: {
#pragma warning restore 612
                    var valueNullable = converter.TryToParse(text);
                    var skip = !valueNullable.HasValue;
                    var value = valueNullable ?? GetLabelValue();

                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                        delta *= 10.0;
                    }

                    if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                        delta *= 2.0;
                    }

                    value = (int)Math.Max(Math.Min((int)(value + delta), maxValue), minValue);
                    return converter.BackToString(value) ?? (value == GetLabelValue() && GetModeLabel(this) != null ? GetModeLabel(this)
                            : skip ? converter.BackToString(value) : FlexibleParser.ReplaceDouble(text, value));
                }

                case SpecialMode.Time: {
                    var splitted = text.Split(':');
                    int totalSeconds;

                    switch (splitted.Length) {
                        case 2: {
                            if (!FlexibleParser.TryParseInt(splitted[0], out var hours) || !FlexibleParser.TryParseInt(splitted[1], out var minutes)) {
                                return null;
                            }

                            totalSeconds = (splitted[0].StartsWith(@"-") ? -1 : 1) * (Math.Abs(hours) * 60 + Math.Abs(minutes)) * 60;
                            break;
                        }
                        case 3: {
                            if (!FlexibleParser.TryParseInt(splitted[0], out var hours) || !FlexibleParser.TryParseInt(splitted[1], out var minutes) ||
                                    !FlexibleParser.TryParseInt(splitted[2], out var seconds)) {
                                return null;
                            }

                            totalSeconds = (splitted[0].StartsWith(@"-") ? -1 : 1) * (Math.Abs(hours) * 60 + Math.Abs(minutes)) * 60 + Math.Abs(seconds);
                            break;
                        }
                        default:
                            return null;
                    }

                    if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                        delta *= 60;
                    }

                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
                        delta *= 60;
                    }

                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                        delta *= 15;
                    }

                    if (double.IsNegativeInfinity(minValue)) {
                        minValue = 0d;
                    }

                    totalSeconds += (int)delta;
                    totalSeconds = (int)Math.Max(Math.Min(totalSeconds, maxValue), minValue);

                    var t = Math.Abs(totalSeconds);
                    return splitted.Length == 2
                            ? $@"{(totalSeconds < 0 ? @"-" : "")}{t / 3600:D2}:{t / 60 % 60:D2}"
                            : $@"{(totalSeconds < 0 ? @"-" : "")}{t / 3600:D2}:{t / 60 % 60:D2}:{t % 60:D2}";
                }

                case SpecialMode.Version: {
                    var splitted = text.Split('.');
                    var index = splitted.Length - 1;

                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                        index--;
                    }

                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                        index--;
                    }

                    if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                        index--;
                    }

                    if (index < 0) {
                        index = 0;
                    }

                    if (FlexibleParser.TryParseInt(splitted[index], out var value)) {
                        splitted[index] = FlexibleParser.ReplaceDouble(splitted[index], value + delta);
                    }

                    return string.Join(@".", splitted);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        // sorry about it, but I don’t think I can handle another library only for number parsing
        internal static class FlexibleParser {
            private static Regex _parseDouble, _parseInt;

            public static bool TryParseDouble(string s, out double value) {
                if (_parseDouble == null) {
                    _parseDouble = new Regex(@"-? *\d+([\.,]\d*)?");
                }

                if (s != null) {
                    var match = _parseDouble.Match(s);
                    if (match.Success) {
                        return double.TryParse(match.Value.Replace(',', '.').Replace(@" ", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
                    }
                }

                value = 0.0;
                return false;
            }

            public static bool TryParseInt(string s, out int value) {
                if (_parseInt == null) {
                    _parseInt = new Regex(@"-? *\d+");
                }

                if (s != null) {
                    var match = _parseInt.Match(s);
                    if (match.Success) {
                        return int.TryParse(match.Value.Replace(@" ", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
                    }
                }

                value = 0;
                return false;
            }

            public static string ReplaceDouble(string s, double value) {
                if (_parseDouble == null) {
                    _parseDouble = new Regex(@"-? *\d+([\.,]\d*)?");
                }

                var match = _parseDouble.Match(s);
                if (!match.Success) return s;

                return s.Substring(0, match.Index) + value.ToString(CultureInfo.InvariantCulture) + s.Substring(match.Index + match.Length);
            }
        }
    }
}