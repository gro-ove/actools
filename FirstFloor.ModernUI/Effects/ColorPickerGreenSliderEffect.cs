using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerGreenSliderEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerGreenSliderEffect.ps", UriKind.Relative)
        };

        public ColorPickerGreenSliderEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(RedProperty);
            UpdateShaderValue(BlueProperty);
        }

        public static readonly DependencyProperty RedProperty = DependencyProperty.Register("Red", typeof(double),
                typeof(ColorPickerGreenSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), RedCoerceSaturationFactor));

        public double Red {
            get { return (double)GetValue(RedProperty); }
            set { SetValue(RedProperty, value); }
        }

        private static object RedCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 255d, 0d), 1d);
        }

        public static readonly DependencyProperty BlueProperty = DependencyProperty.Register("Blue", typeof(double),
                typeof(ColorPickerGreenSliderEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(1), BlueCoerceSaturationFactor));

        public double Blue {
            get { return (double)GetValue(BlueProperty); }
            set { SetValue(BlueProperty, value); }
        }

        private static object BlueCoerceSaturationFactor(DependencyObject d, object value) {
            return Math.Min(Math.Max((double)value / 255d, 0d), 1d);
        }
    }
}