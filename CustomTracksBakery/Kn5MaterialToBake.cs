using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Textures;
using CustomTracksBakery.Shaders;
using SlimDX;
using SlimDX.Direct3D11;

namespace CustomTracksBakery {
    public class Kn5MaterialToBake : IRenderableMaterial {
        private readonly Kn5Material _kn5Material;
        private EffectBakeryShaders _effect;
        private bool _multilayer;
        private bool _init;

        private float _propDiffuse;
        private float _propMagicMult;
        private Vector4 _propMultilayerMult;

        internal Kn5MaterialToBake(Kn5Material kn5Material) {
            _kn5Material = kn5Material;
            if (kn5Material == null) return;

            _propDiffuse = (_kn5Material.GetPropertyByName("ksAmbient")?.ValueA ?? 0.55f)
                    + (_kn5Material.GetPropertyByName("ksDiffuse")?.ValueA ?? 0.45f) * 0.5f;
            _propMagicMult = _kn5Material.GetPropertyByName("magicMult")?.ValueA ?? 1.0f;
            _propMultilayerMult = new Vector4(
                    _kn5Material.GetPropertyByName("multR")?.ValueA ?? 1.0f,
                    _kn5Material.GetPropertyByName("multG")?.ValueA ?? 1.0f,
                    _kn5Material.GetPropertyByName("multB")?.ValueA ?? 1.0f,
                    _kn5Material.GetPropertyByName("multA")?.ValueA ?? 1.0f);
            _multilayer = kn5Material.ShaderName.Contains("Multilayer");
        }

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (_effect != null) return;
            _effect = contextHolder.GetEffect<EffectBakeryShaders>();
        }

        public void Refresh(IDeviceContextHolder contextHolder) { }

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            // Optimized: moved to MainBakery.Render()
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) { }

        private ShaderResourceView _txDiffuseView;
        private ShaderResourceView _txMaskView;
        private ShaderResourceView _txDetailRView;
        private ShaderResourceView _txDetailGView;
        private ShaderResourceView _txDetailBView;
        private ShaderResourceView _txDetailAView;

        public static bool SecondPass;
        public static float SecondPassBrightnessGain;

        public static bool GrassPass;

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            if (!_init) {
                _init = true;

                _txDiffuseView = Tex(contextHolder, "txDiffuse");

                if (_multilayer) {
                    _txMaskView = Tex(contextHolder, "txMask");
                    _txDetailRView = Tex(contextHolder, "txDetailR");
                    _txDetailGView = Tex(contextHolder, "txDetailG");
                    _txDetailBView = Tex(contextHolder, "txDetailB");
                    _txDetailAView = Tex(contextHolder, "txDetailA");
                }
            }

            _effect.FxDiffuseMap.SetResource(_txDiffuseView);
            _effect.FxAlphaRef.Set(_kn5Material?.AlphaTested == true ? 0.5f : -0.5f);

            if (GrassPass) {
                _effect.TechPerPixel_GrassPass.DrawAllPasses(contextHolder.DeviceContext, indices);
            } else if (_multilayer) {
                _effect.FxMagicMult.Set(_propMagicMult * _propDiffuse * (SecondPass ? SecondPassBrightnessGain : 1.0f));
                _effect.FxMultRGBA.Set(_propMultilayerMult);
                _effect.FxMaskMap.SetResource(_txMaskView);
                _effect.FxDetailRMap.SetResource(_txDetailRView);
                _effect.FxDetailGMap.SetResource(_txDetailGView);
                _effect.FxDetailBMap.SetResource(_txDetailBView);
                _effect.FxDetailAMap.SetResource(_txDetailAView);
                (SecondPass ? _effect.TechMultiLayer_SecondPass : _effect.TechMultiLayer).DrawAllPasses(contextHolder.DeviceContext, indices);
            } else {
                _effect.FxKsDiffuse.Set(_propDiffuse * (SecondPass ? SecondPassBrightnessGain : 1.0f));
                (SecondPass ? _effect.TechPerPixel_SecondPass :_effect.TechPerPixel).DrawAllPasses(contextHolder.DeviceContext, indices);
            }
        }

        private ShaderResourceView Tex(IDeviceContextHolder contextHolder, string key) {
            return contextHolder.Get<ITexturesProvider>().GetTexture(contextHolder, _kn5Material?.GetMappingByName(key)?.Texture ?? ".")?.Resource;
        }

        public bool IsBlending => false;

        public void Dispose() { }
    }
}