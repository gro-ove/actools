using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Converters;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class DoubleValueLabel : ValueLabel {
        static DoubleValueLabel() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DoubleValueLabel), new FrameworkPropertyMetadata(typeof(DoubleValueLabel)));
        }

        private readonly Busy _relativeRangeBusy = new Busy();

        public static readonly DependencyProperty SecondValueProperty = DependencyProperty.Register(nameof(SecondValue), typeof(string),
                typeof(DoubleValueLabel), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSecondValueChanged));

        private string _secondValue = "";
        public string SecondValue {
            get => _secondValue;
            set => SetValue(SecondValueProperty, value);
        }

        private static void OnSecondValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DoubleValueLabel)o).OnSecondValueChanged((string)e.NewValue);
        }

        private void OnSecondValueChanged(string newValue) {
            _secondValue = newValue;
            OnValuesChanged();
        }

        protected override void OnValueChanged(string newValue) {
            base.OnValueChanged(newValue);
            OnValuesChanged();
        }

        private void OnValuesChanged() {
            _relativeRangeBusy.Do(() => {
                var format = RelativeRangeStringFormat;
                var from = Value.As<double>();
                var to = SecondValue.As<double>();
                RelativeRangeBase = ((from + to) / 2).ToString(format);
                RelativeRangeHalf = (Math.Abs(from - to) / 2).ToString(format);
                SetValue(ValuesEqualPropertyKey, RelativeRangeHalf.As<double>() == 0);
            });
        }

        private void OnRelativeRangeValuesChanged() {
            _relativeRangeBusy.Do(() => {
                var rangeBase = RelativeRangeBase.As<double>();
                var rangeHalf = RelativeRangeHalf.As<double>();
                Value = (rangeBase - rangeHalf).ToString(CultureInfo.CurrentUICulture);
                SecondValue = (rangeBase + rangeHalf).ToString(CultureInfo.CurrentUICulture);
                SetValue(ValuesEqualPropertyKey, rangeHalf == 0f);
            });
        }

        public static readonly DependencyProperty RelativeRangeBaseProperty = DependencyProperty.Register(nameof(RelativeRangeBase), typeof(string),
                typeof(DoubleValueLabel), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRelativeRangeBaseChanged));

        private string _relativeRangeBase = "";
        public string RelativeRangeBase {
            get => _relativeRangeBase;
            set => SetValue(RelativeRangeBaseProperty, value);
        }

        private static void OnRelativeRangeBaseChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DoubleValueLabel)o).OnRelativeRangeBaseChanged((string)e.NewValue);
        }

        private void OnRelativeRangeBaseChanged(string newValue) {
            _relativeRangeBase = newValue;
            OnRelativeRangeValuesChanged();
        }

        public static readonly DependencyProperty RelativeRangeHalfProperty = DependencyProperty.Register(nameof(RelativeRangeHalf), typeof(string),
                typeof(DoubleValueLabel), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRelativeRangeHalfChanged));

        private string _relativeRangeHalf = "";
        public string RelativeRangeHalf {
            get => _relativeRangeHalf;
            set => SetValue(RelativeRangeHalfProperty, value);
        }

        private static void OnRelativeRangeHalfChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((DoubleValueLabel)o).OnRelativeRangeHalfChanged((string)e.NewValue);
        }

        private void OnRelativeRangeHalfChanged(string newValue) {
            _relativeRangeHalf = newValue;
            OnRelativeRangeValuesChanged();
        }

        public static readonly DependencyProperty RelativeRangeStringFormatProperty = DependencyProperty.Register(nameof(RelativeRangeStringFormat),
                typeof(string), typeof(DoubleValueLabel), new PropertyMetadata("", (o, e) => {
                    ((DoubleValueLabel)o)._relativeRangeStringFormat = (string)e.NewValue;
                }));

        private string _relativeRangeStringFormat = "";
        public string RelativeRangeStringFormat {
            get => _relativeRangeStringFormat;
            set => SetValue(RelativeRangeStringFormatProperty, value);
        }

        public static readonly DependencyPropertyKey ValuesEqualPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ValuesEqual), typeof(bool),
                typeof(DoubleValueLabel), new PropertyMetadata(false));

        public static readonly DependencyProperty ValuesEqualProperty = ValuesEqualPropertyKey.DependencyProperty;

        public bool ValuesEqual => GetValue(ValuesEqualProperty) as bool? == true;

        public static readonly DependencyProperty SeparatorProperty = DependencyProperty.Register(nameof(Separator), typeof(string),
                typeof(DoubleValueLabel));

        public string Separator {
            get => (string)GetValue(SeparatorProperty);
            set => SetValue(SeparatorProperty, value);
        }

        public static readonly DependencyProperty JoinIfEqualProperty = DependencyProperty.Register(nameof(JoinIfEqual), typeof(bool),
                typeof(DoubleValueLabel));

        public bool JoinIfEqual {
            get => GetValue(JoinIfEqualProperty) as bool? == true;
            set => SetValue(JoinIfEqualProperty, value);
        }

        public static readonly DependencyProperty RelativeRangeProperty = DependencyProperty.Register(nameof(RelativeRange), typeof(bool),
                typeof(DoubleValueLabel));

        public bool RelativeRange {
            get => GetValue(RelativeRangeProperty) as bool? == true;
            set => SetValue(RelativeRangeProperty, value);
        }
    }

    [ContentProperty(nameof(Content))]
    public class ValueLabel : Control {
        static ValueLabel() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ValueLabel), new FrameworkPropertyMetadata(typeof(ValueLabel)));
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(string),
                typeof(ValueLabel));

        public string Content {
            get => (string)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string),
                typeof(ValueLabel), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged) {
                    DefaultUpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                });

        private string _value = "";
        public string Value {
            get => _value;
            set => SetValue(ValueProperty, value);
        }

        private static void OnValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ValueLabel)o).OnValueChanged((string)e.NewValue);
        }

        protected virtual void OnValueChanged(string newValue) {
            _value = newValue;
        }

        public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(nameof(Prefix), typeof(string),
                typeof(ValueLabel));

        public string Prefix {
            get => (string)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public static readonly DependencyProperty PostfixProperty = DependencyProperty.Register(nameof(Postfix), typeof(string),
                typeof(ValueLabel));

        public string Postfix {
            get => (string)GetValue(PostfixProperty);
            set => SetValue(PostfixProperty, value);
        }

        public static readonly DependencyProperty ShowZeroAsOffProperty = DependencyProperty.Register(nameof(ShowZeroAsOff), typeof(bool),
                typeof(ValueLabel));

        public bool ShowZeroAsOff {
            get => GetValue(ShowZeroAsOffProperty) as bool? == true;
            set => SetValue(ShowZeroAsOffProperty, value);
        }

        public static readonly DependencyProperty ShowPostfixProperty = DependencyProperty.Register(nameof(ShowPostfix), typeof(bool),
                typeof(ValueLabel), new PropertyMetadata(true));

        public bool ShowPostfix {
            get => GetValue(ShowPostfixProperty) as bool? == true;
            set => SetValue(ShowPostfixProperty, value);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            base.OnMouseLeftButtonUp(e);
            if (GetTemplateChild("PART_TextBox") is TextBox t && !t.IsFocused) {
                Keyboard.Focus(t);
            }
        }
    }
}
