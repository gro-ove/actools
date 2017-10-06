using System;
using System.Drawing;
using System.Linq;
using AcTools.Render.Base.TargetTextures;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForward {
    // some common texture transformations for PaintShop
    public partial class ToolsKn5ObjectRenderer {
        public bool IsHdrTexture(ShaderResourceView view, Size size) {
            using (var maxColor = GetLimits(view, size, ((Texture2D)view.Resource).Description.Format)) {
                var texture = maxColor.Texture;
                using (var copy = new Texture2D(Device, new Texture2DDescription {
                    SampleDescription = new SampleDescription(1, 0),
                    Width = texture.Description.Width,
                    Height = texture.Description.Height,
                    ArraySize = texture.Description.ArraySize,
                    MipLevels = texture.Description.MipLevels,
                    Format = texture.Description.Format,
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CpuAccessFlags = CpuAccessFlags.Read
                })) {
                    Device.ImmediateContext.CopyResource(texture, copy);
                    var rect = Device.ImmediateContext.MapSubresource(copy, 0, MapMode.Read, SlimDX.Direct3D11.MapFlags.None);
                    try {
                        using (var b = new ReadAheadBinaryReader(rect.Data)) {
                            var c = b.ReadSingle4D();
                            AcToolsLogging.Write("Values: " + c.JoinToString("; ") + "; format: " + texture.Description.Format);
                            return c.Any(x => x < 0f || x > 1f);
                        }
                    } finally {
                        Device.ImmediateContext.UnmapSubresource(texture, 0);
                    }
                }
            }
        }

        private TargetResourceTexture GetLimits(ShaderResourceView view, Size size, Format? format = null) {
            TargetResourceTexture result = null;
            for (var i = Math.Max(size.Width, size.Height) / 4; i >= 1 || result == null; i /= 4) {
                if (i < 1) i = 1;

                var current = result;
                result = TargetResourceTexture.Create(format ?? Format.R8G8B8A8_UNorm);
                result.Resize(DeviceContextHolder, i, i, null);

                var j = i;
                var input = current?.View ?? view;
                UseEffect(e => {
                    e.FxSize.Set(new Vector4(j, j, 1f / j, 1f / j));
                    e.FxInputMap.SetResource(input);
                    (current == null ? e.TechFindLimitsFirstStep : e.TechFindLimits).DrawAllPasses(DeviceContext, 6);
                }, result);

                current?.Dispose();
            }

            return result;
        }

        private ShaderResourceView Normalize(ShaderResourceView view, Size size) {
            var max = Math.Max(size.Width, size.Height);
            if (max == 1) {
                using (var temporary = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                    temporary.Resize(DeviceContextHolder, 1, 1, null);
                    UseEffect(e => {
                        e.FxInputMap.SetResource(view);
                        e.FxOverlayMap.SetResource(view);
                        e.TechNormalizeLimits.DrawAllPasses(DeviceContext, 6);
                    }, temporary);

                    temporary.KeepView = true;
                    return temporary.View;
                }
            }

            using (var maxColor = GetLimits(view, size))
            using (var temporary = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                temporary.Resize(DeviceContextHolder, size.Width, size.Height, null);

                UseEffect(e => {
                    e.FxInputMap.SetResource(view);
                    e.FxOverlayMap.SetResource(maxColor.View);
                    e.TechNormalizeLimits.DrawAllPasses(DeviceContext, 6);
                }, temporary);

                temporary.KeepView = true;
                return temporary.View;
            }
        }

        private ShaderResourceView Desaturate(ShaderResourceView view, Size size) {
            using (var temporary = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                temporary.Resize(DeviceContextHolder, size.Width, size.Height, null);
                UseEffect(e => {
                    e.FxInputMap.SetResource(view);
                    e.TechDesaturate.DrawAllPasses(DeviceContext, 6);
                }, temporary);
                temporary.KeepView = true;
                return temporary.View;
            }
        }
    }
}