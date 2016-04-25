using AcTools.Render.Kn5Specific.Materials;
using SlimDX;
using SlimDX.DirectWrite;
using SpriteTextRenderer;
using TextBlockRenderer = SpriteTextRenderer.SlimDX.TextBlockRenderer;

namespace AcTools.Render.DeferredShading {
    public abstract class SpriteDeferredShadingRenderer : DeferredShadingRenderer {
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
KN5 objs.:      {Kn5RenderableMaterial.Drawed}
SSLR:           {(UseLocalReflections ? "Yes" : "No")}
Cubemap refl.:  {(UseCubemapReflections ? "Yes" : "No")}
Shadows:        {(!UseShadows ? "No" : UseDebugShadows ? "Debug" : UseShadowsFilter ? "With Filtering" : "Yes")}
FXAA:           { (!UseFxaa ? "No" : UseExperimentalSmaa ? "No, SMAA" : UseExperimentalFxaa ? "Experim." : "Yes")}".Trim(),
                        new Vector2(Width - 300, 20), 16f, new Color4(1.0f, 1.0f, 1.0f),
                        CoordinateType.Absolute);
            }

            Kn5RenderableMaterial.Drawed = 0;
        }
    }
}