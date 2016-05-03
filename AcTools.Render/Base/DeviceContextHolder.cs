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
        }

        public long GetDedicatedVideoMemory() {
            using (var dxgiDevice = new SlimDX.DXGI.Device(Device))
            using (var adapter = dxgiDevice.Adapter){
                return adapter.Description.DedicatedVideoMemory;
            }
        }

        private readonly Dictionary<Type, IEffectWrapper> _effects = new Dictionary<Type, IEffectWrapper>();

        /// <summary>
        /// If you get effect this way, don't call its Dispose()! It'll be
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

        private readonly Dictionary<Type, IRenderHelper> _helpers = new Dictionary<Type, IRenderHelper>();

        /// <summary>
        /// If you get helper this way, don't call its Dispose()! It'll be
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
            _savedRenderTargetView = DeviceContext.OutputMerger.GetRenderTargets(1)[0];
            _savedViewport = DeviceContext.Rasterizer.GetViewports()[0];
        }

        public void RestoreRenderTargetAndViewport() {
            DeviceContext.Rasterizer.SetViewports(_savedViewport);
            DeviceContext.OutputMerger.SetTargets(_savedRenderTargetView);
        }

        #region Common shared stuff
        private DepthStencilState _normalDepthState, _readOnlyDepthState, _greaterReadOnlyDepthState,
                _lessEqualDepthState, _lessEqualReadOnlyDepthState;
        private BlendState _transparentBlendState, _addBlendState;

        public DepthStencilState NormalDepthState => _normalDepthState ?? (_normalDepthState = _normalDepthState =
                DepthStencilState.FromDescription(Device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.All,
                    DepthComparison = Comparison.Less,
                }));

        public DepthStencilState ReadOnlyDepthState => _readOnlyDepthState ?? (_readOnlyDepthState =
                DepthStencilState.FromDescription(Device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.Zero,
                    DepthComparison = Comparison.Less,
                }));

        public DepthStencilState GreaterReadOnlyDepthState => _greaterReadOnlyDepthState ?? (_greaterReadOnlyDepthState =
                DepthStencilState.FromDescription(Device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.Zero,
                    DepthComparison = Comparison.Greater
                }));

        public DepthStencilState LessEqualDepthState => _lessEqualDepthState ?? (_lessEqualDepthState =
                DepthStencilState.FromDescription(Device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.All,
                    DepthComparison = Comparison.LessEqual
                }));

        public DepthStencilState LessEqualReadOnlyDepthState => _lessEqualReadOnlyDepthState ?? (_lessEqualReadOnlyDepthState =
                DepthStencilState.FromDescription(Device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.Zero,
                    DepthComparison = Comparison.LessEqual
                }));

        public BlendState TransparentBlendState => _transparentBlendState ?? (_transparentBlendState =
                Device.CreateBlendState(new RenderTargetBlendDescription {
                    BlendEnable = true,
                    SourceBlend = BlendOption.SourceAlpha,
                    DestinationBlend = BlendOption.InverseSourceAlpha,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = BlendOption.One,
                    DestinationBlendAlpha = BlendOption.One,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteMaskFlags.All,
                }));

        public BlendState AddBlendState => _addBlendState ?? (_addBlendState =
                Device.CreateBlendState(new RenderTargetBlendDescription {
                    BlendEnable = true,
                    SourceBlend = BlendOption.SourceAlpha,
                    DestinationBlend = BlendOption.One,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = BlendOption.One,
                    DestinationBlendAlpha = BlendOption.Zero,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteMaskFlags.All,
                }));
        #endregion

        public void Dispose() {
            DisposeHelper.Dispose(ref _normalDepthState);
            DisposeHelper.Dispose(ref _readOnlyDepthState);
            DisposeHelper.Dispose(ref _greaterReadOnlyDepthState);
            DisposeHelper.Dispose(ref _lessEqualDepthState);
            DisposeHelper.Dispose(ref _lessEqualReadOnlyDepthState);
            DisposeHelper.Dispose(ref _transparentBlendState);
            DisposeHelper.Dispose(ref _addBlendState);

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
