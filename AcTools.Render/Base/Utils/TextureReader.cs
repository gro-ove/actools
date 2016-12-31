using System.Drawing;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Shaders;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.Utils {
    public static class TextureReader {
        public static byte[] ToPng(DeviceContextHolder holder, byte[] bytes, bool ignoreAlpha, out Format format) {
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
                    output.Resize(holder, loaded.Width, loaded.Height);

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