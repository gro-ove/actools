using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Textures;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Materials {
    public class AmbientShadowMaterial : IRenderableMaterial {
        private readonly string _filename;
        private EffectDeferredGObject _effect;

        private IRenderableTexture _txDiffuse;

        internal AmbientShadowMaterial(string filename) {
            _filename = filename;
        }

        private DepthStencilState _depthStencilState;
        private BlendState _blendState;

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectDeferredGObject>();
            _txDiffuse = TexturesProvider.GetTexture(_filename, contextHolder);

            _depthStencilState = DepthStencilState.FromDescription(contextHolder.Device, new DepthStencilStateDescription {
                IsDepthEnabled = true,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthComparison = Comparison.LessEqual,
                IsStencilEnabled = true,
                StencilReadMask = 0xff,
                StencilWriteMask = 0xff,
                FrontFace = new DepthStencilOperationDescription {
                    FailOperation = StencilOperation.Keep,
                    DepthFailOperation = StencilOperation.Keep,
                    PassOperation = StencilOperation.Replace,
                    Comparison = Comparison.Always
                },
                BackFace = new DepthStencilOperationDescription {
                    FailOperation = StencilOperation.Keep,
                    DepthFailOperation = StencilOperation.Keep,
                    PassOperation = StencilOperation.Replace,
                    Comparison = Comparison.Always
                }
            });


            var transDesc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false,
            };

            transDesc.RenderTargets[0] = new RenderTargetBlendDescription {
                BlendEnable = true,
                SourceBlend = BlendOption.SourceAlpha,
                DestinationBlend = BlendOption.InverseSourceAlpha,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = BlendOption.SourceAlpha,
                DestinationBlendAlpha = BlendOption.InverseSourceAlpha,
                BlendOperationAlpha = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteMaskFlags.All
            };

            _blendState = BlendState.FromDescription(contextHolder.Device, transDesc);
        }

        private bool _skip;

        public void Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            _skip = mode != SpecialRenderMode.Default;
            if (_skip) return;
            _effect.FxDiffuseMap.SetResource(_txDiffuse);
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPT;
            contextHolder.DeviceContext.OutputMerger.BlendState = _blendState;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = _depthStencilState;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            if (_skip) return;
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            if (_skip) return;
            _effect.TechAmbientShadowDeferred.DrawAllPasses(contextHolder.DeviceContext, indices);
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
        }

        public void Dispose() {
            _blendState.Dispose();
            _depthStencilState.Dispose();
        }
    }
}
