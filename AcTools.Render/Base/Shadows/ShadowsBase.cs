using System;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Shadows {
    public abstract class ShadowsBase : IDisposable {
        public int MapSize { get; private set; }
        private Viewport _viewport;

        private RasterizerState _rasterizerState;
        private DepthStencilState _depthStencilState;
        
        protected ShadowsBase(int mapSize) {
            MapSize = mapSize;
            _viewport = new Viewport(0, 0, MapSize, MapSize, 0, 1.0f);
        }

        protected virtual RasterizerStateDescription GetRasterizerStateDescription() {
            return new RasterizerStateDescription {
                CullMode = CullMode.Front,
                FillMode = FillMode.Solid,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = true,
                DepthBias = 100,
                DepthBiasClamp = 0.0f,
                SlopeScaledDepthBias = 1f
            };
        }

        public void Initialize(DeviceContextHolder holder) {
            _rasterizerState = RasterizerState.FromDescription(holder.Device, GetRasterizerStateDescription());

            _depthStencilState = DepthStencilState.FromDescription(holder.Device, new DepthStencilStateDescription {
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Greater,
                IsDepthEnabled = true,
                IsStencilEnabled = false
            });
            
            ResizeBuffers(holder, MapSize);
        }

        public abstract void Invalidate();

        public void SetMapSize(DeviceContextHolder holder, int value) {
            if (Equals(value, MapSize)) return;
            MapSize = value;
            _viewport = new Viewport(0, 0, value, value, 0, 1.0f);
            ResizeBuffers(holder, value);
        }

        public void DrawScene(DeviceContextHolder holder, [NotNull] IShadowsDraw draw) {
            using (holder.SaveRenderTargetAndViewport()) {
                holder.DeviceContext.Rasterizer.SetViewports(_viewport);
                holder.DeviceContext.OutputMerger.DepthStencilState = null;
                holder.DeviceContext.OutputMerger.BlendState = null;
                holder.DeviceContext.Rasterizer.State = _rasterizerState;

                UpdateBuffers(holder, draw);

                holder.DeviceContext.Rasterizer.State = null;
                holder.DeviceContext.OutputMerger.DepthStencilState = null;
            }
        }

        public void Clear(DeviceContextHolder holder) {
            ClearBuffers(holder);
        }

        protected abstract void UpdateBuffers(DeviceContextHolder holder, IShadowsDraw draw);

        protected abstract void ClearBuffers(DeviceContextHolder holder);

        protected abstract void ResizeBuffers(DeviceContextHolder holder, int size);

        public void Dispose() {
            DisposeOverride();
            try {
                // TODO: figure out what's going on
                DisposeHelper.Dispose(ref _rasterizerState);
                DisposeHelper.Dispose(ref _depthStencilState);
            } catch (ObjectDisposedException) { }
        }

        protected abstract void DisposeOverride();
    }
}