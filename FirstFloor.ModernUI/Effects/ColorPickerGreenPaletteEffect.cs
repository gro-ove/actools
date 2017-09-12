using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerGreenPaletteEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerGreenPaletteEffect.ps", UriKind.Relative)
        };

        public ColorPickerGreenPaletteEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(GreenProperty);
        }

        public static readonly DependencyProperty GreenProperty = DependencyProperty.Register("Green", typeof(double),
                typeof(ColorPickerGreenPaletteEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), CoerceSaturationFactor));

        public double Green {
            get => GetValue(GreenProperty) as double? ?? 0d;
            set => SetValue(GreenProperty, value);
        }

        private static object CoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 255d, 0d), 1d);
        }
    }
}