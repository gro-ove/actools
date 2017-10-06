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
                _lessEqualDepthState, _lessEqualReadOnlyDepthState, _disabledDepthState, _shadowsDepthState;
        private BlendState _transparentBlendState, _addBlendState, _addState, _maxState, _minState, _multiplyState;

        private RasterizerState _doubleSidedState, _doubleSidedClockwiseState, _doubleSidedSmoothLinesState, _invertedState, _wireframeState,
                _wireframeInvertedState, _ambientShadowState, _shadowsState, _shadowsPointState;

        public DepthStencilState NormalDepthState => _normalDepthState ?? (_normalDepthState =
                DepthStencilState.FromDescription(_device, new DepthStencilStateDescription {
                    IsDepthEnabled = true,
                    IsStencilEnabled = false,
                    DepthWriteMask = DepthWriteMask.All,
                    DepthComparison = Comparison.Less,
                }));

        public DepthStencilState ShadowsDepthState => _shadowsDepthState ?? (_shadowsDepthState =
                DepthStencilState.FromDescription(_device, new DepthStencilStateDescription {
                    DepthWriteMask = DepthWriteMask.All,
                    DepthComparison = Comparison.Greater,
                    IsDepthEnabled = true,
                    IsStencilEnabled = false
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
                    BlendOperationAlpha = BlendOperation.Maximum,
                    RenderTargetWriteMask = ColorWriteMaskFlags.All
                }));

        public BlendState AddBlendState => _addBlendState ?? (_addBlendState =
                _device.CreateBlendState(new RenderTargetBlendDescription {
                    BlendEnable = true,
                    SourceBlend = BlendOption.SourceAlpha,
                    DestinationBlend = BlendOption.One,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = BlendOption.One,
                    DestinationBlendAlpha = BlendOption.One,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteMaskFlags.All,
                }));

        public BlendState AddState => _addState ?? (_addState =
                _device.CreateBlendState(new RenderTargetBlendDescription {
                    BlendEnable = true,
                    SourceBlend = BlendOption.One,
                    DestinationBlend = BlendOption.One,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = BlendOption.One,
                    DestinationBlendAlpha = BlendOption.One,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteMaskFlags.All,
                }));

        public BlendState MaxState => _maxState ?? (_maxState =
                _device.CreateBlendState(new RenderTargetBlendDescription {
                    BlendEnable = true,
                    SourceBlend = BlendOption.One,
                    DestinationBlend = BlendOption.One,
                    BlendOperation = BlendOperation.Maximum,
                    SourceBlendAlpha = BlendOption.One,
                    DestinationBlendAlpha = BlendOption.One,
                    BlendOperationAlpha = BlendOperation.Maximum,
                    RenderTargetWriteMask = ColorWriteMaskFlags.All,
                }));

        public BlendState MinState => _minState ?? (_minState =
                _device.CreateBlendState(new RenderTargetBlendDescription {
                    BlendEnable = true,
                    SourceBlend = BlendOption.One,
                    DestinationBlend = BlendOption.One,
                    BlendOperation = BlendOperation.Minimum,
                    SourceBlendAlpha = BlendOption.One,
                    DestinationBlendAlpha = BlendOption.One,
                    BlendOperationAlpha = BlendOperation.Minimum,
                    RenderTargetWriteMask = ColorWriteMaskFlags.All,
                }));

        public BlendState MultiplyState => _multiplyState ?? (_multiplyState =
                _device.CreateBlendState(new RenderTargetBlendDescription {
                    BlendEnable = true,
                    SourceBlend = BlendOption.Zero,
                    DestinationBlend = BlendOption.SourceColor,
                    BlendOperation = BlendOperation.Add,
                    SourceBlendAlpha = BlendOption.Zero,
                    DestinationBlendAlpha = BlendOption.SourceAlpha,
                    BlendOperationAlpha = BlendOperation.Add,
                    RenderTargetWriteMask = ColorWriteMaskFlags.All,
                }));

        public RasterizerState DoubleSidedSmoothLinesState => _doubleSidedSmoothLinesState ?? (_doubleSidedSmoothLinesState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    IsAntialiasedLineEnabled = true,
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.None,
                    IsFrontCounterclockwise = true,
                    IsDepthClipEnabled = false
                }));

        public RasterizerState DoubleSidedState => _doubleSidedState ?? (_doubleSidedState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.None,
                    IsFrontCounterclockwise = true,
                    IsDepthClipEnabled = false
                }));

        public RasterizerState DoubleSidedClockwiseState => _doubleSidedClockwiseState ?? (_doubleSidedClockwiseState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.None,
                    IsFrontCounterclockwise = false,
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

        public RasterizerState AmbientShadowState => _ambientShadowState ?? (_ambientShadowState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.None,
                    IsFrontCounterclockwise = false,
                    IsDepthClipEnabled = false,
                    DepthBias = -1000,
                    DepthBiasClamp = 0.0f,
                    SlopeScaledDepthBias = -100f
                }));

        public RasterizerState ShadowsState => _shadowsState ?? (_shadowsState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    CullMode = CullMode.Front,
                    FillMode = FillMode.Solid,
                    IsAntialiasedLineEnabled = false,
                    IsDepthClipEnabled = true,
                    DepthBias = 100,
                    DepthBiasClamp = 0.0f,
                    SlopeScaledDepthBias = 1f
                }));

        public RasterizerState ShadowsPointState => _shadowsPointState ?? (_shadowsPointState =
                RasterizerState.FromDescription(_device, new RasterizerStateDescription {
                    CullMode = CullMode.Back,
                    FillMode = FillMode.Solid,
                    IsFrontCounterclockwise = true,
                    IsAntialiasedLineEnabled = false,
                    IsDepthClipEnabled = true,
                    DepthBias = 100,
                    DepthBiasClamp = 0.0f,
                    SlopeScaledDepthBias = 1f
                }));

        public void Dispose() {
            try {
                DisposeHelper.Dispose(ref _normalDepthState);
                DisposeHelper.Dispose(ref _shadowsDepthState);
                DisposeHelper.Dispose(ref _readOnlyDepthState);
                DisposeHelper.Dispose(ref _greaterReadOnlyDepthState);
                DisposeHelper.Dispose(ref _lessEqualDepthState);
                DisposeHelper.Dispose(ref _lessEqualReadOnlyDepthState);
                DisposeHelper.Dispose(ref _transparentBlendState);
                DisposeHelper.Dispose(ref _addBlendState);
                DisposeHelper.Dispose(ref _addState);
                DisposeHelper.Dispose(ref _maxState);
                DisposeHelper.Dispose(ref _minState);
                DisposeHelper.Dispose(ref _multiplyState);
                DisposeHelper.Dispose(ref _doubleSidedState);
                DisposeHelper.Dispose(ref _doubleSidedClockwiseState);
                DisposeHelper.Dispose(ref _doubleSidedSmoothLinesState);
                DisposeHelper.Dispose(ref _invertedState);
                DisposeHelper.Dispose(ref _wireframeState);
                DisposeHelper.Dispose(ref _wireframeInvertedState);
                DisposeHelper.Dispose(ref _ambientShadowState);
                DisposeHelper.Dispose(ref _shadowsState);
                DisposeHelper.Dispose(ref _shadowsPointState);
            } catch (Exception e) {
                AcToolsLogging.Write(e);
            }
        }
    }
}