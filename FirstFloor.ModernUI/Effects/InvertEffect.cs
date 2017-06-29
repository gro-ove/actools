using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class InvertEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/InvertEffect.ps", UriKind.Relative)
        };

        public InvertEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(InputProperty);
        }

        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(InvertEffect), 0);

        public Brush Input {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }
    }
}