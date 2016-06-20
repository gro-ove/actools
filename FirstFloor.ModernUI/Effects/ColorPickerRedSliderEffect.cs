using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerRedSliderEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerRedSliderEffect.ps", UriKind.Relative)
        };

        public ColorPickerRedSliderEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(GreenProperty);
            UpdateShaderValue(BlueProperty);
        }

        public static readonly DependencyProperty GreenProperty = DependencyProperty.Register("Green", typeof(double),
                typeof(ColorPickerRedSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), GreenCoerceSaturationFactor));

        public double Green {
            get { return (double)GetValue(GreenProperty); }
            set { SetValue(GreenProperty, value); }
        }

        private static object GreenCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 255d, 0d), 1d);
        }

        public static readonly DependencyProperty BlueProperty = DependencyProperty.Register("Blue", typeof(double),
                typeof(ColorPickerRedSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(1), BlueCoerceSaturationFactor));

        public double Blue {
            get { return (double)GetValue(BlueProperty); }
            set { SetValue(BlueProperty, value); }
        }

        private static object BlueCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 255d, 0d), 1d);
        }
    }
}