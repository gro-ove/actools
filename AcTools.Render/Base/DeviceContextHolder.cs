using System;
using System.Collections.Generic;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base {
    public class DeviceContextHolder : IDisposable {
        public readonly Device Device;
        public readonly DeviceContext DeviceContext;

        // public IEffectMatricesWrapper EffectReplacement { get; set; }
        // public EffectTechnique TechniqueReplacement { get; set; }

        private QuadBuffers _quadBuffers;

        public QuadBuffers QuadBuffers => _quadBuffers ?? (_quadBuffers = new QuadBuffers(Device));

        public DeviceContextHolder(Device device) {
            Device = device;
            DeviceContext = device.ImmediateContext;
        }

        private readonly Dictionary<Type, IEffectWrapper> _effects = new Dictionary<Type, IEffectWrapper>(); 

        /// <summary>
        /// If you get effect this way, don't call its Dispose()! It'll be
        /// called automatically!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetEffect<T>() where T : IEffectWrapper, new() {
            var type = typeof (T);
            if (_effects.ContainsKey(type)) return (T)_effects[type];
            var created = (T)(_effects[type] = new T());
            created.Initialize(Device);
            return created;
        }

        private readonly Dictionary<Type, IRenderHelper> _helpers = new Dictionary<Type, IRenderHelper>(); 

        /// <summary>
        /// If you get helper this way, don't call its Dispose()! It'll be
        /// called automatically!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetHelper<T>() where T : IRenderHelper, new() {
            var type = typeof (T);
            if (_helpers.ContainsKey(type)) return (T)_helpers[type];
            var created = (T)(_helpers[type] = new T());
            created.Initialize(this);
            return created;
        }

        private RenderTargetView _savedRenderTargetView;
        private Viewport _savedViewport;

        public void SaveRenderTargetAndViewport() {
            _savedRenderTargetView = DeviceContext.OutputMerger.GetRenderTargets(1)[0];
            _savedViewport = DeviceContext.Rasterizer.GetViewports()[0];
        }

        public void RestoreRenderTargetAndViewport() {
            DeviceContext.Rasterizer.SetViewports(_savedViewport);
            DeviceContext.OutputMerger.SetTargets(_savedRenderTargetView);
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _quadBuffers);

            foreach (var effect in _effects.Values) {
                effect.Dispose();
            }
            _effects.Clear();

            foreach (var helper in _helpers.Values) {
                helper.Dispose();
            }
            _helpers.Clear();

            DeviceContext.ClearState();
            DeviceContext.Flush();
            Device.Dispose();
            DeviceContext.Dispose();
        }
    }
}
