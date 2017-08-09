using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        public AmbientShadow AmbientShadowNode;
        private readonly float _shadowsHeight;
        private readonly bool _asyncOverrideTexturesLoading;
        private Vector3 _ambientShadowSize;
        private string _currentSkin;

        private IRenderableObject LoadBodyAmbientShadow() {
            AmbientShadowNode = new AmbientShadow("body_shadow.png", Matrix.Identity);
            ResetAmbientShadowSize();
            return AmbientShadowNode;
        }

        public Vector3 AmbientShadowSize {
            get => _ambientShadowSize;
            set {
                if (Equals(value, _ambientShadowSize)) return;
                _ambientShadowSize = value;

                if (AmbientShadowNode != null) {
                    AmbientShadowNode.Transform = Matrix.Scaling(AmbientShadowSize) * Matrix.RotationY(MathF.PI) *
                            Matrix.Translation(0f, _shadowsHeight, 0f);
                }
            }
        }

        public void FitAmbientShadowSize() {
            if (!RootObject.BoundingBox.HasValue) return;
            var size = RootObject.BoundingBox.Value;
            AmbientShadowSize = new Vector3(Math.Max(-size.Minimum.X, size.Maximum.X) * 1.1f, 1.0f, Math.Max(-size.Minimum.Z, size.Maximum.Z) * 1.1f);
        }

        public void ResetAmbientShadowSize() {
            AmbientShadowSize = _carData.GetBodyShadowSize();
        }

        private AmbientShadow LoadWheelAmbientShadow(string nodeName, string textureName) {
            var node = GetDummyByName(nodeName);
            return node == null ? null : new AmbientShadow(textureName, GetWheelAmbientShadowMatrix(node));
        }

        public IReadOnlyList<IRenderableObject> GetAmbientShadows() {
            return _ambientShadows;
        }

        public ShaderResourceView GetAmbientShadowView(IDeviceContextHolder holder, AmbientShadow shadow) {
            if (_ambientShadowsTextures == null) {
                InitializeAmbientShadows(holder);
            }

            return shadow.GetView(_ambientShadowsHolder);
        }

        private AmbientShadow _wheelLfShadow;
        private AmbientShadow _wheelRfShadow;
        private AmbientShadow _wheelLrShadow;
        private AmbientShadow _wheelRrShadow;

        private void UpdateFrontWheelsShadowsRotation() {
            if (_wheelLfShadow != null) {
                var node = GetDummyByName("WHEEL_LF");
                if (node != null) {
                    _wheelLfShadow.Transform = GetWheelAmbientShadowMatrix(node);
                }
            }

            if (_wheelRfShadow != null) {
                var node = GetDummyByName("WHEEL_RF");
                if (node != null) {
                    _wheelRfShadow.Transform = GetWheelAmbientShadowMatrix(node);
                }
            }
        }

        private void UpdateRearWheelsShadowsRotation() {
            if (_wheelLrShadow != null) {
                var node = GetDummyByName("WHEEL_LR");
                if (node != null) {
                    _wheelLrShadow.Transform = GetWheelAmbientShadowMatrix(node);
                }
            }

            if (_wheelRrShadow != null) {
                var node = GetDummyByName("WHEEL_RR");
                if (node != null) {
                    _wheelRrShadow.Transform = GetWheelAmbientShadowMatrix(node);
                }
            }
        }

        private IEnumerable<IRenderableObject> LoadAmbientShadows() {
            return _carData.IsEmpty ? new IRenderableObject[0] : new[] {
                LoadBodyAmbientShadow(),
                _wheelLfShadow = LoadWheelAmbientShadow("WHEEL_LF", "tyre_0_shadow.png"),
                _wheelRfShadow = LoadWheelAmbientShadow("WHEEL_RF", "tyre_1_shadow.png"),
                _wheelLrShadow = LoadWheelAmbientShadow("WHEEL_LR", "tyre_2_shadow.png"),
                _wheelRrShadow = LoadWheelAmbientShadow("WHEEL_RR", "tyre_3_shadow.png")
            }.Where(x => x != null);
        }
    }
}