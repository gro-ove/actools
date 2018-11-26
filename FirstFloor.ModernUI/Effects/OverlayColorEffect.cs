using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class OverlayColorEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/OverlayColorEffect.ps", UriKind.Relative)
        };

        public OverlayColorEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(InputProperty);
        }

        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(OverlayColorEffect), 0);

        public Brush Input {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public static readonly DependencyProperty OverlayColorProperty = DependencyProperty.Register("OverlayColor", typeof(Color),
                typeof(OverlayColorEffect), new UIPropertyMetadata(default(Color), PixelShaderConstantCallback(0)));

        public Color OverlayColor {
            get => GetValue(OverlayColorProperty) as Color? ?? default;
            set => SetValue(OverlayColorProperty, value);
        }
    }
}