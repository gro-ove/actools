using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Attached;

namespace FirstFloor.ModernUI.Windows.Controls {
    public enum SpecialMode {
        None, Number, Integer, IntegerOrZeroLabel, Time, Version,

        [Obsolete]
        Positive
    }

    public class BetterComboBox : ComboBox {
        static BetterComboBox() {
            IsEditableProperty.OverrideMetadata(typeof(BetterComboBox), new FrameworkPropertyMetadata(true));
        }

        public BetterComboBox() {
            DefaultStyleKey = typeof(BetterComboBox);
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string),
                typeof(BetterComboBox));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape:
                    if (FocusAdvancement.RemoveFocus(this)) {
                        e.Handled = true;
                    }
                    break;

                case Key.Enter:
                    if (FocusAdvancement.MoveFocus(this)) {
                        e.Handled = true;
                    }
                    break;
            }

            if (!e.Handled) {
                base.OnPreviewKeyDown(e);
            }
        }
    }

    public class BetterTextBox : TextBox {
        public BetterTextBox() {
            DefaultStyleKey = typeof(BetterTextBox);
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string),
                typeof(BetterTextBox));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(SpecialMode),
                typeof(BetterTextBox));

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            if (AcceptsReturn) {
                switch (e.Key) {
                    case Key.Escape:
                        if (FocusAdvancement.RemoveFocus(this)) {
                            e.Handled = true;
                        }
                        break;

                    case Key.Enter:
                        if (FocusAdvancement.MoveFocus(this)) {
                            e.Handled = true;
                        }
                        break;

                    case Key.Up:
                    case Key.Down:
                        if (Mode != SpecialMode.None) {
                            var processed = ProcessText(Mode, Text, e.Key == Key.Up ? 1d : -1d, Minimum, Maximum);
                            if (processed != null) SetTextBoxText(this, processed);
                            e.Handled = true;
                        }
                        break;
                }
            }

            if (!e.Handled) {
                base.OnPreviewKeyDown(e);
            }
        }

        public SpecialMode Mode {
            get { return (SpecialMode)GetValue(ModeProperty); }
            set { SetValue(ModeProperty, value); }
        }

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double),
                typeof(BetterTextBox), new PropertyMetadata(double.NegativeInfinity));

        public double Minimum {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double),
                typeof(BetterTextBox), new PropertyMetadata(double.PositiveInfinity));

        public double Maximum {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public static void SetTextBoxText(TextBox textBox, string text) {
            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;
            textBox.Text = text;
            textBox.SelectionStart = selectionStart;
            textBox.SelectionLength = selectionLength;
        }

        public static string ProcessText(SpecialMode mode, string text, double delta, double minValue, double maxValue) {
            switch (mode) {
                case SpecialMode.Number: {
                        double value;
                        if (!FlexibleParser.TryParseDouble(text, out value)) return null;

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
                        return FlexibleParser.ReplaceDouble(text, value);
                    }

                case SpecialMode.Integer:
#pragma warning disable 612
                case SpecialMode.Positive: {
#pragma warning restore 612
                    int value;
                    if (!FlexibleParser.TryParseInt(text, out value)) return null;

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
                    return FlexibleParser.ReplaceDouble(text, value);
                    }

                case SpecialMode.IntegerOrZeroLabel: {
                        int value;
                        var skip = !FlexibleParser.TryParseInt(text, out value);

                        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                            delta *= 10.0;
                        }

                        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                            delta *= 2.0;
                        }
                        
                        value = (int)Math.Max(Math.Min((int)(value + delta), maxValue), minValue);
                        return skip ? value.ToString(CultureInfo.InvariantCulture) : FlexibleParser.ReplaceDouble(text, value);
                    }

                case SpecialMode.Time: {
                        var splitted = text.Split(':');
                        int hours, minutes;

                        if (splitted.Length != 2 || !FlexibleParser.TryParseInt(splitted[0], out hours) ||
                                !FlexibleParser.TryParseInt(splitted[1], out minutes)) return null;

                        var totalMinutes = hours * 60 + minutes;

                        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                            delta *= 0.5;
                        }

                        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                            delta *= 6.0;
                        }

                        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                            delta *= 2.0;
                        }

                        totalMinutes += (int)(delta * 10);
                        totalMinutes = (int)Math.Max(Math.Min(totalMinutes, maxValue), minValue);
                        return $"{totalMinutes / 60:D}:{totalMinutes % 60:D}";
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

                        int value;
                        if (FlexibleParser.TryParseInt(splitted[index], out value)) {
                            splitted[index] = FlexibleParser.ReplaceDouble(splitted[index], value);
                        }

                        return string.Join(".", splitted);
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        // sorry about it, but I don’t think I can handle another library only for number parsing
        private static class FlexibleParser {
            private static Regex _parseDouble, _parseInt;

            public static bool TryParseDouble(string s, out double value) {
                if (_parseDouble == null) {
                    _parseDouble = new Regex(@"-? *\d+([\.,]\d*)?");
                }

                if (s != null) {
                    var match = _parseDouble.Match(s);
                    if (match.Success) {
                        return double.TryParse(match.Value.Replace(',', '.').Replace(" ", ""), NumberStyles.Any,
                                               CultureInfo.InvariantCulture, out value);
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
                        return int.TryParse(match.Value.Replace(" ", ""), NumberStyles.Any,
                                            CultureInfo.InvariantCulture, out value);
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

                return s.Substring(0, match.Index) + value.ToString(CultureInfo.InvariantCulture) +
                       s.Substring(match.Index + match.Length);
            }
        }
    }
}