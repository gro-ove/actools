using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.TargetTextures {
    public class TargetResourceDepthTexture : BaseTargetResourceTexture {
        public DepthStencilView StencilView { get; private set; }

        public TargetResourceDepthTexture(Texture2DDescription description) : base(description) { }

        public override void Resize(DeviceContextHolder holder, int width, int height) {
            base.Resize(holder, width, height);

            StencilView = new DepthStencilView(holder.Device, Texture, new DepthStencilViewDescription {
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

            if (StencilView != null) {
                StencilView.Dispose();
                StencilView = null;
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