using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerSaturationSliderEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerSaturationSliderEffect.ps", UriKind.Relative)
        };

        public ColorPickerSaturationSliderEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(HueProperty);
            UpdateShaderValue(BrightnessProperty);
        }

        public static readonly DependencyProperty HueProperty = DependencyProperty.Register("Hue", typeof(double),
                typeof(ColorPickerSaturationSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), HueCoerceSaturationFactor));

        public double Hue {
            get => GetValue(HueProperty) as double? ?? 0d;
            set => SetValue(HueProperty, value);
        }

        private static object HueCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 360d, 0d), 1d);
        }

        public static readonly DependencyProperty BrightnessProperty = DependencyProperty.Register("Brightness", typeof(double),
                typeof(ColorPickerSaturationSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(1), BrightnessCoerceSaturationFactor));

        public double Brightness {
            get => GetValue(BrightnessProperty) as double? ?? 0d;
            set => SetValue(BrightnessProperty, value);
        }

        private static object BrightnessCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 100d, 0d), 1d);
        }
    }
}