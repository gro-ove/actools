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
        protected Texture2D CubeTex;

        private int _cubeMapSize;
        private int _mipLevels;
        private Viewport _viewport;
        private ShaderResourceView _view;
        private RenderTargetView[] _targetView;
        private DepthStencilView _depthTargetView;

        private Color4 _backgroundColor;

        public Color4 BackgroundColor {
            get => _backgroundColor;
            set {
                if (value.Red == _backgroundColor.Red &&
                        value.Green == _backgroundColor.Green &&
                        value.Blue == _backgroundColor.Blue) return;
                _backgroundColor = value;
                SetDirty();
            }
        }

        public ShaderResourceView View => _view;

        public ReflectionCubemap(int cubeMapSize) {
            _cubeMapSize = cubeMapSize;
            _viewport = new Viewport(0, 0, _cubeMapSize, _cubeMapSize, 0, 1.0f);
        }

        public void SetResolution(DeviceContextHolder holder, int resolution) {
            if (_cubeMapSize == resolution) return;
            _cubeMapSize = resolution;
            _viewport = new Viewport(0, 0, _cubeMapSize, _cubeMapSize, 0, 1.0f);
            Initialize(holder);
            SetDirty();
        }

        protected virtual int GetMipLevels(int resolution, int minResolution) {
            var v = 1 + Math.Log(resolution, 2) - Math.Log(minResolution, 2);
            return double.IsNaN(v) || v < 1 ? 1 : v.RoundToInt();
        }

        public void Initialize(DeviceContextHolder holder) {
            const Format format = Format.R16G16B16A16_Float;

            DisposeHelper.Dispose(ref _view);
            DisposeHelper.Dispose(ref _depthTargetView);
            DisposeHelper.Dispose(ref _targetView);
            DisposeHelper.Dispose(ref CubeTex);

            _mipLevels = GetMipLevels(_cubeMapSize, 4);
            CubeTex = new Texture2D(holder.Device, new Texture2DDescription {
                Width = _cubeMapSize,
                Height = _cubeMapSize,
                MipLevels = _mipLevels,
                ArraySize = 6,
                SampleDescription = new SampleDescription(1, 0),
                Format = format,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.GenerateMipMaps | ResourceOptionFlags.TextureCube
            });

            _targetView = Enumerable.Range(0, 6).Select(x => new RenderTargetView(holder.Device, CubeTex,
                    new RenderTargetViewDescription {
                        Format = format,
                        Dimension = RenderTargetViewDimension.Texture2DArray,
                        ArraySize = 1,
                        FirstArraySlice = x,
                        MipSlice = 0
                    })).ToArray();

            _view = new ShaderResourceView(holder.Device, CubeTex, new ShaderResourceViewDescription {
                Format = format,
                Dimension = ShaderResourceViewDimension.TextureCube,
                MostDetailedMip = 0,
                MipLevels = -1
            });

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

        private readonly FpsCamera[] _cameras = Enumerable.Range(0, 6).Select(x => {
            var c = new FpsCamera(MathF.PI / 2) { NearZ = 0.05f, FarZ = 500.0f, RhMode = false };
            c.SetLens(1f);
            return c;
        }).ToArray();

        private readonly Vector3[] _targets = {
            new Vector3(1, 0, 0),
            new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, -1, 0),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, -1)
        };

        private readonly Vector3[] _ups = {
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, -1),
            new Vector3(0, 0, 1),
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 0),
        };

        private bool _onceUpdated;

        public void Update(Vector3 center) {
            if (_onceUpdated && (center - _previousCenter).LengthSquared() < 0.01) return;
            _onceUpdated = true;

            for (var i = 0; i < 6; i++) {
                _cameras[i].LookAt(center, center + _targets[i], _ups[i]);
                _cameras[i].UpdateViewMatrix();
            }

            _previousCenter = center;
            SetDirty();
        }

        private int _dirty;

        public void SetDirty(bool dirty = true) {
            _dirty = dirty ? 63 : 0;
        }

        public bool IsDirty => _dirty != 0;

        protected virtual void OnCubemapUpdate(DeviceContextHolder holder) {
            if (_mipLevels > 1) {
                holder.DeviceContext.GenerateMips(_view);
            }
        }

        private int _previouslyUpdated;

        public void DrawScene(DeviceContextHolder holder, IReflectionDraw draw, int facesPerFrame) {
            if (facesPerFrame == 0 || _dirty == 0) return;

            using (holder.SaveRenderTargetAndViewport()) {
                holder.DeviceContext.Rasterizer.SetViewports(_viewport);

                var offset = _previouslyUpdated + 1;
                for (var k = 0; k < 6; k++) {
                    var i = (k + offset) % 6;
                    if (((_dirty >> i) & 1) == 1 && --facesPerFrame >= 0) {
                        _dirty ^= 1 << i;

                        holder.DeviceContext.ClearRenderTargetView(_targetView[i], _backgroundColor);
                        holder.DeviceContext.ClearDepthStencilView(_depthTargetView,
                                DepthStencilClearFlags.Depth,
                                1.0f, 0);
                        holder.DeviceContext.OutputMerger.SetTargets(_depthTargetView, _targetView[i]);
                        draw.DrawSceneForReflection(holder, _cameras[i]);
                        _previouslyUpdated = i;
                    }
                }

                OnCubemapUpdate(holder);
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _view);
            DisposeHelper.Dispose(ref _depthTargetView);
            DisposeHelper.Dispose(ref _targetView);
            DisposeHelper.Dispose(ref CubeTex);
        }
    }
}
