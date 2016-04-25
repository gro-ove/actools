using System;
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

        public Texture2D Texture { get; private set; }

        public ShaderResourceView View { get; protected set; }

        public virtual void Resize(DeviceContextHolder holder, int width, int height) {
            Dispose();
            
            Description.Width = width;
            Description.Height = height;
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