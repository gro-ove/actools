using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerBrightnessSliderEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerBrightnessSliderEffect.ps", UriKind.Relative)
        };

        public ColorPickerBrightnessSliderEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(HueProperty);
            UpdateShaderValue(SaturationProperty);
        }

        public static readonly DependencyProperty HueProperty = DependencyProperty.Register("Hue", typeof(double),
                typeof(ColorPickerBrightnessSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), HueCoerceSaturationFactor));

        public double Hue {
            get => GetValue(HueProperty) as double? ?? 0d;
            set => SetValue(HueProperty, value);
        }

        private static object HueCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 360d, 0d), 1d);
        }

        public static readonly DependencyProperty SaturationProperty = DependencyProperty.Register("Saturation", typeof(double),
                typeof(ColorPickerBrightnessSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(1), SaturationCoerceSaturationFactor));

        public double Saturation {
            get => GetValue(SaturationProperty) as double? ?? 0d;
            set => SetValue(SaturationProperty, value);
        }

        private static object SaturationCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 100d, 0d), 1d);
        }
    }
}