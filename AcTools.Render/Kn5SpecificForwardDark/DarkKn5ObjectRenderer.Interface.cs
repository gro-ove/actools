using System;
using System.Linq;
using AcTools.Render.Forward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public partial class DarkKn5ObjectRenderer {
        protected override void DrawSpritesInner() {
            if (_complexMode && ShowMovementArrows) {
                for (var i = _lights.Length - 1; i >= 0; i--) {
                    var light = _lights[i];
                    if (light.Enabled) {
                        light.DrawSprites(Sprite, Camera, new Vector2(ActualWidth, ActualHeight));
                    }
                }
            }

            base.DrawSpritesInner();
        }

        protected override string GetInformationString() {
            var aa = new[] {
                UseMsaa ? MsaaSampleCount + "xMSAA" : null,
                UseSsaa ? $"{Math.Pow(ResolutionMultiplier, 2d).Round()}xSSAA" : null,
                UseFxaa ? "FXAA" : null,
            }.NonNull().JoinToString(", ");

            var se = new[] {
                UseDof ? UseAccumulationDof ? "Acc. DOF" : "DOF" : null,
                UseSslr ? "SSLR" : null,
                UseAo ? AoType.GetDescription() : null,
                UseBloom ? "HDR" : null,
            }.NonNull().JoinToString(", ");

            var pp = new[] {
                ToneMapping != ToneMappingFn.None ? "Tone Mapping" : null,
                UseColorGrading && ColorGradingData != null ? "Color Grading" : null
            }.NonNull().JoinToString(", ");

            if (ToneMapping != ToneMappingFn.None) {
                pp += $"\r\nTone Mapping Func.: {ToneMapping.GetDescription()}";
                pp += $"\r\nExp./Gamma/White P.: {ToneExposure:F2}, {ToneGamma:F2}, {ToneWhitePoint:F2}";
            }

            return CarNode?.DebugString ?? $@"
FPS: {FramesPerSecond:F0}{(SyncInterval ? " (limited)" : "")} ({Width}×{Height})
Triangles: {CarNode?.TrianglesCount:D}
AA: {(string.IsNullOrEmpty(aa) ? "None" : aa)}
Shadows: {(EnableShadows ? $"{(UsePcss ? "Yes, PCSS" : "Yes")} ({ShadowMapSize})" : "No")}
Effects: {(string.IsNullOrEmpty(se) ? "None" : se)}
Color: {(string.IsNullOrWhiteSpace(pp) ? "Original" : pp)}
Shaders set: {_darkMode}
Lights: {_lights.Count(x => x.ActuallyEnabled)} (shadows: {(EnableShadows ? 1 + _lights.Count(x => x.ActuallyEnabled && x.ShadowsActive) : 0)})
Skin editing: {(ImageUtils.IsMagickSupported ? MagickOverride ? "Magick.NET av., enabled" : "Magick.NET av., disabled" : "Magick.NET not available")}".Trim();
        }
    }
}