using System.IO;
using System.Linq;
using AcTools.Kn5Render.Kn5Render.Effects;
using AcTools.Utils;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;

namespace AcTools.Kn5Render.Kn5Render {
    public interface IRenderLogging {
        void Write(string format, params object[] args);
        void Warning(string format, params object[] args);
        void Error(string format, params object[] args);
    }

    public class EmptyRenderLogging : IRenderLogging {
        public void Warning(string format, params object[] args) {
        }

        public void Error(string format, params object[] args) {
        }

        public void Write(string format, params object[] args) {
        }
    }

    public partial class Render : System.IDisposable {
        private static readonly IRenderLogging EmptyLogging = new EmptyRenderLogging();
        private static IRenderLogging _logging;

        public static IRenderLogging Logging {
            get { return _logging ?? EmptyLogging; }
            set { _logging = value; }
        }

        public enum VisualMode {
            SIMPLE_PREVIEW_GT5,
            SIMPLE_PREVIEW_GT6,
            SIMPLE_PREVIEW_SEAT_LEON_EUROCUP,

            LIVERY_VIEW,
            BODY_SHADOW,
            TRACK_MAP,

            DARK_ROOM,
            BRIGHT_ROOM
        }

        int _width, _height;
        public float AspectRatio { get { return (float)_width / _height; } }

        public Device CurrentDevice { get; private set; }

        DeviceContext _context;
        Viewport _viewport;

        EffectTest _effectTest;
        EffectScreen _effectScreen;

        Texture2D _renderTargetTexture;
        RenderTargetView _renderTarget;
        ShaderResourceView _renderTargetResource;
        DepthStencilView _depthStencilView;
        Texture2D _depthStencilTexture;
        DepthStencilState _depthState;
        private SquareBuffers _squareBuffers;

        RasterizerState _wireframeRs;
        SampleDescription _sampleDesc;
        private BlendState _transparentBlendState;

        CameraBase _camera;
        bool _wireframeMode;

        public Render(string filename, int skinNumber, VisualMode mode = VisualMode.DARK_ROOM) {
            DxInitDevice();
            DxInitStuff();
            LoadScene(filename, skinNumber, mode);
        }

        public Render(string filename, string skinName, VisualMode mode = VisualMode.DARK_ROOM) 
            : this(filename, 
                    Directory.GetDirectories(FileUtils.GetCarSkinsDirectory(Path.GetDirectoryName(filename)))
                            .Select(Path.GetFileName).ToList().IndexOf(skinName), 
            mode){}

        private bool _dx11Mode;

        void DxInitDevice() {
            CurrentDevice = new Device(DriverType.Hardware, DeviceCreationFlags.None);
            if (CurrentDevice.FeatureLevel < FeatureLevel.Level_10_1) {
                throw new System.Exception("Direct3D Feature Level 10.1 unsupported");
            }

            _dx11Mode = CurrentDevice.FeatureLevel >= FeatureLevel.Level_11_0;

            var msaaQuality = CurrentDevice.CheckMultisampleQualityLevels(Format.R8G8B8A8_UNorm, 4);
            _sampleDesc = _dx11Mode ? new SampleDescription(1, 0) : new SampleDescription(4, msaaQuality - 1);

            Logging.Write("MSAA Quality: " + msaaQuality);

            _context = CurrentDevice.ImmediateContext;
        }

        void DxInitStuff() {
            _effectTest = new EffectTest(CurrentDevice);
            _effectScreen = new EffectScreen(CurrentDevice);

            _squareBuffers = new SquareBuffers(CurrentDevice);

            _wireframeRs = RasterizerState.FromDescription(CurrentDevice, new RasterizerStateDescription {
                FillMode = FillMode.Wireframe,
                CullMode = CullMode.Back,
                IsFrontCounterclockwise = false,
                IsDepthClipEnabled = true
            });
            
            var transDesc = new BlendStateDescription {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };

            transDesc.RenderTargets[0].BlendEnable = true;
            transDesc.RenderTargets[0].SourceBlend = BlendOption.SourceAlpha;
            transDesc.RenderTargets[0].DestinationBlend = BlendOption.InverseSourceAlpha;
            transDesc.RenderTargets[0].BlendOperation = BlendOperation.Add;
            transDesc.RenderTargets[0].SourceBlendAlpha = BlendOption.One;
            transDesc.RenderTargets[0].DestinationBlendAlpha = BlendOption.One;
            transDesc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
            transDesc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;

            _transparentBlendState = BlendState.FromDescription(CurrentDevice, transDesc);
        }

        public void Dispose() {
            foreach (var obj in _objs) {
                obj.Dispose();
            }

            foreach (var tex in _textures.Values.Where(tex => tex != null)) {
                tex.Dispose();
            }

            if (_form_swapChain != null) {
                _form_renderTarget.Dispose();
                _form_swapChain.Dispose();
            }

            if (_reflectionCubemap != null) _reflectionCubemap.Dispose();
            if (_wireframeRs != null) _wireframeRs.Dispose();
            if (_transparentBlendState != null) _transparentBlendState.Dispose();
            if (_depthState != null) _depthState.Dispose();
            if (_depthStencilTexture != null) _depthStencilTexture.Dispose();
            if (_depthStencilView != null) _depthStencilView.Dispose();
            if (_effectTest != null) _effectTest.Dispose();
            if (_effectScreen != null) _effectScreen.Dispose();
            if (_renderTarget != null) _renderTarget.Dispose();
            if (_renderTargetTexture != null) _renderTargetTexture.Dispose();
            if (_renderTargetResource != null) _renderTargetResource.Dispose();
            if (_squareBuffers != null) _squareBuffers.Dispose();
            if (CurrentDevice != null) CurrentDevice.Dispose();

            System.GC.Collect();
        }

        public void Dispose(bool b) {
            if (b) Dispose();
        }
    }
}
