using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public abstract class DarkAreaLightBase : DarkLightBase {
        protected DarkAreaLightBase(DarkLightType type) : base(type) {
            ShadowsAvailable = false;
            HighQualityShadowsAvailable = false;
            IsVisibleAsMesh = true;
        }

        private bool _visibleLight;

        public bool VisibleLight {
            get => _visibleLight;
            set {
                if (Equals(value, _visibleLight)) return;
                _visibleLight = value;
                ResetLightMesh();
                OnPropertyChanged();
            }
        }

        protected override void SerializeOverride(JObject obj) {
            base.SerializeOverride(obj);
            if (VisibleLight) {
                obj["visible"] = VisibleLight;
            }
        }

        protected override void DeserializeOverride(JObject obj) {
            base.DeserializeOverride(obj);
            VisibleLight = obj["visible"] != null && (bool)obj["visible"];
        }

        protected sealed override void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, IShadowsDraw shadowsDraw) {}

        protected sealed override void SetShadowOverride(out Vector4 size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar) {
            size = default(Vector4);
            matrix = Matrix.Identity;
            view = null;
        }

        public override void InvalidateShadows() {}

        protected override void DisposeOverride() {
            ResetLightMesh();

            _lightRenderableObject?.Clear();
            DisposeHelper.Dispose(ref _lightRenderableObject);
        }

        private VisibleLightObject _light;
        private RenderableList _lightRenderableObject;

        public override IRenderableObject GetRenderableObject() {
            if (_lightRenderableObject == null) {
                EnsureLightCreated();
                _lightRenderableObject = new RenderableList { _light };
            }

            return _lightRenderableObject;
        }

        private void EnsureLightCreated() {
            if (_light == null) {
                _light = CreateLightMesh();
                _light.ParentMatrix = Matrix.Identity;

                if (_lightRenderableObject != null) {
                    _lightRenderableObject.Clear();
                    _lightRenderableObject.Add(_light);
                }
            }
        }

        public override void DrawLight(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode) {
            if (!VisibleLight) return;

            EnsureLightCreated();
            _light.Transform = GetLightMeshTransformMatrix();// * Matrix.Scaling(Width, 1f, Height);
            _light.SetColor(LightMeshColor);
            _light.Draw(holder, camera, mode);
        }

        protected virtual Vector4 LightMeshColor => Color.ToVector4() * Brightness;

        protected void ResetLightMesh() {
            _lightRenderableObject?.Clear();
            DisposeHelper.Dispose(ref _light);
        }

        protected abstract Matrix GetLightMeshTransformMatrix();

        [NotNull]
        protected abstract VisibleLightObject CreateLightMesh();
    }

    public class VisibleLightMaterial : IRenderableMaterial {
        private EffectSpecialAreaLights _effect;

        internal VisibleLightMaterial() { }

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (_effect != null) return;
            _effect = contextHolder.GetEffect<EffectSpecialAreaLights>();
        }

        public void Refresh(IDeviceContextHolder contextHolder) {}

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.GBuffer) return false;

            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPC;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public Vector4? OverrideColor { get; set; }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.FxOverrideColor.Set(OverrideColor.HasValue);
            if (OverrideColor.HasValue) {
                _effect.FxCustomColor.Set(OverrideColor.Value);
            }

            (mode == SpecialRenderMode.GBuffer ? _effect.TechGPass : _effect.TechMain).DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public bool IsBlending => true;

        public string Name => null;

        public void Dispose() { }
    }

    public class VisibleLightObject : TrianglesRenderableObject<InputLayouts.VerticePC> {
        public VisibleLightObject([CanBeNull] string name, InputLayouts.VerticePC[] vertices, ushort[] indices) : base(name, vertices, indices) {
            _material = new VisibleLightMaterial();
        }

        private readonly VisibleLightMaterial _material;

        public Matrix Transform;

        protected override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);
            _material.EnsureInitialized(contextHolder);
        }

        public void SetColor(Vector4 color) {
            _material.OverrideColor = color;
        }

        protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!_material.Prepare(contextHolder, mode)) return;
            base.DrawOverride(contextHolder, camera, mode);
            _material.SetMatrices(Transform * ParentMatrix, camera);
            _material.Draw(contextHolder, Indices.Length, mode);
        }

        public override BaseRenderableObject Clone() {
            return new ClonedObject(this);
        }

        private class ClonedObject : TrianglesRenderableObject<InputLayouts.VerticePC> {
            private readonly VisibleLightObject _original;

            internal ClonedObject(VisibleLightObject original) : base(original.Name + "_copy", original.Vertices, original.Indices) {
                _original = original;
            }

            public override bool IsEnabled => _original.IsEnabled;

            public override bool IsReflectable => _original.IsReflectable;

            protected override void Initialize(IDeviceContextHolder contextHolder) {
                base.Initialize(contextHolder);
                _original.Draw(contextHolder, null, SpecialRenderMode.InitializeOnly);
            }

            protected override void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
                if (!_original._material.Prepare(contextHolder, mode)) return;
                base.DrawOverride(contextHolder, camera, mode);
                _original._material.SetMatrices(_original.Transform * ParentMatrix, camera);
                _original._material.Draw(contextHolder, Indices.Length, mode);
            }

            public override BaseRenderableObject Clone() {
                return new ClonedObject(_original);
            }
        }
    }
}