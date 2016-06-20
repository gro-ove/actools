using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerRedPaletteEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerRedPaletteEffect.ps", UriKind.Relative)
        };

        public ColorPickerRedPaletteEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(RedProperty);
        }

        public static readonly DependencyProperty RedProperty = DependencyProperty.Register("Red", typeof(double),
                typeof(ColorPickerRedPaletteEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), CoerceSaturationFactor));

        public double Red {
            get { return (double)GetValue(RedProperty); }
            set { SetValue(RedProperty, value); }
        }

        private static object CoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 255d, 0d), 1d);
        }
    }
}