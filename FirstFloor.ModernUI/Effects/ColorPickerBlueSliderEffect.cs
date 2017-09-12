using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerBlueSliderEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerBlueSliderEffect.ps", UriKind.Relative)
        };

        public ColorPickerBlueSliderEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(RedProperty);
            UpdateShaderValue(GreenProperty);
        }

        public static readonly DependencyProperty RedProperty = DependencyProperty.Register("Red", typeof(double),
                typeof(ColorPickerBlueSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), RedCoerceSaturationFactor));

        public double Red {
            get => GetValue(RedProperty) as double? ?? 0d;
            set => SetValue(RedProperty, value);
        }

        private static object RedCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 255d, 0d), 1d);
        }

        public static readonly DependencyProperty GreenProperty = DependencyProperty.Register("Green", typeof(double),
                typeof(ColorPickerBlueSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), GreenCoerceSaturationFactor));

        public double Green {
            get => GetValue(GreenProperty) as double? ?? 0d;
            set => SetValue(GreenProperty, value);
        }

        private static object GreenCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 255d, 0d), 1d);
        }
    }
}