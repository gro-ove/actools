using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class AsTransparencyMask : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/AsTransparencyMask.ps", UriKind.Relative)
        };

        public AsTransparencyMask() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(InputProperty);
        }

        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(AsTransparencyMask), 0);

        public Brush Input {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public static readonly DependencyProperty OverlayColorProperty = DependencyProperty.Register("OverlayColor", typeof(Color),
                typeof(AsTransparencyMask), new UIPropertyMetadata(default(Color), PixelShaderConstantCallback(0)));

        public Color OverlayColor {
            get => GetValue(OverlayColorProperty) as Color? ?? default;
            set => SetValue(OverlayColorProperty, value);
        }
    }
}