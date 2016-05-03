using System;
using System.Drawing;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Forward {
    public abstract class ForwardRenderer : SceneRenderer {
        public int TrianglesCount { get; protected set; }

        public bool UseFxaa = true;
        public bool ShowWireframe = false;

        private bool _useBloom;

        public bool UseBloom {
            get { return _useBloom; }
            set {
                if (Equals(value, _useBloom)) return;

                _useBloom = value;

                DisposeHelper.Dispose(ref _buffer);
                DisposeHelper.Dispose(ref _buffer1);
                DisposeHelper.Dispose(ref _buffer2);

                if (_useBloom) {
                    _buffer = TargetResourceTexture.Create(Format.R16G16B16A16_Float, SampleDescription);
                    _buffer1 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm, SampleDescription);
                    _buffer2 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm, SampleDescription);
                } else {
                    _buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm, SampleDescription);
                }

                if (!_resized) return;
                _buffer.Resize(DeviceContextHolder, Width, Height);
                _buffer1?.Resize(DeviceContextHolder, Width, Height);
                _buffer2?.Resize(DeviceContextHolder, Width, Height);
            }
        }

        public bool UseMsaa;

        protected override SampleDescription SampleDescription => UseMsaa ? base.SampleDescription : new SampleDescription(1, 0);

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        private TargetResourceTexture _buffer, _buffer1, _buffer2;
        private RasterizerState _wireframeRasterizerState;

        protected override void InitializeInner() {
            UseBloom = true;

            _wireframeRasterizerState = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                FillMode = FillMode.Wireframe,
                CullMode = CullMode.Back,
                IsFrontCounterclockwise = false,
                IsDepthClipEnabled = true
            });
        }

        private bool _resized;

        protected override void ResizeInner() {
            base.ResizeInner();

            _resized = true;
            _buffer.Resize(DeviceContextHolder, Width, Height);
            _buffer1?.Resize(DeviceContextHolder, Width, Height);
            _buffer2?.Resize(DeviceContextHolder, Width, Height);
        }

        private EffectPpHdr _hdr;
        private BlurHelper _blur;

        protected override void DrawInner() {
            DrawPrepare();

            DeviceContext.ClearRenderTargetView(_buffer.TargetView, Color.Gray);
            DeviceContext.ClearDepthStencilView(DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            DeviceContext.OutputMerger.SetTargets(DepthStencilView, _buffer.TargetView);

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = ShowWireframe ? _wireframeRasterizerState : null;

            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.Simple);

            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.ReadOnlyDepthState;
            Scene.Draw(DeviceContextHolder, Camera, SpecialRenderMode.SimpleTransparent);

            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = null;

            ShaderResourceView result;
            if (UseBloom) {
                if (_hdr == null) {
                    _hdr = DeviceContextHolder.GetEffect<EffectPpHdr>();
                    _blur = DeviceContextHolder.GetHelper<BlurHelper>();
                }

                DeviceContext.OutputMerger.SetTargets(_buffer1.TargetView);
                DeviceContext.ClearRenderTargetView(_buffer1.TargetView, ColorTransparent);

                DeviceContextHolder.PrepareQuad(_hdr.LayoutPT);
                _hdr.FxInputMap.SetResource(_buffer.View);
                _hdr.TechBloom.DrawAllPasses(DeviceContext, 6);
                
                _blur.Blur(DeviceContextHolder, _buffer1, _buffer2, 1f, 2);

                DeviceContext.OutputMerger.SetTargets(_buffer2.TargetView);

                _hdr.FxInputMap.SetResource(_buffer.View);
                _hdr.FxBloomMap.SetResource(_buffer1.View);
                _hdr.TechCombine.DrawAllPasses(DeviceContext, 6);

                result = _buffer2.View;
            } else {
                result = _buffer.View;
            }

            DeviceContext.ClearRenderTargetView(RenderTargetView, ColorTransparent);
            if (UseFxaa) {
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, result, RenderTargetView);
            } else {
                DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, result, RenderTargetView);
            }
        }

        public override Image Shot(int multipler) {
            var useFxaa = UseFxaa;
            UseFxaa = false;

            try {
                return base.Shot(multipler);
            } finally {
                UseFxaa = useFxaa;
            }
        }

        protected sealed override void DrawSprites() {
            if (Sprite == null) throw new NotSupportedException();
            DrawSpritesInner();
            Sprite.Flush();
        }

        protected virtual void DrawSpritesInner() {}

        public override void Dispose() {
            base.Dispose();
            
            DisposeHelper.Dispose(ref _wireframeRasterizerState);
            DisposeHelper.Dispose(ref _buffer);
            DisposeHelper.Dispose(ref _buffer1);
            DisposeHelper.Dispose(ref _buffer2);
        }
    }
}
