using System;
using JetBrains.Annotations;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.TargetTextures {
    public class BaseTargetResourceTexture : IDisposable {
        protected Texture2DDescription Description;

        protected BaseTargetResourceTexture(Texture2DDescription description) {
            Description = description;
        }

        public int Width => Description.Width;

        public int Height => Description.Height;

        public Viewport Viewport => new Viewport(0, 0, Width, Height, 0.0f, 1.0f);

        public Texture2D Texture { get; private set; }

        public ShaderResourceView View { get; protected set; }

        public virtual void Resize(DeviceContextHolder holder, int width, int height, [CanBeNull] SampleDescription? sample) {
            if (width == Width && height == Height && (!sample.HasValue || sample == Description.SampleDescription)) return;
            Dispose();
            
            Description.Width = width;
            Description.Height = height;
            Description.SampleDescription = sample ?? DefaultSampleDescription;

            Texture = new Texture2D(holder.Device, Description);
        }

        public virtual void Dispose() {
            if (Texture != null) {
                Texture.Dispose();
                Texture = null;
            }

            if (View != null) {
                View.Dispose();
                View = null;
            }
        }

        protected static readonly SampleDescription DefaultSampleDescription = new SampleDescription(1, 0);
    }
}