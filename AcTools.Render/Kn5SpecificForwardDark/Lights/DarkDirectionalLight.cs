using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public class DarkDirectionalLight : DarkLightBase {
        public Vector3 Direction = new Vector3(-1f, -1f, -1f);
        private ShadowsDirectional _shadows;
        private bool _shadowsCleared;
        private float _shadowsSize = 50f;

        public void SetShadowsSize(DeviceContextHolder holder, float size) {
            _shadowsSize = size;
            _shadows?.SetSplits(holder, new []{ _shadowsSize });
        }

        protected override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {
            if (_shadows == null) {
                _shadows = new ShadowsDirectional(ShadowsResolution, new[] { _shadowsSize });
                _shadows.Initialize(holder);
            }

            if (shadowsDraw != null) {
                _shadowsCleared = false;
                _shadows.SetMapSize(holder, ShadowsResolution);
                if (_shadows.Update(-Direction, shadowsPosition)) {
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
                matrix = _shadows.Splits[0].ShadowTransform;
                view = _shadows.Splits[0].View;
            }
        }

        protected override void SetOverride(ref EffectDarkMaterial.Light light) {
            base.SetOverride(ref light);
            light.DirectionW = Vector3.Normalize(Direction);
            light.Type = EffectDarkMaterial.LightDirectional;
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