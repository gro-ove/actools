using System.Drawing;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5SpecificDeferred.Materials;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.DirectWrite;
using SpriteTextRenderer;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using TextBlockRenderer = SpriteTextRenderer.SlimDX.TextBlockRenderer;

namespace AcTools.Render.DeferredShading {
    public abstract class StatsDeferredShadingRenderer : DeferredShadingRenderer {
        private TextBlockRenderer _textBlock;

        public bool VisibleUi = true;

        protected override void ResizeInner() {
            base.ResizeInner();

            if (_textBlock != null) return;
            _textBlock = new TextBlockRenderer(Sprite, "Consolas", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
        }

        protected override void DrawSpritesInner() {
            if (VisibleUi) {
                _textBlock.DrawString($@"
FPS:            {FramesPerSecond:F1}{(SyncInterval ? " (limited)" : "")}
Mode:           {Mode}
KN5 objs.:      {Kn5MaterialDeferred.Drawed}
SSLR:           {(!UseLocalReflections ? "No" : BlurLocalReflections ? "Yes, blurred" : "Yes")}
Cubemap refl.:  {(UseCubemapReflections ? "Yes" : "No")}
Shadows:        {(!UseShadows ? "No" : UseDebugShadows ? "Debug" : UseShadowsFilter ? "With Filtering" : "Yes")}
FXAA:           {(!UseFxaa ? "No" : UseExperimentalSmaa ? "No, SMAA" : UseExperimentalFxaa ? "Experim." : "Yes")}
Lights:         {(Lights.Count > 0 ? Lights.Count.ToString() : "")}".Trim(),
                        new Vector2(Width - 300, 20), 16f, new Color4(1.0f, 1.0f, 1.0f),
                        CoordinateType.Absolute);
            }

            Kn5MaterialDeferred.Drawed = 0;
        }

        public override Image Shot(int multipler) {
            var visibleUi = VisibleUi;
            VisibleUi = false;

            try {
                return base.Shot(multipler);
            } finally {
                VisibleUi = visibleUi;
            }
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _textBlock);
            base.Dispose();
        }
    }
}