using System;
using System.Drawing;
using AcTools.Render.Base.TargetTextures;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForward {
    // some common texture transformations for PaintShop
    public partial class ToolsKn5ObjectRenderer {
        private ShaderResourceView NormalizeMax(ShaderResourceView view, Size size) {
            var max = Math.Max(size.Width, size.Height);
            if (max == 1) {
                using (var temporary = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                    temporary.Resize(DeviceContextHolder, 1, 1, null);
                    UseEffect(e => {
                        e.FxInputMap.SetResource(view);
                        e.FxOverlayMap.SetResource(view);
                        e.TechMaximumApply.DrawAllPasses(DeviceContext, 6);
                    }, temporary);

                    temporary.KeepView = true;
                    return temporary.View;
                }
            }

            var originalView = view;
            for (var i = max / 4; i > 1 || ReferenceEquals(view, originalView); i /= 4) {
                if (i < 1) i = 1;

                using (var temporary = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                    temporary.Resize(DeviceContextHolder, i, i, null);
                    UseEffect(e => {
                        e.FxInputMap.SetResource(view);
                        e.TechMaximum.DrawAllPasses(DeviceContext, 6);
                    }, temporary);

                    if (!ReferenceEquals(view, originalView)) {
                        view.Dispose();
                    }

                    view = temporary.View;
                    temporary.KeepView = true;
                }
            }

            using (var temporary = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm)) {
                temporary.Resize(DeviceContextHolder, size.Width, size.Height, null);

                UseEffect(e => {
                    e.FxInputMap.SetResource(originalView);
                    e.FxOverlayMap.SetResource(view);
                    e.TechMaximumApply.DrawAllPasses(DeviceContext, 6);
                }, temporary);

                view.Dispose();

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