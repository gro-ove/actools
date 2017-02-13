using System;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base {
    public class CommonStates : IDisposable {
        private readonly Device _device;

        public CommonStates(Device device) {
            _device = device;
        }

        private DepthStencilState _normalDepthState, _readOnlyDepthState, _greaterReadOnlyDepthState,
                _lessEqualDepthState, _lessEqualReadOnlyDepthState, _disabledDepthState;
        private BlendState _transparentBlendState, _addBlendState;
        private RasterizerState _doubleSidedState, _invertedState, _wireframeState, _wireframeInvertedState;

        public DepthStencilState NormalDepthState => _normalDepthState ?? (_normalDepthState =
                DepthStencilState.FromDescription(_device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.All,
                    DepthComparison = Comparison.Less,
                }));

        public DepthStencilState DisabledDepthState => _disabledDepthState ?? (_disabledDepthState =
                DepthStencilState.FromDescription(_device, new DepthStencilStateDescription {
                    IsDepthEnabled = false,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.Zero,
                    DepthComparison = Comparison.Always,
                }));

        public DepthStencilState ReadOnlyDepthState => _readOnlyDepthState ?? (_readOnlyDepthState =
                DepthStencilState.FromDescription(_device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.Zero,
                    DepthComparison = Comparison.Less,
                }));

        public DepthStencilState GreaterReadOnlyDepthState => _greaterReadOnlyDepthState ?? (_greaterReadOnlyDepthState =
                DepthStencilState.FromDescription(_device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.Zero,
                    DepthComparison = Comparison.Greater
                }));

        public DepthStencilState LessEqualDepthState => _lessEqualDepthState ?? (_lessEqualDepthState =
                DepthStencilState.FromDescription(_device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.All,
                    DepthComparison = Comparison.LessEqual
                }));

        public DepthStencilState LessEqualReadOnlyDepthState => _lessEqualReadOnlyDepthState ?? (_lessEqualReadOnlyDepthState =
                DepthStencilState.FromDescription(_device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.Zero,
                    DepthComparison = Comparison.LessEqual
                }));

        public BlendState TransparentBlendState => _transparentBlendState ?? (_transparentBlendState =
                _device.CreateBlendState(new RenderTargetBlendDescription {
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
                _device.CreateBlendState(new RenderTargetBlendDescription {
                    BlendEnable = true,
                    SourceBlend = BlendOption.SourceAlpha,
                    DestinationBlend = BlendOption.One,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = BlendOption.One,
                    DestinationBlendAlpha = BlendOption.Zero,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteMaskFlags.All,
                }));

        public RasterizerState DoubleSidedState => _doubleSidedState ?? (_doubleSidedState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.None,
                    IsFrontCounterclockwise = true,
                    IsDepthClipEnabled = false
                }));

        public RasterizerState InvertedState => _invertedState ?? (_invertedState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.Back,
                    IsFrontCounterclockwise = true,
                    IsDepthClipEnabled = true
                }));

        public RasterizerState WireframeState => _wireframeState ?? (_wireframeState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    FillMode = FillMode.Wireframe,
                    CullMode = CullMode.Back,
                    IsFrontCounterclockwise = false,
                    IsAntialiasedLineEnabled = false,
                    IsDepthClipEnabled = true
                }));

        public RasterizerState WireframeInvertedState => _wireframeInvertedState ?? (_wireframeInvertedState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    FillMode = FillMode.Wireframe,
                    CullMode = CullMode.Back,
                    IsFrontCounterclockwise = true,
                    IsAntialiasedLineEnabled = false,
                    IsDepthClipEnabled = true
                }));

        public void Dispose() {
            DisposeHelper.Dispose(ref _normalDepthState);
            DisposeHelper.Dispose(ref _readOnlyDepthState);
            DisposeHelper.Dispose(ref _greaterReadOnlyDepthState);
            DisposeHelper.Dispose(ref _lessEqualDepthState);
            DisposeHelper.Dispose(ref _lessEqualReadOnlyDepthState);
            DisposeHelper.Dispose(ref _transparentBlendState);
            DisposeHelper.Dispose(ref _addBlendState);
            DisposeHelper.Dispose(ref _doubleSidedState);
            DisposeHelper.Dispose(ref _invertedState);
            DisposeHelper.Dispose(ref _wireframeState);
            DisposeHelper.Dispose(ref _wireframeInvertedState);
        }
    }
}