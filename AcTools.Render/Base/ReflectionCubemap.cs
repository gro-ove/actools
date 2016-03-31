using System;
using System.Linq;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base {
    public interface IReflectionDraw {
        void DrawSceneForReflection(DeviceContextHolder holder, ICamera camera);
    }

    public class ReflectionCubemap : IDisposable {
        private readonly int _cubeMapSize;
        private readonly Viewport _viewport;
        private readonly FpsCamera[] _cameras;

        private ShaderResourceView _view;
        private RenderTargetView[] _targetView;
        private DepthStencilView _depthTargetView;

        public ShaderResourceView View {
            get { return _view; }
        }

        public ReflectionCubemap(int cubeMapSize) {
            _cubeMapSize = cubeMapSize;
            _cameras = new FpsCamera[6];
            _viewport = new Viewport(0, 0, _cubeMapSize, _cubeMapSize, 0, 1.0f);
        }

        public void Initialize(DeviceContextHolder holder) {
            const Format format = Format.R8G8B8A8_UNorm;

            using (var cubeTex = new Texture2D(holder.Device, new Texture2DDescription() {
                Width = _cubeMapSize,
                Height = _cubeMapSize,
                MipLevels = 0,
                ArraySize = 6,
                SampleDescription = new SampleDescription(1, 0),
                Format = format,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.GenerateMipMaps | ResourceOptionFlags.TextureCube
            })) {
                _targetView = Enumerable.Range(0, 6).Select(x => new RenderTargetView(holder.Device, cubeTex,
                    new RenderTargetViewDescription() {
                        Format = format,
                        Dimension =
                            RenderTargetViewDimension
                        .Texture2DArray,
                        ArraySize = 1,
                        FirstArraySlice = x,
                        MipSlice = 0
                    })).ToArray();

                _view = new ShaderResourceView(holder.Device, cubeTex, new ShaderResourceViewDescription {
                    Format = format,
                    Dimension = ShaderResourceViewDimension.TextureCube,
                    MostDetailedMip = 0,
                    MipLevels = -1
                });
            }

            const Format depthFormat = Format.D32_Float;
            using (var depthTex = new Texture2D(holder.Device, new Texture2DDescription {
                Width = _cubeMapSize,
                Height = _cubeMapSize,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Format = depthFormat,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            })) {
                _depthTargetView = new DepthStencilView(holder.Device, depthTex, new DepthStencilViewDescription {
                    Format = depthFormat,
                    Flags = DepthStencilViewFlags.None,
                    Dimension = DepthStencilViewDimension.Texture2D,
                    MipSlice = 0,
                });
            }
        }

        private void UpdateCameras(Vector3 center) {
            var targets = new[] {
                new Vector3(center.X + 1, center.Y, center.Z),
                new Vector3(center.X - 1, center.Y, center.Z),
                new Vector3(center.X, center.Y + 1, center.Z),
                new Vector3(center.X, center.Y - 1, center.Z),
                new Vector3(center.X, center.Y, center.Z + 1),
                new Vector3(center.X, center.Y, center.Z - 1)
            };

            var ups = new[] {
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 0, -1),
                new Vector3(0, 0, 1),
                new Vector3(0, 1, 0),
                new Vector3(0, 1, 0),
            };

            for (var i = 0; i < 6; i++) {
                _cameras[i] = new FpsCamera(MathF.PI / 2) {
                    NearZ = 0.1f,
                    FarZ = 100.0f
                };
                _cameras[i].LookAt(center, targets[i], ups[i]);
                _cameras[i].SetLens(1.0f);
                _cameras[i].UpdateViewMatrix();
            }
        }

        public void DrawScene(DeviceContextHolder holder, Vector3 position, IReflectionDraw draw) {
            UpdateCameras(position);

            holder.SaveRenderTargetAndViewport();
            holder.DeviceContext.Rasterizer.SetViewports(_viewport);
            for (var i = 0; i < 6; i++) {
                holder.DeviceContext.ClearRenderTargetView(_targetView[i], new Color4(0));
                holder.DeviceContext.ClearDepthStencilView(_depthTargetView,
                                                           DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil,
                                                           1.0f, 0);
                holder.DeviceContext.OutputMerger.SetTargets(_depthTargetView, _targetView[i]);
                draw.DrawSceneForReflection(holder, _cameras[i]);
            }

            holder.DeviceContext.GenerateMips(_view);
            holder.RestoreRenderTargetAndViewport();
        }

        public void Dispose() {
            if (_targetView == null) return;

            SlimDxExtension.Dispose(ref _view);
            SlimDxExtension.Dispose(ref _depthTargetView);

            foreach (var view in _targetView) {
                view.Dispose();
            }

            _targetView = null;
        }
    }
}
