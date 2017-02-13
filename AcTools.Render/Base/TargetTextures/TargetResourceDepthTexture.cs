using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.TargetTextures {
    public class TargetResourceDepthTexture : BaseTargetResourceTexture {
        public DepthStencilView DepthView { get; private set; }

        private TargetResourceDepthTexture(Texture2DDescription description) : base(description) { }

        public override void Resize(DeviceContextHolder holder, int width, int height, SampleDescription? sample) {
            if (width == Width && height == Height && (!sample.HasValue || Description.SampleDescription == sample.Value)) return;
            base.Resize(holder, width, height, sample);

            DepthView = new DepthStencilView(holder.Device, Texture, new DepthStencilViewDescription {
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

            if (DepthView != null) {
                DepthView.Dispose();
                DepthView = null;
            }
        }

        public static TargetResourceDepthTexture Create() {
            return new TargetResourceDepthTexture(new Texture2DDescription {
                ArraySize = 1,
                MipLevels = 1,
                Format = Format.R24G8_Typeless,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

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