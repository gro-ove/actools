using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerHuePaletteEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerHuePaletteEffect.ps", UriKind.Relative)
        };

        public ColorPickerHuePaletteEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(HueProperty);
        }

        public static readonly DependencyProperty HueProperty = DependencyProperty.Register("Hue", typeof(double),
                typeof(ColorPickerHuePaletteEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), CoerceSaturationFactor));

        public double Hue {
            get => GetValue(HueProperty) as double? ?? 0d;
            set => SetValue(HueProperty, value);
        }

        private static object CoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 360d, 0d), 1d);
        }
    }
}