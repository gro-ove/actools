using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerBluePaletteEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerBluePaletteEffect.ps", UriKind.Relative)
        };

        public ColorPickerBluePaletteEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(BlueProperty);
        }

        public static readonly DependencyProperty BlueProperty = DependencyProperty.Register("Blue", typeof(double),
                typeof(ColorPickerBluePaletteEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), CoerceSaturationFactor));

        public double Blue {
            get { return (double)GetValue(BlueProperty); }
            set { SetValue(BlueProperty, value); }
        }

        private static object CoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 255d, 0d), 1d);
        }
    }
}