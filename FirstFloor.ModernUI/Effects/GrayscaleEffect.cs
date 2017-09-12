using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class GrayscaleEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/GrayscaleEffect.ps", UriKind.Relative)
        };

        public GrayscaleEffect() {
            PixelShader = PixelShaderCompiled;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(SaturationFactorProperty);
        }

        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(GrayscaleEffect), 0);

        public Brush Input {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public static readonly DependencyProperty SaturationFactorProperty = DependencyProperty.Register("SaturationFactor", typeof(double),
                typeof(GrayscaleEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0), CoerceSaturationFactor));

        public double SaturationFactor {
            get => GetValue(SaturationFactorProperty) as double? ?? 0d;
            set => SetValue(SaturationFactorProperty, value);
        }

        private static object CoerceSaturationFactor(DependencyObject d, object value) {
            var effect = (GrayscaleEffect)d;
            var newFactor = (double)value;

            if (newFactor < 0.0 || newFactor > 1.0) {
                return effect.SaturationFactor;
            }

            return newFactor;
        }
    }
}
