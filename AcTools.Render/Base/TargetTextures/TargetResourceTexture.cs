using AcTools.Utils.Helpers;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.TargetTextures {
    public class TargetResourceTextureArray : BaseTargetResourceTexture {
        public RenderTargetView[] TargetViews { get; private set; }

        public Format Format => Description.Format;

        public TargetResourceTextureArray(Texture2DDescription description) : base(description) {}
        
        public override void Resize(DeviceContextHolder holder, int width, int height, SampleDescription? sample) {
            if (width == Width && height == Height && (!sample.HasValue || Description.SampleDescription == sample.Value)) return;
            base.Resize(holder, width, height, sample);

            TargetViews = new RenderTargetView[Description.ArraySize];

            for (var i = 0; i < TargetViews.Length; i++) {
                TargetViews[i] = new RenderTargetView(holder.Device, Texture, new RenderTargetViewDescription {
                    Format = Description.Format,
                    Dimension = RenderTargetViewDimension.Texture2DArray,
                    ArraySize = 1,
                    MipSlice = 0,
                    FirstArraySlice = i
                });
            }

            View = new ShaderResourceView(holder.Device, Texture);
        }

        public override void Dispose() {
            base.Dispose();

            if (TargetViews != null) {
                TargetViews.DisposeEverything();
                TargetViews = null;
            }
        }

        public static TargetResourceTextureArray Create(Format format, int arraySize, int mipLevels = 1) {
            return new TargetResourceTextureArray(new Texture2DDescription {
                MipLevels = mipLevels,
                ArraySize = arraySize,
                Format = format,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
        }
    }

    public class TargetResourceTexture : BaseTargetResourceTexture {
        public RenderTargetView TargetView { get; private set; }

        public Format Format => Description.Format;

        public TargetResourceTexture(Texture2DDescription description) : base(description) {}
        
        public override void Resize(DeviceContextHolder holder, int width, int height, SampleDescription? sample) {
            if (width == Width && height == Height && (!sample.HasValue || Description.SampleDescription == sample.Value)) return;
            base.Resize(holder, width, height, sample);

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

        public static TargetResourceTexture Create(Format format, int mipLevels = 1) {
            return new TargetResourceTexture(new Texture2DDescription {
                MipLevels = mipLevels,
                ArraySize = 1,
                Format = format,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
        }
    }

    public class TargetResourceSpecialTexture : BaseTargetResourceTexture {
        public DepthStencilView StencilView { get; private set; }
        
        public RenderTargetView TargetView { get; private set; }

        public TargetResourceSpecialTexture(Texture2DDescription description) : base(description) { }

        public override void Resize(DeviceContextHolder holder, int width, int height, SampleDescription? sample) {
            if (width == Width && height == Height && (!sample.HasValue || Description.SampleDescription == sample.Value)) return;
            base.Resize(holder, width, height, sample);

            StencilView = new DepthStencilView(holder.Device, Texture, new DepthStencilViewDescription {
                Flags = DepthStencilViewFlags.None,
                Format = Format.D24_UNorm_S8_UInt,
                Dimension = DepthStencilViewDimension.Texture2D,
                MipSlice = 0
            });

            TargetView = new RenderTargetView(holder.Device, Texture, new RenderTargetViewDescription {
                MipSlice = 0,
                Dimension = RenderTargetViewDimension.Texture2D,
                Format = Format.D24_UNorm_S8_UInt
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

            if (StencilView != null) {
                StencilView.Dispose();
                StencilView = null;
            }

            if (TargetView != null) {
                TargetView.Dispose();
                TargetView = null;
            }
        }

        public static TargetResourceSpecialTexture Create() {
            return new TargetResourceSpecialTexture(new Texture2DDescription {
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R24G8_Typeless,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
        }
    }
}
