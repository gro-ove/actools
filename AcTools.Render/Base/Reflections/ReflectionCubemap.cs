using System;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.Reflections {
    public class ReflectionCubemap : IDisposable {
        private readonly FpsCamera[] _cameras;

        private int _cubeMapSize;
        private Viewport _viewport;
        private ShaderResourceView _view;
        private RenderTargetView[] _targetView;
        private DepthStencilView _depthTargetView;

        public Color4 BackgroundColor { get; set; }

        public ShaderResourceView View => _view;

        public ReflectionCubemap(int cubeMapSize) {
            _cubeMapSize = cubeMapSize;
            _cameras = new FpsCamera[6];
            _viewport = new Viewport(0, 0, _cubeMapSize, _cubeMapSize, 0, 1.0f);
        }

        public void SetResolution(DeviceContextHolder holder, int resolution) {
            if (_cubeMapSize == resolution) return;
            _cubeMapSize = resolution;
            _viewport = new Viewport(0, 0, _cubeMapSize, _cubeMapSize, 0, 1.0f);
            Initialize(holder);
            SetDirty();
        }

        private static int GetMipLevels(int resolution, int minResolution) {
            var v = 1 + Math.Log(resolution, 2) - Math.Log(minResolution, 2);
            return double.IsNaN(v) || v < 1 ? 1 : v.RoundToInt();
        }

        public void Initialize(DeviceContextHolder holder) {
            const Format format = Format.R16G16B16A16_Float;

            DisposeHelper.Dispose(ref _view);
            DisposeHelper.Dispose(ref _depthTargetView);
            DisposeHelper.Dispose(ref _targetView);

            using (var cubeTex = new Texture2D(holder.Device, new Texture2DDescription {
                Width = _cubeMapSize,
                Height = _cubeMapSize,
                MipLevels = GetMipLevels(_cubeMapSize, 4),
                ArraySize = 6,
                SampleDescription = new SampleDescription(1, 0),
                Format = format,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.GenerateMipMaps | ResourceOptionFlags.TextureCube
            })) {
                _targetView = Enumerable.Range(0, 6).Select(x => new RenderTargetView(holder.Device, cubeTex,
                        new RenderTargetViewDescription {
                            Format = format,
                            Dimension = RenderTargetViewDimension.Texture2DArray,
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

        private Vector3 _previousCenter;

        public void Update(Vector3 center) {
            if (_cameras[0] != null && (center - _previousCenter).LengthSquared() < 0.01) return;

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
                    FarZ = 500.0f,
                    RhMode = false
                };
                _cameras[i].LookAt(center, targets[i], ups[i]);
                _cameras[i].SetLens(1f);
                _cameras[i].UpdateViewMatrix();
            }

            _previousCenter = center;
            SetDirty();
        }

        private readonly bool[] _dirty = {
            true, true, true, true, true, true
        };

        public void SetDirty(bool dirty = true) {
            for (var i = 0; i < _dirty.Length; i++) {
                _dirty[i] = dirty;
            }
        }

        private int _previouslyUpdated;

        public bool DrawScene(DeviceContextHolder holder, IReflectionDraw draw, int facesPerFrame) {
            if (facesPerFrame == 0 || _dirty.All(x => !x)) return true;

            using (holder.SaveRenderTargetAndViewport()) {
                holder.DeviceContext.Rasterizer.SetViewports(_viewport);

                var offset = _previouslyUpdated + 1;
                for (var k = 0; k < 6; k++) {
                    var i = (k + offset) % 6;
                    if (_dirty[i] && --facesPerFrame >= 0) {
                        _dirty[i] = false;

                        holder.DeviceContext.ClearRenderTargetView(_targetView[i], BackgroundColor);
                        holder.DeviceContext.ClearDepthStencilView(_depthTargetView,
                                DepthStencilClearFlags.Depth,
                                1.0f, 0);
                        holder.DeviceContext.OutputMerger.SetTargets(_depthTargetView, _targetView[i]);
                        draw.DrawSceneForReflection(holder, _cameras[i]);
                        _previouslyUpdated = i;
                    }
                }

                holder.DeviceContext.GenerateMips(_view);
            }

            return facesPerFrame >= 0;
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _view);
            DisposeHelper.Dispose(ref _depthTargetView);
            DisposeHelper.Dispose(ref _targetView);
        }
    }
}
