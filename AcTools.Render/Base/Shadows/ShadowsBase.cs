using System;
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

        protected virtual RasterizerState GetRasterizerState(IDeviceContextHolder holder) {
            return holder.States.ShadowsState;
        }

        public void Initialize(DeviceContextHolder holder) {
            _rasterizerState = GetRasterizerState(holder);
            _depthStencilState = holder.States.ShadowsDepthState;
            ResizeBuffers(holder, MapSize);
        }

        public abstract void Invalidate();

        public void SetMapSize(DeviceContextHolder holder, int value) {
            if (Equals(value, MapSize)) return;
            MapSize = value;
            _viewport = new Viewport(0, 0, value, value, 0, 1.0f);
            ResizeBuffers(holder, value);
            Invalidate();
        }

        public void DrawScene(DeviceContextHolder holder, [NotNull] IShadowsDraw draw) {
            using (holder.SaveRenderTargetAndViewport()) {
                holder.DeviceContext.Rasterizer.SetViewports(_viewport);
                holder.DeviceContext.Rasterizer.State = _rasterizerState;
                holder.DeviceContext.OutputMerger.DepthStencilState = _depthStencilState;
                holder.DeviceContext.OutputMerger.BlendState = null;

                UpdateBuffers(holder, draw);

                holder.DeviceContext.Rasterizer.State = null;
                holder.DeviceContext.OutputMerger.DepthStencilState = null;
            }
        }

        public void Clear(DeviceContextHolder holder) {
            ClearBuffers(holder);
            Invalidate();
        }

        protected abstract void UpdateBuffers(DeviceContextHolder holder, IShadowsDraw draw);

        protected abstract void ClearBuffers(DeviceContextHolder holder);

        protected abstract void ResizeBuffers(DeviceContextHolder holder, int size);

        public void Dispose() {
            DisposeOverride();
        }

        protected abstract void DisposeOverride();
    }
}