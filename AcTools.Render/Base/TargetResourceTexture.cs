using System;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base {
    public class AbstractTargetResourceTexture : IDisposable {
        protected Texture2DDescription Description;

        protected AbstractTargetResourceTexture(Texture2DDescription description) {
            Description = description;
        }

        public int Width {
            get { return Description.Width; }
        }

        public int Height {
            get { return Description.Height; }
        }

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

    public class TargetResourceTexture : AbstractTargetResourceTexture {
        public RenderTargetView TargetView { get; private set; }

        public TargetResourceTexture(Texture2DDescription description) : base(description) {}
        
        public override void Resize(DeviceContextHolder holder, int width, int height) {
            base.Resize(holder, width, height);

            TargetView = new RenderTargetView(holder.Device, Texture);
            View = new ShaderResourceView(holder.Device, Texture);
        }

        public override void Dispose() {
            base.Dispose();

            if (TargetView != null) {
                TargetView.Dispose();
                TargetView = null;
            }
        }

        public static TargetResourceTexture Create(Format format, SampleDescription sampleDescription) {
            return new TargetResourceTexture(new Texture2DDescription {
                MipLevels = 1,
                ArraySize = 1,
                Format = format,
                SampleDescription = sampleDescription,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
        }

        public static TargetResourceTexture Create(Format format) {
            return Create(format, DefaultSampleDescription);
        }
    }

    public class TargetResourceDepthTexture : AbstractTargetResourceTexture {
        public DepthStencilView TargetView { get; private set; }

        public TargetResourceDepthTexture(Texture2DDescription description) : base(description) {}

        public override void Resize(DeviceContextHolder holder, int width, int height) {
            base.Resize(holder, width, height);
            
            TargetView = new DepthStencilView(holder.Device, Texture, new DepthStencilViewDescription {
                Flags = DepthStencilViewFlags.None,
                Format = Format.D24_UNorm_S8_UInt,
                Dimension = DepthStencilViewDimension.Texture2D,
                MipSlice = 0
            });

            View = new ShaderResourceView(holder.Device, Texture, new ShaderResourceViewDescription {
                Format = Format.R24_UNorm_X8_Typeless,
                Dimension = ShaderResourceViewDimension.Texture2D,
                MipLevels = 1,
                MostDetailedMip = 0
            });
        }

        public override void Dispose() {
            base.Dispose();

            if (TargetView != null) {
                TargetView.Dispose();
                TargetView = null;
            }
        }

        public static TargetResourceDepthTexture Create() {
            return new TargetResourceDepthTexture(new Texture2DDescription {
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R24G8_Typeless,
                SampleDescription = DefaultSampleDescription,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
        }
    }
}
