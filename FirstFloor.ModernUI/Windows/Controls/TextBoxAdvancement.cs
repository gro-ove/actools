using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Windows.Controls {
    public static class TextBoxAdvancement {
        // sorry about it, but I don't think I can handle another library only for number parsing
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

        public enum SpecialMode {
            None, Number, Integer, IntegerOrSkip, Positive, Time
        }

        public static SpecialMode GetSpecialMode(DependencyObject obj) {
            return (SpecialMode)obj.GetValue(SpecialModeProperty);
        }

        public static void SetSpecialMode(DependencyObject obj, SpecialMode value) {
            obj.SetValue(SpecialModeProperty, value);
        }

        public static readonly DependencyProperty SpecialModeProperty = DependencyProperty.RegisterAttached("SpecialMode",
            typeof(SpecialMode), typeof(TextBoxAdvancement), new UIPropertyMetadata(OnSpecialModePropertyChanged));

        static void OnSpecialModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as TextBox;
            if (element == null) return;

            if (e.NewValue is SpecialMode && (SpecialMode)e.NewValue != SpecialMode.None) {
                element.PreviewKeyDown += Element_KeyDown;
            } else {
                element.PreviewKeyDown -= Element_KeyDown;
            }
        }

        private static void SetTextBoxText(TextBox textBox, string text) {
            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;
            textBox.Text = text;
            textBox.SelectionStart = selectionStart;
            textBox.SelectionLength = selectionLength;
        }

        private static string ProcessText(SpecialMode mode, string text, double delta) {
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

                    return FlexibleParser.ReplaceDouble(text, value + delta);
                }

                case SpecialMode.Integer:
                case SpecialMode.Positive: {
                    int value;
                    if (!FlexibleParser.TryParseInt(text, out value)) return null;

                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                        delta *= 10.0;
                    }

                    if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                        delta *= 2.0;
                    }

                    value = (int)(value + delta);
                    if (mode == SpecialMode.Positive && value < 1) {
                        value = 1;
                    }

                    return FlexibleParser.ReplaceDouble(text, value);
                }

                case SpecialMode.IntegerOrSkip: {
                        var skip = string.Equals(text, "Skip", StringComparison.OrdinalIgnoreCase);

                        int value;
                        if (skip) {
                            value = 0;
                        } else if (!FlexibleParser.TryParseInt(text, out value)) return null;

                        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                            delta *= 10.0;
                        }

                        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                            delta *= 2.0;
                        }

                        value = (int)(value + delta);
                        if (mode == SpecialMode.Positive && value < 1) {
                            value = 1;
                        }

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
                    return $"{totalMinutes / 60:D}:{totalMinutes % 60:D}";
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static void Element_KeyDown(object sender, KeyEventArgs e) {
            if (!e.Key.Equals(Key.Up) && !e.Key.Equals(Key.Down)) return;

            var element = sender as TextBox;
            if (element == null) return;

            var mode = GetSpecialMode(element);
            var delta = e.Key == Key.Up ? 1.0 : -1.0;

            var processed = ProcessText(mode, element.Text, delta);
            if (processed != null) {
                SetTextBoxText(element, processed);
            }

            e.Handled = true;
        }
    }
}
