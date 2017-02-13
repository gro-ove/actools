using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base {
    public interface IDeviceContextHolder {
        [NotNull]
        Device Device { get; }

        [NotNull]
        DeviceContext DeviceContext { get; }

        T Get<T>() where T : class;

        T GetEffect<T>() where T : IEffectWrapper, new();

        [NotNull]
        IRenderableMaterial GetMaterial(object key);

        [NotNull]
        CommonStates States { get; }

        void RaiseUpdateRequired();

        void RaiseSceneUpdated();
    }

    public class DeviceContextHolder : IDeviceContextHolder, IDisposable {
        public Device Device { get; }

        public DeviceContext DeviceContext { get; }

        private QuadBuffers _quadBuffers;

        public QuadBuffers QuadBuffers => _quadBuffers ?? (_quadBuffers = new QuadBuffers(Device));

        public void PrepareQuad(InputLayout layout) {
            QuadBuffers.Prepare(DeviceContext, layout);
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public DeviceContextHolder(Device device) {
            Device = device;
            DeviceContext = device.ImmediateContext;
        }

        public void OnResize(int width, int height) {
            Width = width;
            Height = height;

            foreach (var helper in _helpers.Values) {
                helper.OnResize(this);
            }

            foreach (var effect in _effects.Values.OfType<IEffectScreenSizeWrapper>()) {
                effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));
            }
        }

        public long GetDedicatedVideoMemory() {
            using (var dxgiDevice = new SlimDX.DXGI.Device(Device))
            using (var adapter = dxgiDevice.Adapter){
                return adapter.Description.DedicatedVideoMemory;
            }
        }

        private readonly Dictionary<Type, IEffectWrapper> _effects = new Dictionary<Type, IEffectWrapper>();

        /// <summary>
        /// If you get effect this way, don’t call its Dispose()! It’ll be
        /// called automatically!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetEffect<T>() where T : IEffectWrapper, new() {
            IEffectWrapper result;
            var type = typeof(T);
            if (_effects.TryGetValue(type, out result)) return (T)result;

            var created = (T)(_effects[type] = new T());
            created.Initialize(Device);
            return created;
        }

        private readonly Dictionary<Type, object> _something = new Dictionary<Type, object>();

        public void Set<T>(T obj) where T : class {
            _something[typeof(T)] = obj;
        }

        public void Remove<T>() where T : class {
            _something.Remove(typeof(T));
        }

        public T Get<T>() where T : class {
            var key = typeof(T);
            object result;

            if (_something.TryGetValue(key, out result)) {
                return (T)result;
            }

            if (typeof(T) == typeof(SharedMaterials)) {
                return GetSharedMaterialsInstance() as T;
            }

            T child = null;
            foreach (var o in _something) {
                child = o.Value as T;
                if (child != null) {
                    break;
                }
            }

            if (child != null) {
                _something[key] = child;
                return child;
            }

            throw new Exception($"Entry with type {key} not found");
        }

        private SharedMaterials _sharedMaterials;

        private SharedMaterials GetSharedMaterialsInstance() {
            return _sharedMaterials ?? (_sharedMaterials = new SharedMaterials(Get<IMaterialsFactory>()));
        }

        public IRenderableMaterial GetMaterial(object key) {
            return GetSharedMaterialsInstance().GetMaterial(key);
        }

        private readonly Dictionary<Type, IRenderHelper> _helpers = new Dictionary<Type, IRenderHelper>();

        /// <summary>
        /// If you get helper this way, don’t call its Dispose()! It’ll be
        /// called automatically!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetHelper<T>() where T : IRenderHelper, new() {
            IRenderHelper result;
            var type = typeof(T);
            if (_helpers.TryGetValue(type, out result)) return (T)result;

            var created = (T)(_helpers[type] = new T());
            created.OnInitialize(this);
            if (Width != 0 || Height != 0) {
                created.OnResize(this);
            }
            return created;
        }

        private RenderTargetView _savedRenderTargetView;
        private Viewport _savedViewport;

        public void SaveRenderTargetAndViewport() {
            var targets = DeviceContext.OutputMerger.GetRenderTargets(1);
            var viewports = DeviceContext.Rasterizer.GetViewports();
            _savedRenderTargetView = targets.Length > 0 ? targets[0] : null;
            _savedViewport = viewports.Length > 0 ? viewports[0] : default(Viewport);
        }

        public void RestoreRenderTargetAndViewport() {
            DeviceContext.Rasterizer.SetViewports(_savedViewport);
            DeviceContext.OutputMerger.SetTargets(_savedRenderTargetView);
        }

        public event EventHandler UpdateRequired;
        public event EventHandler SceneUpdated;

        public void RaiseUpdateRequired() {
            UpdateRequired?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseSceneUpdated() {
            UpdateRequired?.Invoke(this, EventArgs.Empty);
            SceneUpdated?.Invoke(this, EventArgs.Empty);
        }

        private CommonStates _states;

        public CommonStates States => _states ?? (_states = new CommonStates(Device));

        public void Dispose() {
            DisposeHelper.Dispose(ref _states);
            DisposeHelper.Dispose(ref _quadBuffers);

            _effects.DisposeEverything();
            _helpers.DisposeEverything();
            _something.Values.OfType<IDisposable>().DisposeEverything();
            _something.Clear();

            DeviceContext.ClearState();
            DeviceContext.Flush();
            Device.Dispose();
            DeviceContext.Dispose();
        }
    }
}
