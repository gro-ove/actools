using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class NorEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/NorEffect.ps", UriKind.Relative)
        };

        public NorEffect() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(OverlayProperty);
        }

        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(NorEffect), 0);

        public Brush Input {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public static readonly DependencyProperty OverlayProperty = RegisterPixelShaderSamplerProperty("Overlay", typeof(NorEffect), 1);

        public Brush Overlay {
            get => (Brush)GetValue(OverlayProperty);
            set => SetValue(OverlayProperty, value);
        }
    }
}