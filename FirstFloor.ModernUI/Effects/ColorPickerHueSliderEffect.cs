using System;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class ColorPickerHueSliderEffect : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/ColorPickerHueSliderEffect.ps", UriKind.Relative)
        };

        public ColorPickerHueSliderEffect() {
            PixelShader = PixelShaderCompiled;
        }
    }
}