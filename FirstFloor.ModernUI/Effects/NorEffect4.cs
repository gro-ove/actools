using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FirstFloor.ModernUI.Effects {
    public class NorEffect4 : ShaderEffect {
        private static readonly PixelShader PixelShaderCompiled = new PixelShader {
            UriSource = new Uri("/FirstFloor.ModernUI;component/Effects/Shaders/NorEffect4.ps", UriKind.Relative)
        };

        public NorEffect4() {
            PixelShader = PixelShaderCompiled;
            UpdateShaderValue(Layer1Property); //
            UpdateShaderValue(Layer2Property);
            UpdateShaderValue(Layer3Property);
            UpdateShaderValue(Layer4Property);
        }

        public static readonly DependencyProperty Layer1Property = RegisterPixelShaderSamplerProperty("Layer1", typeof(NorEffect4), 0);

        public Brush Layer1 {
            get => (Brush)GetValue(Layer1Property);
            set => SetValue(Layer1Property, value);
        }

        public static readonly DependencyProperty Layer2Property = RegisterPixelShaderSamplerProperty("Layer2", typeof(NorEffect4), 1);

        public Brush Layer2 {
            get => (Brush)GetValue(Layer2Property);
            set => SetValue(Layer2Property, value);
        }

        public static readonly DependencyProperty Layer3Property = RegisterPixelShaderSamplerProperty("Layer3", typeof(NorEffect4), 2);

        public Brush Layer3 {
            get => (Brush)GetValue(Layer3Property);
            set => SetValue(Layer3Property, value);
        }

        public static readonly DependencyProperty Layer4Property = RegisterPixelShaderSamplerProperty("Layer4", typeof(NorEffect4), 3);

        public Brush Layer4 {
            get => (Brush)GetValue(Layer4Property);
            set => SetValue(Layer4Property, value);
        }
    }
}