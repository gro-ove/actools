using System.IO;
using AcTools.Render.Base.Sprites;
using AcTools.Render.Deferred.Kn5Specific.Materials;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.DirectWrite;
using FontStyle = SlimDX.DirectWrite.FontStyle;

namespace AcTools.Render.Deferred {
    public abstract class StatsDeferredShadingRenderer : DeferredShadingRenderer {
        private TextBlockRenderer _textBlock;

        public bool VisibleUi = true;

        protected override void ResizeInner() {
            base.ResizeInner();

            if (_textBlock != null) return;
            _textBlock = new TextBlockRenderer(Sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
        }

        protected override void DrawSpritesInner() {
            if (VisibleUi) {
                _textBlock.DrawString($@"
FPS: {FramesPerSecond:F1}{(SyncInterval ? " (limited)" : "")}
Mode: {Mode}
KN5 objs.: {Kn5MaterialDeferred.Drawed}
SSLR: {(!UseLocalReflections ? "No" : BlurLocalReflections ? "Yes, blurred" : "Yes")}
Cubemap refl.: {(UseCubemapReflections ? "Yes" : "No")}
Shadows: {(!UseShadows ? "No" : UseDebugShadows ? "Debug" : UseShadowsFilter ? "With Filtering" : "Yes")}
FXAA: {(!UseFxaa ? "No" : UseExperimentalSmaa ? "No, SMAA" : UseExperimentalFxaa ? "Experim." : "Yes")}
Lights: {(Lights.Count > 0 ? Lights.Count.ToString() : "")}".Trim(),
                        new Vector2(Width - 300, 20), 16f, new Color4(1.0f, 1.0f, 1.0f),
                        CoordinateType.Absolute);
            }

            Kn5MaterialDeferred.Drawed = 0;
        }

        public override void Shot(double multiplier, double downscale, Stream outputStream, bool lossless) {
            var visibleUi = VisibleUi;
            VisibleUi = false;

            try {
                base.Shot(multiplier, downscale, outputStream, lossless);
            } finally {
                VisibleUi = visibleUi;
            }
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _textBlock);
            base.DisposeOverride();
        }
    }
}