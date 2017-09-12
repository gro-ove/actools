using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerBrightnessPaletteEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerBrightnessPaletteEffect.ps", UriKind.Relative)
        };

        public ColorPickerBrightnessPaletteEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(BrightnessProperty);
        }

        public static readonly DependencyProperty BrightnessProperty = DependencyProperty.Register("Brightness", typeof(double),
                typeof(ColorPickerBrightnessPaletteEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), CoerceSaturationFactor));

        public double Brightness {
            get => GetValue(BrightnessProperty) as double? ?? 0d;
            set => SetValue(BrightnessProperty, value);
        }

        private static object CoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 100d, 0d), 1d);
        }
    }
}