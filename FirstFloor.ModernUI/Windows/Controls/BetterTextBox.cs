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
        /* disabled */
        None,

        /* numbers */
        Number,
        Integer,

        [Obsolete]
        Positive,

        /* complicated strings */
        Time,
        Version,

        /* numbers with alternate string for some cases */
        IntegerOrLabel,

        [Obsolete]
        IntegerOrZeroLabel,
        [Obsolete]
        IntegerOrMinusOneLabel,
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
            FocusAdvancement.OnKeyDown(this, e);

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
                    : base(new[] { NullValue }.Concat(collection.OfType<object>())) {
                var o = collection as INotifyCollectionChanged;
                if (o != null) {
                    // ReSharper disable once PossibleMultipleEnumeration
                    _collection = collection;
                    WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(o, nameof(o.CollectionChanged),
                            OnCollectionChanged);
                }
            }

            private void Rebuild() {
                ReplaceEverythingBy(new[] { NullValue }.Concat(_collection.OfType<object>()));
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

        public static readonly DependencyProperty PlaceholderOpacityProperty = DependencyProperty.Register(nameof(PlaceholderOpacity), typeof(double),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(0.5));

        public double PlaceholderOpacity {
            get { return (double)GetValue(PlaceholderOpacityProperty); }
            set { SetValue(PlaceholderOpacityProperty, value); }
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(BetterTextBox));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

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

                case Key.Tab:
                    if (Keyboard.Modifiers == ModifierKeys.Shift && FocusAdvancement.MoveFocus(this, FocusNavigationDirection.Previous)) {
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

        public static SpecialMode GetMode(DependencyObject obj) {
            return (SpecialMode)obj.GetValue(ModeProperty);
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
            return (double)obj.GetValue(ModeLabelValueProperty);
        }

        public static void SetModeLabelValue(DependencyObject obj, double value) {
            obj.SetValue(ModeLabelValueProperty, value);
        }

        public static readonly DependencyProperty ModeLabelValueProperty = DependencyProperty.RegisterAttached("ModeLabelValue", typeof(double),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.Inherits));


        public static double GetMinimum(DependencyObject obj) {
            return (double)obj.GetValue(MinimumProperty);
        }

        public static void SetMinimum(DependencyObject obj, double value) {
            obj.SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.RegisterAttached("Minimum", typeof(double),
                typeof(BetterTextBox), new FrameworkPropertyMetadata(double.NegativeInfinity, FrameworkPropertyMetadataOptions.Inherits));


        public static double GetMaximum(DependencyObject obj) {
            return (double)obj.GetValue(MaximumProperty);
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
            var minValue = GetMinimum(this);
            var maxValue = GetMaximum(this);

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

                case SpecialMode.IntegerOrLabel:
#pragma warning disable 612
                case SpecialMode.IntegerOrMinusOneLabel:
                case SpecialMode.IntegerOrZeroLabel: {
#pragma warning restore 612
                    int value;
                    var skip = !FlexibleParser.TryParseInt(text, out value);
                        
                    if (skip) {
                        value = GetLabelValue();
                    }

                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) {
                        delta *= 10.0;
                    }

                    if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                        delta *= 2.0;
                    }

                    value = (int)Math.Max(Math.Min((int)(value + delta), maxValue), minValue);

                    var label = GetModeLabel(this);
                    if (value == GetLabelValue() && label != null) {
                        return label;
                    }

                    return skip ? value.ToString(CultureInfo.InvariantCulture) : FlexibleParser.ReplaceDouble(text, value);
                }

                case SpecialMode.Time: {
                    var splitted = text.Split(':');
                    int totalSeconds;

                    switch (splitted.Length) {
                        case 2: {
                            int hours, minutes;
                            if (!FlexibleParser.TryParseInt(splitted[0], out hours) || !FlexibleParser.TryParseInt(splitted[1], out minutes)) return null;
                            totalSeconds = (splitted[0].StartsWith(@"-") ? -1 : 1) * (Math.Abs(hours) * 60 + Math.Abs(minutes)) * 60;
                            break;
                        }
                        case 3: {
                            int hours, minutes, seconds;
                            if (!FlexibleParser.TryParseInt(splitted[0], out hours) || !FlexibleParser.TryParseInt(splitted[1], out minutes) ||
                                    !FlexibleParser.TryParseInt(splitted[2], out seconds)) return null;
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