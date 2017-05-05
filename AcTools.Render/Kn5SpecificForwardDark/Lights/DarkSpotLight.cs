using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public class DarkSpotLight : DarkLightBase {
        public Vector3 Direction = new Vector3(-1f, -1f, -1f);
        public float Angle = MathF.PI / 6;
        public float SpotFocus = 0.5f;
        public float Range = 2f;
        private ShadowsSpot _shadows;
        private bool _shadowsCleared;

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {
            if (_shadows == null) {
                _shadows = new ShadowsSpot(ShadowsResolution);
                _shadows.Initialize(holder);
            }

            if (shadowsDraw != null) {
                _shadowsCleared = false;
                _shadows.SetMapSize(holder, ShadowsResolution);
                if (_shadows.Update(Position, -Direction, Range, Angle * 2f)) {
                    _shadows.DrawScene(holder, shadowsDraw);
                }
            } else if (!_shadowsCleared) {
                _shadowsCleared = true;
                _shadows.Clear(holder);
            }
        }

        protected override void SetShadowOverride(out float size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar) {
            if (_shadows == null) {
                size = 0;
                matrix = Matrix.Identity;
                view = null;
            } else {
                size = 1f / _shadows.MapSize;
                matrix = _shadows.ShadowTransform;
                view = _shadows.View;
            }
        }

        protected override void SetOverride(ref EffectDarkMaterial.Light light) {
            base.SetOverride(ref light);
            light.DirectionW = Vector3.Normalize(Direction);
            light.PosW = Position;
            light.Range = Range;
            light.SpotlightCosMin = Angle.Cos();
            light.SpotlightCosMax = light.SpotlightCosMin.Lerp(1f, SpotFocus);
            //light.SpotlightAngle = Angle;
            /*light.LightParams.X = Range;
            light.LightParams.Y = Angle.Cos();
            light.LightParams.Z = light.LightParams.Y.Lerp(1f, SpotFocus);*/
            //light.LightParams = new Vector4(Range, Angle.Cos(), Angle.Cos().Lerp(1f, SpotFocus), 0f);
            light.Type = EffectDarkMaterial.LightSpot;
        }

        public override void InvalidateShadows() {
            _shadows?.Invalidate();
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _shadows);
        }

        public override void Rotate(Quaternion delta) {
            if (!IsMovable) return;
            Direction = Vector3.TransformNormal(Direction, Matrix.RotationQuaternion(delta));
        }

        private RenderableList _dummy;

        public override void DrawDummy(IDeviceContextHolder holder, ICamera camera) {
            if (_dummy == null) {
                _dummy = new RenderableList {
                    DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitY, new Color4(1f, 1f, 1f, 0f), 20, 0.04f),
                    DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 20, 0.04f),
                    DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitZ, new Color4(1f, 1f, 1f, 0f), 20, 0.04f),
                    DebugLinesObject.GetLinesArrow(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 0.12f)
                };
            }

            _dummy.ParentMatrix = Position.LookAtMatrixXAxis(Position - Direction, Vector3.UnitY);
            _dummy.Draw(holder, camera, SpecialRenderMode.Simple);
        }
    }
}