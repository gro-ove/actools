using System.Drawing;
using System.Runtime.CompilerServices;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Shaders;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.Utils {
    public class TextureReader : BaseRenderer {
        public TextureReader() {
            Initialize();
        }

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        protected override void InitializeInner() {}

        protected override void ResizeInner() {}

        protected override void OnTick(float dt) { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public byte[] ToPngNoFormat(byte[] bytes, bool ignoreAlpha = false, Size? downsize = null) {
            return ToPng(bytes, ignoreAlpha, downsize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public byte[] ToPng(byte[] bytes, bool ignoreAlpha = false, Size? downsize = null) {
            Format format;
            return ToPng(DeviceContextHolder, bytes, ignoreAlpha, downsize, out format);
        }

        public byte[] ToPng(byte[] bytes, bool ignoreAlpha, out Format format) {
            return ToPng(DeviceContextHolder, bytes, ignoreAlpha, null, out format);
        }

        public byte[] ToPng(byte[] bytes, bool ignoreAlpha, Size? downsize, out Format format) {
            return ToPng(DeviceContextHolder, bytes, ignoreAlpha, downsize, out format);
        }

        public static byte[] ToPng(DeviceContextHolder holder, byte[] bytes, bool ignoreAlpha = false, Size? downsize = null) {
            Format format;
            return ToPng(holder, bytes, ignoreAlpha, downsize, out format);
        }

        public static byte[] ToPng(DeviceContextHolder holder, byte[] bytes, bool ignoreAlpha, out Format format) {
            return ToPng(holder, bytes, ignoreAlpha, null, out format);
        }

        public static byte[] ToPng(DeviceContextHolder holder, byte[] bytes, bool ignoreAlpha, Size? downsize, out Format format) {
            Viewport[] viewports = null;

            try {
                using (var stream = new System.IO.MemoryStream())
                using (var effect = new EffectPpBasic())
                using (var output = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm))
                using (var resource = ShaderResourceView.FromMemory(holder.Device, bytes)) {
                    var texture = (Texture2D)resource.Resource;
                    var loaded = texture.Description;
                    effect.Initialize(holder.Device);

                    format = loaded.Format;
                    output.Resize(holder, downsize?.Width ?? loaded.Width, downsize?.Height ?? loaded.Height, null);

                    holder.DeviceContext.ClearRenderTargetView(output.TargetView, Color.Transparent);
                    holder.DeviceContext.OutputMerger.SetTargets(output.TargetView);

                    viewports = holder.DeviceContext.Rasterizer.GetViewports();
                    holder.DeviceContext.Rasterizer.SetViewports(new Viewport(0, 0, loaded.Width, loaded.Height, 0.0f, 1.0f));

                    holder.DeviceContext.OutputMerger.BlendState = null;
                    holder.QuadBuffers.Prepare(holder.DeviceContext, effect.LayoutPT);

                    effect.FxInputMap.SetResource(resource);
                    holder.PrepareQuad(effect.LayoutPT);

                    if (ignoreAlpha) {
                        effect.TechCopyNoAlpha.DrawAllPasses(holder.DeviceContext, 6);
                    } else {
                        effect.TechCopy.DrawAllPasses(holder.DeviceContext, 6);
                    }

                    Texture2D.ToStream(holder.DeviceContext, output.Texture, ImageFileFormat.Png, stream);
                    stream.Position = 0;
                    return stream.GetBuffer();
                }
            } finally {
                if (viewports != null) {
                    holder.DeviceContext.Rasterizer.SetViewports(viewports);
                }
            }
        }
    }
}