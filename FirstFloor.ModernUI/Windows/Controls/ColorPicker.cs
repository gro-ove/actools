using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ColorPicker : Control {
        private static WeakReference<ColorPicker> _lastColorPicker;

        /// <summary>
        /// Needed a reference so it could stay open while user is picking a color from screen.
        /// </summary>
        [CanBeNull]
        internal static ColorPicker GetLastOpened() {
            ColorPicker r = null;
            return _lastColorPicker?.TryGetTarget(out r) == true ? r : null;
        }

        public ColorPicker() {
            DefaultStyleKey = typeof(ColorPicker);
        }

        internal Popup Popup { get; private set; }
        private ColorPickerPanel _panel;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (Popup != null) {
                Popup.Opened -= Popup_Opened;
            }

            Popup = GetTemplateChild("PART_Popup") as Popup;
            _panel = GetTemplateChild("PART_Panel") as ColorPickerPanel;
            
            if (Popup != null) {
                Popup.Opened += Popup_Opened;
            }
        }

        private void Popup_Opened(object sender, EventArgs e) {
            _panel.OriginalColor = Color;
            _lastColorPicker = new WeakReference<ColorPicker>(this);
        }

        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color), typeof(Color),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(Color.FromRgb(0, 0, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

        public Color Color {
            get { return (Color)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }

        private static void OnColorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ColorPicker)o).OnColorChanged((Color)e.OldValue, (Color)e.NewValue);
        }

        private bool _skip;

        private void OnColorChanged(Color oldValue, Color newValue) {
            if (_skip) return;
            if (DisplayColor.ToColor() != newValue) {
                DisplayColor = newValue.ToHexString();
            }
        }

        public static readonly DependencyProperty DisplayColorProperty = DependencyProperty.Register(nameof(DisplayColor), typeof(string),
                typeof(ColorPicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDisplayColorChanged));

        public string DisplayColor {
            get { return (string)GetValue(DisplayColorProperty); }
            set { SetValue(DisplayColorProperty, value); }
        }

        private static void OnDisplayColorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ColorPicker)o).OnDisplayColorChanged((string)e.OldValue, (string)e.NewValue);
        }

        private void OnDisplayColorChanged(string oldValue, string newValue) {
            var color = newValue.ToColor();
            if (color.HasValue) {
                _skip = true;
                Color = color.Value;
                _skip = false;
            }
        }

        private class InnerColorToBrushConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                var c = value as Color?;
                return c == null ? null : new SolidColorBrush(c.Value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter ColorToBrushConverter { get; } = new InnerColorToBrushConverter();

        private class InnerColorToStringConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return (value as Color?)?.ToHexString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter ColorToStringConverter { get; } = new InnerColorToStringConverter();
    }
}
