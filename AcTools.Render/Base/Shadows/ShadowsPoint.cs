using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.Shadows {
    public class ShadowsPoint : ShadowsBase {
        private readonly FpsCamera[] _cameras;
        private ShaderResourceView _view;
        private DepthStencilView[] _targetView;

        public ShaderResourceView View => _view;

        public float FarZValue => _cameras[0].FarZValue;

        public float NearZValue => _cameras[0].NearZValue;

        public ShadowsPoint(int mapSize) : base(mapSize) {
            _cameras = Enumerable.Range(0, 6).Select(x => new FpsCamera(MathF.PI / 2) {
                NearZ = 0.1f,
                RhMode = false
            }).ToArray();
        }

        protected override RasterizerStateDescription GetRasterizerStateDescription() {
            return new RasterizerStateDescription {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
                IsFrontCounterclockwise = true,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = true,
                DepthBias = 100,
                DepthBiasClamp = 0.0f,
                SlopeScaledDepthBias = 1f
            };
        }

        protected override void ResizeBuffers(DeviceContextHolder holder, int size) {
            DisposeHelper.Dispose(ref _view);
            DisposeHelper.Dispose(ref _targetView);

            using (var cubeTex = new Texture2D(holder.Device, new Texture2DDescription {
                Width = size,
                Height = size,
                MipLevels = 1,
                ArraySize = 6,
                SampleDescription = new SampleDescription(1, 0),
                Format = Format.R24G8_Typeless,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.TextureCube
            })) {
                _targetView = Enumerable.Range(0, 6).Select(x => new DepthStencilView(holder.Device, cubeTex,
                        new DepthStencilViewDescription {
                            Format = Format.D24_UNorm_S8_UInt,
                            Dimension = DepthStencilViewDimension.Texture2DArray,
                            ArraySize = 1,
                            FirstArraySlice = x,
                            MipSlice = 0
                        })).ToArray();
                _view = new ShaderResourceView(holder.Device, cubeTex, new ShaderResourceViewDescription {
                    Format = Format.R24_UNorm_X8_Typeless,
                    Dimension = ShaderResourceViewDimension.TextureCube,
                    MostDetailedMip = 0,
                    MipLevels = -1
                });
            }
        }

        private Vector3 _center;
        private float _radius;

        public bool Update(Vector3 center, float radius) {
            if (_center == center && _radius == radius) return false;

            _center = center;
            _radius = radius;

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
                if (_cameras[i].FarZ != radius) {
                    _cameras[i].FarZ = radius;
                    _cameras[i].SetLens(1f);
                }

                _cameras[i].LookAt(center, targets[i], ups[i]);
                _cameras[i].UpdateViewMatrix();
            }

            return true;
        }

        public override void Invalidate() {
            _center = default(Vector3);
            _radius = 0f;
        }

        protected override void UpdateBuffers(DeviceContextHolder holder, IShadowsDraw draw) {
            for (var i = 0; i < 6; i++) {
                var view = _targetView[i];
                holder.DeviceContext.OutputMerger.SetTargets(view);
                holder.DeviceContext.ClearDepthStencilView(view, DepthStencilClearFlags.Depth, 1.0f, 0);
                draw.DrawSceneForShadows(holder, _cameras[i]);
            }
        }

        protected override void ClearBuffers(DeviceContextHolder holder) {
            foreach (var view in _targetView) {
                holder.DeviceContext.ClearDepthStencilView(view, DepthStencilClearFlags.Depth, 1.0f, 0);
            }
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _view);
            DisposeHelper.Dispose(ref _targetView);
        }
    }
}
