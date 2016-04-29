using System;
using System.Drawing;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DirectWrite;
using SlimDX.DXGI;
using SpriteTextRenderer;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using TextBlockRenderer = SpriteTextRenderer.SlimDX.TextBlockRenderer;

namespace AcTools.Render.Forward {
    public abstract class ForwardRenderer : SceneRenderer {
        public bool UseFxaa = true;
        public bool ShowWireframe = false;
        public bool VisibleUi = true;

        protected override SampleDescription GetSampleDescription(int msaaQuality) => new SampleDescription(1, 0);

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        private TargetResourceTexture _buffer;
        private TextBlockRenderer _textBlock;
        private RasterizerState _wireframeRasterizerState;

        protected override void InitializeInner() {
            _buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm, SampleDescription);

            _wireframeRasterizerState = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                FillMode = FillMode.Wireframe,
                CullMode = CullMode.Back,
                IsFrontCounterclockwise = false,
                IsDepthClipEnabled = true
            });
        }

        protected override void ResizeInner() {
            base.ResizeInner();
            _buffer.Resize(DeviceContextHolder, Width, Height);

            if (_textBlock != null) return;
            _textBlock = new TextBlockRenderer(Sprite, "Consolas", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
        }

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

            DeviceContext.ClearRenderTargetView(RenderTargetView, ColorTransparent);
            if (UseFxaa) {
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, _buffer.View, RenderTargetView);
            } else {
                DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _buffer.View, RenderTargetView);
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

        protected void DrawSpritesInner() {
            if (VisibleUi) {
                _textBlock.DrawString($@"
FPS:            {FramesPerSecond:F1}{(SyncInterval ? " (limited)" : "")}
FXAA:           {(!UseFxaa ? "No" : "Yes")}".Trim(),
                        new Vector2(Width - 300, 20), 16f, new Color4(1.0f, 1.0f, 1.0f),
                        CoordinateType.Absolute);
            }
        }

        public override void Dispose() {
            base.Dispose();

            DisposeHelper.Dispose(ref _textBlock);
            DisposeHelper.Dispose(ref _wireframeRasterizerState);
            DisposeHelper.Dispose(ref _buffer);
        }
    }
}
