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
    }

    public class Kn5RenderableFile : RenderableList, IKn5Model {
        protected readonly bool AllowSkinnedObjects;
        protected readonly bool AsyncTexturesLoading;

        public readonly Kn5 OriginalFile;

        private Kn5RenderableList _rootObject;
        private List<Kn5RenderableList> _dummies;
        private List<IKn5RenderableObject> _meshes;

        public Kn5RenderableList RootObject {
            get { return _rootObject; }
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

        public Kn5RenderableFile(Kn5 kn5, Matrix matrix, bool asyncTexturesLoading = true, bool allowSkinnedObjects = false) : base(kn5.OriginalFilename, matrix) {
            AllowSkinnedObjects = allowSkinnedObjects;
            OriginalFile = kn5;
            AsyncTexturesLoading = asyncTexturesLoading;
            RootObject = (Kn5RenderableList)Convert(kn5.RootNode, AllowSkinnedObjects);
            Add(RootObject);
        }

        private void OnRootObjectMatrixChanged(object sender, EventArgs e) {
            InvalidateModelMatrixInverted();
        }

        private bool _modelMatrixInvertedDirty;

        protected void InvalidateModelMatrixInverted() {
            _modelMatrixInvertedDirty = true;
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

        protected void DrawInitialize(IDeviceContextHolder contextHolder) {
            UpdateModelMatrixInverted();
            if (LocalHolder == null) {
                SharedMaterials = InitializeMaterials(contextHolder);
                TexturesProvider = InitializeTextures(contextHolder);
                LocalHolder = InitializeLocalHolder(contextHolder);
            }
        }

        /// <summary>
        /// Despite being a list, only RootObject is drawn!
        /// </summary>
        public override void Draw(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            DrawInitialize(contextHolder);
            RootObject.Draw(LocalHolder, camera, mode, filter);
        }

        public override void Dispose() {
            base.Dispose();
            DisposeHelper.Dispose(ref SharedMaterials);
            DisposeHelper.Dispose(ref TexturesProvider);
        }

        public static IRenderableObject Convert(Kn5Node node, bool allowSkinnedObjects) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node, n => Convert(n, allowSkinnedObjects));

                case Kn5NodeClass.Mesh:
                    return new Kn5RenderableObject(node);

                case Kn5NodeClass.SkinnedMesh:
                    if (allowSkinnedObjects) {
                        return new Kn5SkinnedObject(node);
                    }
                    return new Kn5RenderableObject(node);

                default:
                    throw new ArgumentOutOfRangeException();
            }
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
