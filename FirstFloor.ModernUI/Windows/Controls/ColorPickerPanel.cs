using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Controls {
    public enum ColorPickingMode {
        Hue,
        Saturation,
        Brightness,
        Red,
        Green,
        Blue
    }

    public class ColorPickerPanel : Control {
        public ColorPickerPanel() {
            DefaultStyleKey = typeof(ColorPickerPanel);
        }

        private Thumb _thumb;
        private Slider _slider;
        private FrameworkElement _palette;
        private FrameworkElement _originalColor;
        private FrameworkElement _pickerButton;

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            OriginalColor = Color;

            if (_thumb != null) {
                _thumb.DragDelta -= Thumb_DragDelta;
            }

            if (_palette != null) {
                _palette.MouseDown -= Palette_MouseMove;
                _palette.MouseMove -= Palette_MouseMove;
                _palette.MouseWheel -= Palette_MouseWheel;
            }

            if (_originalColor != null) {
                _originalColor.MouseLeftButtonUp -= OriginalColor_MouseLeftButtonUp;
            }

            if (_pickerButton != null) {
                _pickerButton.PreviewMouseLeftButtonUp -= PickerButton_MouseLeftButtonUp;
            }

            _thumb = GetTemplateChild("PART_Thumb") as Thumb;
            _slider = GetTemplateChild("PART_Slider") as Slider;
            _palette = GetTemplateChild("PART_Palette") as FrameworkElement;
            _originalColor = GetTemplateChild("PART_OriginalColor") as FrameworkElement;
            _pickerButton = GetTemplateChild("PART_PickerButton") as ModernButton;

            if (_thumb != null) {
                _thumb.DragDelta += Thumb_DragDelta;
            }

            if (_palette != null) {
                _palette.MouseDown += Palette_MouseMove;
                _palette.MouseMove += Palette_MouseMove;
                _palette.MouseWheel += Palette_MouseWheel;
                UpdateThumbPosition();
            }

            if (_originalColor != null) {
                _originalColor.MouseLeftButtonUp += OriginalColor_MouseLeftButtonUp;
            }

            if (_pickerButton != null) {
                _pickerButton.PreviewMouseLeftButtonUp += PickerButton_MouseLeftButtonUp;
            }
        }

        private void OriginalColor_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            Color = OriginalColor;
        }

        private void PickerButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            var picker = ColorPicker.GetLastOpened();
            if (picker != null) {
                picker.Popup.StaysOpen = true;
            }

            Color = ScreenColorPickerDialog.Pick() ?? Color;

            if (picker != null) {
                picker.Popup.Focus();
                picker.Popup.StaysOpen = false;
            }
        }

        private void ThumbMoved() {
            if (_palette == null) return;

            var position = Mouse.GetPosition(_palette);
            var x = position.X / _palette.ActualWidth;
            var y = 1 - position.Y / _palette.ActualHeight;

            switch (Mode) {
                case ColorPickingMode.Hue:
                    Saturation = (int)(100d * x);
                    Brightness = (int)(100d * y);
                    break;

                case ColorPickingMode.Saturation:
                    Hue = (int)(359d * x);
                    Brightness = (int)(100d * y);
                    break;

                case ColorPickingMode.Brightness:
                    Hue = (int)(359d * x);
                    Saturation = (int)(100d * y);
                    break;

                case ColorPickingMode.Red:
                    Blue = (int)(255d * x);
                    Green = (int)(255d * y);
                    break;

                case ColorPickingMode.Green:
                    Blue = (int)(255d * x);
                    Red = (int)(255d * y);
                    break;

                case ColorPickingMode.Blue:
                    Red = (int)(255d * x);
                    Green = (int)(255d * y);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            UpdateThumbPosition();
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e) {
            ThumbMoved();
        }

        private void Palette_MouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            ThumbMoved();
        }

        private void Palette_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (_slider == null) return;
            _slider.Value += e.Delta / _slider.Maximum;
        }

        private bool _skip;

        private void OnColorChanged(Color newValue) {
            if (_skip) {
                return;
            }
            _skip = true;
            UpdateRgb(newValue);
            UpdateHsb(newValue);
            _skip = false;
        }

        private void UpdateThumbPosition() {
            double x, y;

            switch (Mode) {
                case ColorPickingMode.Hue:
                    x = Saturation / 100d;
                    y = Brightness / 100d;
                    break;

                case ColorPickingMode.Saturation:
                    x = Hue / 359d;
                    y = Brightness / 100d;
                    break;

                case ColorPickingMode.Brightness:
                    x = Hue / 359d;
                    y = Saturation / 100d;
                    break;

                case ColorPickingMode.Red:
                    x = Blue / 255d;
                    y = Green / 255d;
                    break;

                case ColorPickingMode.Green:
                    x = Blue / 255d;
                    y = Red / 255d;
                    break;

                case ColorPickingMode.Blue:
                    x = Red / 255d;
                    y = Green / 255d;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            SetValue(ThumbXPropertyKey, (int)(x * 256) - 10);
            SetValue(ThumbYPropertyKey, (int)((1d - y) * 256) - 10);
            SetValue(IsBrightPropertyKey, Brightness > 50);
        }

        private void UpdateRgb(Color value) {
            Red = value.R;
            Green = value.G;
            Blue = value.B;

            UpdateThumbPosition();
        }

        private void UpdateHsb(Color value) {
            Hue = (int)value.GetHue();

            var brightness = value.GetBrightness();
            Brightness = (int)(brightness * 100);

            var saturation = value.GetSaturation();
            Saturation = (int)(saturation * 100);

            UpdateThumbPosition();
        }

        private void OnRgbChanged() {
            if (_skip) {
                return;
            }
            _skip = true;
            var color = Color.FromRgb((byte)Red, (byte)Green, (byte)Blue);
            Color = color;
            UpdateHsb(color);
            _skip = false;
        }

        private void OnHsbChanged() {
            if (_skip) {
                return;
            }
            _skip = true;
            var color = ColorExtension.FromHsb(Hue, 0.01 * Saturation, 0.01 * Brightness);
            Color = color;
            UpdateRgb(color);
            _skip = false;
        }

        #region Properties
        public static readonly DependencyProperty OriginalColorProperty = DependencyProperty.Register(nameof(OriginalColor), typeof(Color), typeof(ColorPickerPanel));

        public Color OriginalColor {
            get { return (Color)GetValue(OriginalColorProperty); }
            set { SetValue(OriginalColorProperty, value); }
        }

        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color), typeof(Color), typeof(ColorPickerPanel), new FrameworkPropertyMetadata(Color.FromRgb(255, 127, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

        public Color Color {
            get { return (Color)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }

        private static void OnColorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ColorPickerPanel)o).OnColorChanged((Color)e.NewValue);
        }

        private static object ChannelCoerceValueCallback(DependencyObject dependencyObject, object baseValue) {
            return Math.Max(Math.Min((int)baseValue, 255), 0);
        }

        public static readonly DependencyProperty RedProperty = DependencyProperty.Register(nameof(Red), typeof(int), typeof(ColorPickerPanel), new PropertyMetadata(0, OnRgbChanged, ChannelCoerceValueCallback));

        public int Red {
            get { return (int)GetValue(RedProperty); }
            set { SetValue(RedProperty, Math.Max(Math.Min(value, 255), 0)); }
        }

        public static readonly DependencyProperty GreenProperty = DependencyProperty.Register(nameof(Green), typeof(int), typeof(ColorPickerPanel), new PropertyMetadata(0, OnRgbChanged, ChannelCoerceValueCallback));

        public int Green {
            get { return (int)GetValue(GreenProperty); }
            set { SetValue(GreenProperty, Math.Max(Math.Min(value, 255), 0)); }
        }

        public static readonly DependencyProperty BlueProperty = DependencyProperty.Register(nameof(Blue), typeof(int), typeof(ColorPickerPanel), new PropertyMetadata(0, OnRgbChanged, ChannelCoerceValueCallback));

        public int Blue {
            get { return (int)GetValue(BlueProperty); }
            set { SetValue(BlueProperty, Math.Max(Math.Min(value, 255), 0)); }
        }

        private static void OnRgbChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ColorPickerPanel)o).OnRgbChanged();
        }

        public static readonly DependencyProperty HueProperty = DependencyProperty.Register(nameof(Hue), typeof(int), typeof(ColorPickerPanel), new PropertyMetadata(0, OnHsbChanged, HueCoerceValueCallback));

        private static object HueCoerceValueCallback(DependencyObject dependencyObject, object baseValue) {
            return Math.Max(Math.Min((int)baseValue, 359), 0);
        }

        public int Hue {
            get { return (int)GetValue(HueProperty); }
            set { SetValue(HueProperty, value); }
        }

        public static readonly DependencyProperty SaturationProperty = DependencyProperty.Register(nameof(Saturation), typeof(int), typeof(ColorPickerPanel), new PropertyMetadata(0, OnHsbChanged, SaturationCoerceValueCallback));

        private static object SaturationCoerceValueCallback(DependencyObject dependencyObject, object baseValue) {
            return Math.Max(Math.Min((int)baseValue, 100), 0);
        }

        public int Saturation {
            get { return (int)GetValue(SaturationProperty); }
            set { SetValue(SaturationProperty, value); }
        }

        public static readonly DependencyProperty BrightnessProperty = DependencyProperty.Register(nameof(Brightness), typeof(int), typeof(ColorPickerPanel), new PropertyMetadata(0, OnHsbChanged, BrightnessCoerceValueCallback));

        private static object BrightnessCoerceValueCallback(DependencyObject dependencyObject, object baseValue) {
            return Math.Max(Math.Min((int)baseValue, 100), 0);
        }

        public int Brightness {
            get { return (int)GetValue(BrightnessProperty); }
            set { SetValue(BrightnessProperty, value); }
        }

        private static void OnHsbChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ColorPickerPanel)o).OnHsbChanged();
        }

        public static readonly DependencyPropertyKey ThumbXPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ThumbX), typeof(int), typeof(ColorPickerPanel), new PropertyMetadata(0));

        public static readonly DependencyProperty ThumbXProperty = ThumbXPropertyKey.DependencyProperty;

        public int ThumbX => (int)GetValue(ThumbXProperty);

        public static readonly DependencyPropertyKey ThumbYPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ThumbY), typeof(int), typeof(ColorPickerPanel), new PropertyMetadata(0));

        public static readonly DependencyProperty ThumbYProperty = ThumbYPropertyKey.DependencyProperty;

        public int ThumbY => (int)GetValue(ThumbYProperty);

        public static readonly DependencyPropertyKey IsBrightPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsBright), typeof(bool), typeof(ColorPickerPanel), new PropertyMetadata(false));

        public static readonly DependencyProperty IsBrightProperty = IsBrightPropertyKey.DependencyProperty;

        public bool IsBright => (bool)GetValue(IsBrightProperty);

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(ColorPickingMode),
                typeof(ColorPickerPanel), new PropertyMetadata(OnModeChanged));

        public ColorPickingMode Mode {
            get { return (ColorPickingMode)GetValue(ModeProperty); }
            set { SetValue(ModeProperty, value); }
        }

        private static void OnModeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ColorPickerPanel)o).UpdateThumbPosition();
        }
        #endregion
    }
}