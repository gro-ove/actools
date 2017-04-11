using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.TargetTextures {
    public class TargetResourceDepthTexture : BaseTargetResourceTexture {
        public DepthStencilView DepthView { get; private set; }

        protected TargetResourceDepthTexture(Texture2DDescription description) : base(description) { }

        public override void Resize(DeviceContextHolder holder, int width, int height, SampleDescription? sample) {
            if (width == Width && height == Height && (!sample.HasValue || Description.SampleDescription == sample.Value)) return;
            base.Resize(holder, width, height, sample);

            DepthView = new DepthStencilView(holder.Device, Texture, new DepthStencilViewDescription {
                Flags = DepthStencilViewFlags.None,
                Format = GetDepthStencilViewFormat(Description.Format),
                Dimension = DepthStencilViewDimension.Texture2D,
                MipSlice = 0
            });

            View = new ShaderResourceView(holder.Device, Texture, new ShaderResourceViewDescription {
                Format = GetShaderResourceViewFormat(Description.Format),
                Dimension = ShaderResourceViewDimension.Texture2D,
                MipLevels = 1,
                MostDetailedMip = 0
            });
        }

        protected static Format GetDepthStencilViewFormat(Format texture) {
            switch (texture) {
                case Format.R16_Typeless:
                    return Format.R16_Float;
                case Format.R32_Typeless:
                    return Format.R32_Float;
                case Format.R24G8_Typeless:
                    return Format.D24_UNorm_S8_UInt;
                case Format.R32G8X24_Typeless:
                    return Format.R32_Float_X8X24_Typeless;
                default:
                    return texture;
            }
        }

        protected static Format GetShaderResourceViewFormat(Format texture) {
            switch (texture) {
                case Format.R16_Typeless:
                    return Format.R16_Typeless;
                case Format.R32_Typeless:
                    return Format.R16_Typeless;
                case Format.R24G8_Typeless:
                    return Format.R24_UNorm_X8_Typeless;
                case Format.R32G8X24_Typeless:
                    return Format.R32_Float_X8X24_Typeless;
                default:
                    return texture;
            }
        }

        public override void Dispose() {
            base.Dispose();

            if (DepthView != null) {
                DepthView.Dispose();
                DepthView = null;
            }
        }

        public static TargetResourceDepthTexture Create(Format format) {
            return new TargetResourceDepthTexture(new Texture2DDescription {
                ArraySize = 1,
                MipLevels = 1,
                Format = format,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
        }

        public static TargetResourceDepthTexture Create() {
            return Create(Format.R24G8_Typeless);

            /*
                ArraySize = 1,
                MipLevels = 1,
                Width = width,
                Height = height,
                SampleDescription = SampleDescription,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            */
        }
    }
}