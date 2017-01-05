using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Objects {
    /* Despite holding references to SharedMaterials and ITexturesProvider, this
     * thing doesn’t need to be disposed — those references are managed outside,
     * and this thing is just a relatively convinient way to pass them down 
     * rendering tree */

    public class Kn5LocalDeviceContextHolder : IDeviceContextHolder {
        private readonly IDeviceContextHolder _mainHolder;
        private readonly SharedMaterials _sharedMaterials;
        private readonly ITexturesProvider _texturesProvider;

        public Kn5LocalDeviceContextHolder(IDeviceContextHolder mainHolder, SharedMaterials sharedMaterials, ITexturesProvider texturesProvider) {
            _mainHolder = mainHolder;
            _sharedMaterials = sharedMaterials;
            _texturesProvider = texturesProvider;
        }
        public Device Device => _mainHolder.Device;

        public DeviceContext DeviceContext => _mainHolder.DeviceContext;

        public T Get<T>() where T : class {
            if (typeof(T) == typeof(ITexturesProvider)) {
                return _texturesProvider as T;
            }

            if (typeof(T) == typeof(SharedMaterials)) {
                return _sharedMaterials as T;
            }

            return _mainHolder.Get<T>();
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
    }

    public class Kn5RenderableFile : RenderableList {
        public readonly Kn5 OriginalFile;
        public readonly Kn5RenderableList RootObject;

        public Kn5RenderableFile(Kn5 kn5, Matrix matrix) : base(kn5.OriginalFilename, matrix) {
            OriginalFile = kn5;
            RootObject = (Kn5RenderableList)Convert(kn5.RootNode);
            Add(RootObject);
        }
        
        protected Kn5SharedMaterials SharedMaterials;
        protected ITexturesProvider TexturesProvider;
        protected IDeviceContextHolder LocalHolder;
        
        protected virtual Kn5SharedMaterials InitializeMaterials(IDeviceContextHolder contextHolder) {
            return new Kn5SharedMaterials(contextHolder, OriginalFile);
        }

        protected virtual ITexturesProvider InitializeTextures(IDeviceContextHolder contextHolder) {
            return new Kn5TexturesProvider(OriginalFile);
        }

        protected virtual IDeviceContextHolder InitializeLocalHolder(IDeviceContextHolder contextHolder) {
            return new Kn5LocalDeviceContextHolder(contextHolder, SharedMaterials, TexturesProvider);
        }

        protected void DrawInitialize(IDeviceContextHolder contextHolder) {
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

        public static IRenderableObject Convert(Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node);

                case Kn5NodeClass.Mesh:
                case Kn5NodeClass.SkinnedMesh:
                    return new Kn5RenderableObject(node);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
