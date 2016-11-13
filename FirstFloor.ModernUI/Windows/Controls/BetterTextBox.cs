using System;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;

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
            PreviewMouseWheel += ComboBox_PreviewMouseWheel;
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string),
                typeof(BetterComboBox));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (IsDropDownOpen) return;

            e.Handled = true;
            (Parent as UIElement)?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            });
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape:
                    e.Handled = FocusAdvancement.RemoveFocus(this);
                    break;

                case Key.Enter:
                    e.Handled = FocusAdvancement.MoveFocus(this);
                    break;
            }

            if (!e.Handled) {
                base.OnPreviewKeyDown(e);
            }
        }

        public new static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object),
                typeof(BetterComboBox), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        [CanBeNull]
        public new object SelectedItem {
            get { return GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        private static void OnSelectedItemChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterComboBox)o).OnSelectedItemChanged(e.NewValue);
        }

        private void OnSelectedItemChanged(object newValue) {
            SetValue(Selector.SelectedItemProperty, newValue ?? NullValue);
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            base.OnSelectionChanged(e);
            SelectedItem = ReferenceEquals(base.SelectedItem, NullValue) ? null : base.SelectedItem;
        }

        public static readonly object NullValue = new object();

        private class NullPrefixedCollection : BetterObservableCollection<object> {
            private readonly IEnumerable _collection;

            public NullPrefixedCollection(IEnumerable collection)
                    // ReSharper disable once PossibleMultipleEnumeration
                    : base(new [] { NullValue }.Concat(collection.OfType<object>())) {
                var o = collection as INotifyCollectionChanged;
                if (o != null) {
                    // ReSharper disable once PossibleMultipleEnumeration
                    _collection = collection;
                    WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(o, nameof(o.CollectionChanged),
                            OnCollectionChanged);
                }
            }

            private void Rebuild() {
                ReplaceEverythingBy(new [] { NullValue }.Concat(_collection.OfType<object>()));
            }

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
                switch (e.Action) {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems?.Count == 1 && e.NewStartingIndex + 1 > 0) {
                            if (e.NewStartingIndex + 1 >= Count) {
                                Add(e.NewItems[0]);
                            } else {
                                Insert(e.NewStartingIndex + 1, e.NewItems[0]);
                            }
                        } else {
                            Rebuild();
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems != null) {
                            foreach (var oldItem in e.OldItems) {
                                Remove(oldItem);
                            }
                        } else {
                            Rebuild();
                        }
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        if (e.OldItems?.Count == 1 && e.NewItems?.Count == 1) {
                            var i = IndexOf(e.OldItems[0]);
                            if (i != -1) {
                                this[i] = e.NewItems[0];
                            } else {
                                Rebuild();
                            }
                        } else {
                            Rebuild();
                        }
                        break;
                    case NotifyCollectionChangedAction.Move:
                        Move(e.OldStartingIndex + 1, e.NewStartingIndex + 1);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        Rebuild();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void UpdateItemsSource() {
            var items = ItemsSource;
            var nullable = Nullable;
            SetValue(ItemsControl.ItemsSourceProperty,
                    nullable ? items == null ? (IEnumerable)new object[] { null } : new NullPrefixedCollection(items) : ItemsSource);
            if (nullable && base.SelectedItem == null) {
                base.SelectedItem = NullValue;
            }
        }

        public new static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable),
                typeof(BetterComboBox), new PropertyMetadata(OnItemsSourceChanged));

        public new IEnumerable ItemsSource {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        private static void OnItemsSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterComboBox)o).UpdateItemsSource();
        }

        public static readonly DependencyProperty NullableProperty = DependencyProperty.Register(nameof(Nullable), typeof(bool),
                typeof(BetterComboBox), new PropertyMetadata(OnNullableChanged));

        public bool Nullable {
            get { return (bool)GetValue(NullableProperty); }
            set { SetValue(NullableProperty, value); }
        }

        private static void OnNullableChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterComboBox)o).UpdateItemsSource();
        }
    }

    public class BetterTextBox : TextBox {
        public BetterTextBox() {
            DefaultStyleKey = typeof(BetterTextBox);
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(BetterTextBox));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(SpecialMode), typeof(BetterTextBox));

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape:
                    e.Handled = FocusAdvancement.RemoveFocus(this);
                    break;

                case Key.Enter:
                    if (!AcceptsReturn) {
                        e.Handled = FocusAdvancement.MoveFocus(this);
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

            if (!e.Handled) {
                base.OnPreviewKeyDown(e);
            }
        }

        public SpecialMode Mode {
            get { return (SpecialMode)GetValue(ModeProperty); }
            set { SetValue(ModeProperty, value); }
        }

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(BetterTextBox), new PropertyMetadata(double.NegativeInfinity));

        public double Minimum {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(BetterTextBox), new PropertyMetadata(double.PositiveInfinity));

        public double Maximum {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        internal static void SetTextBoxText(TextBox textBox, string text) {
            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;
            textBox.Text = text;
            textBox.SelectionStart = selectionStart;
            textBox.SelectionLength = selectionLength;
        }

        internal static string ProcessText(SpecialMode mode, string text, double delta, double minValue, double maxValue) {
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

                    if (splitted.Length != 2 || !FlexibleParser.TryParseInt(splitted[0], out hours) || !FlexibleParser.TryParseInt(splitted[1], out minutes)) return null;

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
                        splitted[index] = FlexibleParser.ReplaceDouble(splitted[index], value + delta);
                    }

                    return string.Join(@".", splitted);
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