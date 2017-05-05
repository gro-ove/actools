using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public class DarkPointLight : DarkLightBase {
        public float Range = 2f;
        private ShadowsPoint _shadows;
        private bool _shadowsCleared;

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {
            if (_shadows == null) {
                _shadows = new ShadowsPoint(ShadowsResolution);
                _shadows.Initialize(holder);
            }

            if (shadowsDraw != null) {
                _shadowsCleared = false;
                _shadows.SetMapSize(holder, ShadowsResolution);
                if (_shadows.Update(Position, Range)) {
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
                matrix = Matrix.Identity;
                view = _shadows.View;

                var n = _shadows.NearZValue;
                var f = _shadows.FarZValue;
                nearFar.X = n;
                nearFar.Y = f;
                nearFar.Z = (f + n) / (f - n);
                nearFar.W = 2f * f * n / (f - n);
            }
        }

        protected override void SetOverride(ref EffectDarkMaterial.Light light) {
            base.SetOverride(ref light);
            light.PosW = Position;
            light.Range = Range;
            light.Type = EffectDarkMaterial.LightPoint;
        }

        public override void InvalidateShadows() {
            _shadows?.Invalidate();
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _shadows);
        }

        public override void Rotate(Quaternion delta) {}

        protected override MoveableHelper CreateMoveableHelper() {
            return new MoveableHelper(this, MoveableRotationAxis.None);
        }

        private RenderableList _dummy;

        public override void DrawDummy(IDeviceContextHolder holder, ICamera camera) {
            if (_dummy == null) {
                _dummy = new RenderableList {
                    DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitY, new Color4(1f, 1f, 1f, 0f), 20, 0.08f),
                    DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitX, new Color4(1f, 1f, 1f, 0f), 20, 0.08f),
                    DebugLinesObject.GetLinesCircle(Matrix.Identity, Vector3.UnitZ, new Color4(1f, 1f, 1f, 0f), 20, 0.08f),
                };
            }

            _dummy.ParentMatrix = Matrix.Translation(Position);
            _dummy.Draw(holder, camera, SpecialRenderMode.Simple);
        }
    }
}