using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Objects {
    public interface IKn5Model {
        [CanBeNull]
        IKn5RenderableObject GetNodeByName([NotNull] string name);

        Kn5RenderableList GetDummyByName([NotNull] string name);
    }

    /* Despite holding references to SharedMaterials and ITexturesProvider, this
     * thing doesn’t need to be disposed — those references are managed outside,
     * and this thing is just a relatively convinient way to pass them down
     * rendering tree */

    public class Kn5LocalDeviceContextHolder : IDeviceContextHolder {
        private readonly IDeviceContextHolder _mainHolder;
        private readonly SharedMaterials _sharedMaterials;
        private readonly ITexturesProvider _texturesProvider;
        private readonly IKn5Model _model;

        public Kn5LocalDeviceContextHolder(IDeviceContextHolder mainHolder, SharedMaterials sharedMaterials, ITexturesProvider texturesProvider,
                IKn5Model model) {
            _mainHolder = mainHolder;
            _sharedMaterials = sharedMaterials;
            _texturesProvider = texturesProvider;
            _model = model;
        }

        public Device Device => _mainHolder.Device;

        public DeviceContext DeviceContext => _mainHolder.DeviceContext;

        public void Set<T>(T obj) where T : class {
            _mainHolder.Set(obj);
        }

        public T Get<T>() where T : class {
            var result = TryToGet<T>();
            if (result == null) throw new Exception($"Entry with type {typeof(T)} not found");
            return result;
        }

        public T TryToGet<T>() where T : class {
            if (typeof(T) == typeof(ITexturesProvider)) {
                return _texturesProvider as T;
            }

            if (typeof(T) == typeof(SharedMaterials)) {
                return _sharedMaterials as T;
            }

            if (typeof(T) == typeof(IKn5Model)) {
                return _model as T;
            }

            return _mainHolder.TryToGet<T>();
        }

        public T GetEffect<T>() where T : IEffectWrapper, new() {
            return _mainHolder.GetEffect<T>();
        }

        public IRenderableMaterial GetMaterial(object key) {
            return _sharedMaterials.GetMaterial(key);
        }

        public CommonStates States => _mainHolder.States;

        public void RaiseUpdateRequired() {
            _mainHolder.RaiseUpdateRequired();
        }

        public void RaiseSceneUpdated() {
            _mainHolder.RaiseSceneUpdated();
        }

        public void RaiseTexturesUpdated() {
            _mainHolder.RaiseTexturesUpdated();
        }

        public ShaderResourceView GetRandomTexture(int width, int height) {
            return _mainHolder.GetRandomTexture(width, height);
        }

        public ShaderResourceView GetFlatNmTexture() {
            return _mainHolder.GetFlatNmTexture();
        }

        public double LastFrameTime => _mainHolder.LastFrameTime;

        public RendererStopwatch StartNewStopwatch() {
            return _mainHolder.StartNewStopwatch();
        }

        public float TimeFactor {
            get => _mainHolder.TimeFactor;
            set {}
        }
    }

    public class Kn5RenderableFile : RenderableList, IKn5Model {
        // protected readonly bool AllowSkinnedObjects;
        protected readonly bool AsyncTexturesLoading;

        [NotNull]
        public readonly IKn5 OriginalFile;

        private List<Kn5RenderableList> _dummies;
        private List<IKn5RenderableObject> _meshes;

        private RenderableList _rootObject;

        [NotNull]
        public RenderableList RootObject {
            get => _rootObject;
            protected set {
                if (_rootObject != null) {
                    _rootObject.MatrixChanged -= OnRootObjectMatrixChanged;
                }

                _dummies = null;
                _rootObject = value;

                InvalidateModelMatrixInverted();
                if (_rootObject != null) {
                    _rootObject.MatrixChanged += OnRootObjectMatrixChanged;
                }
            }
        }

        public List<Kn5RenderableList> Dummies => _dummies ?? (_dummies = RootObject.GetAllChildren().OfType<Kn5RenderableList>().ToList());

        public List<IKn5RenderableObject> Meshes => _meshes ?? (_meshes = RootObject.GetAllChildren().OfType<IKn5RenderableObject>().ToList());

        public Kn5RenderableFile([NotNull] IKn5 kn5, Matrix matrix, bool asyncTexturesLoading = true, IKn5ToRenderableConverter converter = null) : base(kn5.OriginalFilename, matrix) {
            // AllowSkinnedObjects = allowSkinnedObjects;
            OriginalFile = kn5;
            AsyncTexturesLoading = asyncTexturesLoading;

            var obj = (converter ?? Kn5ToRenderableSimpleConverter.Instance).Convert(kn5.RootNode);
            RootObject = obj as RenderableList ?? new RenderableList { obj };
            Add(RootObject);
        }

        private void OnRootObjectMatrixChanged(object sender, EventArgs e) {
            InvalidateModelMatrixInverted();
        }

        private bool _modelMatrixInvertedDirty;

        protected void InvalidateModelMatrixInverted() {
            _modelMatrixInvertedDirty = true;
        }

        public static void UpdateModelMatrixInverted(RenderableList root) {
            var inverted = Matrix.Invert(root.Matrix);
            foreach (var dummy in root.GetAllChildren().OfType<Kn5RenderableList>()) {
                dummy.ModelMatrixInverted = inverted;
            }

            foreach (var dummy in root.GetAllChildren().OfType<IKn5RenderableObject>()) {
                dummy.ModelMatrixInverted = inverted;
            }
        }

        public void UpdateModelMatrixInverted() {
            if (!_modelMatrixInvertedDirty) return;
            _modelMatrixInvertedDirty = false;

            var inverted = Matrix.Invert(RootObject.Matrix);
            foreach (var dummy in Dummies) {
                dummy.ModelMatrixInverted = inverted;
            }

            foreach (var dummy in Meshes) {
                dummy.ModelMatrixInverted = inverted;
            }
        }

        protected Kn5SharedMaterials SharedMaterials;
        protected ITexturesProvider TexturesProvider;
        protected IDeviceContextHolder LocalHolder;

        protected virtual Kn5SharedMaterials InitializeMaterials(IDeviceContextHolder contextHolder) {
            return new Kn5SharedMaterials(contextHolder, OriginalFile);
        }

        protected virtual ITexturesProvider InitializeTextures(IDeviceContextHolder contextHolder) {
            return new Kn5TexturesProvider(OriginalFile, AsyncTexturesLoading);
        }

        protected IDeviceContextHolder InitializeLocalHolder(IDeviceContextHolder contextHolder) {
            return new Kn5LocalDeviceContextHolder(contextHolder, SharedMaterials, TexturesProvider, this);
        }

        public void RefreshMaterial(DeviceContextHolder contextHolder, uint materialId) {
            EnsureInitialized(contextHolder);
            SharedMaterials.GetMaterial(materialId).Refresh(LocalHolder);
        }

        private void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (LocalHolder == null) {
                SharedMaterials = InitializeMaterials(contextHolder);
                TexturesProvider = InitializeTextures(contextHolder);
                LocalHolder = InitializeLocalHolder(contextHolder);
                if (_debugMode) {
                    SetDebugMode(true);
                }
            }
        }

        protected void DrawInitialize(IDeviceContextHolder contextHolder) {
            UpdateModelMatrixInverted();
            EnsureInitialized(contextHolder);
        }

        private bool _debugMode;

        public virtual bool DebugMode {
            get => _debugMode;
            set {
                if (Equals(value, _debugMode)) return;
                _debugMode = value;
                if (LocalHolder == null) return;
                SetDebugMode(value);
            }
        }

        private void SetDebugMode(bool value) {
            foreach (var node in this.GetAllChildren().OfType<IKn5RenderableObject>()) {
                node.SetDebugMode(LocalHolder, value);
            }
        }

        /// <summary>
        /// Despite being a list, only RootObject is drawn!
        /// </summary>
        public override void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            DrawInitialize(holder);
            RootObject.Draw(LocalHolder, camera, mode, filter);
        }

        public override void Dispose() {
            base.Dispose();
            DisposeHelper.Dispose(ref SharedMaterials);
            DisposeHelper.Dispose(ref TexturesProvider);
        }

        public virtual IKn5RenderableObject GetNodeByName(string name) {
            return this.GetByName(name);
        }

        [CanBeNull]
        public virtual Kn5RenderableList GetDummyByName(string name) {
            var dummies = Dummies;
            for (var i = 0; i < dummies.Count; i++) {
                var x = dummies[i];
                if (x.Name == name) return x;
            }
            return null;
        }
    }
}
