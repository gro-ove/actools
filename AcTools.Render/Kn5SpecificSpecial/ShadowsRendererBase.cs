using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificSpecial {
    public abstract class ShadowsRendererBase : UtilsRendererBase {
        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        [NotNull]
        protected readonly IKn5 Kn5;

        [NotNull]
        protected readonly RenderableList Scene;

        [CanBeNull]
        protected readonly CarData CarData;

        protected RenderableList CarNode { get; private set; }

        protected ShadowsRendererBase([NotNull] IKn5 kn5, [CanBeNull] DataWrapper carData) {
            Kn5 = kn5;
            Scene = new RenderableList();
            CarData = carData == null ? null : new CarData(carData);
        }

        protected override void ResizeInner() { }

        public int Iterations = 500;
        public bool PoissonSampling = true;
        public float ΘFromDeg = -10f;
        public float ΘToDeg = 50f;

        protected override void InitializeInner() {
            DeviceContextHolder.Set<IMaterialsFactory>(new DepthMaterialsFactory());
            DeviceContextHolder.Set<IAlphaTexturesProvider>(new AlphaTexturesProvider(Kn5));
            CarNode = (RenderableList)Kn5DepthOnlyConverter.Instance.Convert(Kn5.RootNode);
            Scene.Add(CarNode);

            ApplyCarState();
            Scene.UpdateBoundingBox();
        }

        #region Light sources distribution
        private List<Vector2> _poissonDisk;
        private static WeakReference<List<Vector2>> _poissonDiskWeak;

        private static void CropPoisson(Vector2[] points, float yFrom, float yTo) {
            var rangeHeight = (yTo - yFrom).Abs();
            var bands = Math.Max((float)Math.Floor(2f / rangeHeight), 1f);
            var bandHeight = 2f / bands;

            for (var i = 0; i < points.Length; i++) {
                var point = points[i];
                var bandIndex = Math.Min((int)((point.Y + 1f) / bandHeight), bands - 1);
                var bandStart = bandIndex * bandHeight - 1f;
                var yInBand = yFrom + (yTo - yFrom) * (point.Y - bandStart) / bandHeight;
                points[i].X = (point.X + 2f * bandIndex + 1f) / bands - 1f;
                points[i].Y = yInBand;
            }
        }

        private IEnumerable<Vector3> GetLightsDistributionPoisson(int size, float θFrom, float θTo) {
            Vector2[] poisson;
            if (size >= 1000) {
                if (_poissonDisk == null && _poissonDiskWeak != null && _poissonDiskWeak.TryGetTarget(out var value)) {
                    _poissonDisk = value;
                }

                if (_poissonDisk?.Count != size) {
                    _poissonDisk = UniformPoissonDiskSampler.SampleSquare(size, false);
                    _poissonDiskWeak = new WeakReference<List<Vector2>>(_poissonDisk);
                }

                poisson = _poissonDisk.ToArray();
            } else {
                poisson = UniformPoissonDiskSampler.SampleSquare(size, false).ToArray();
            }

            CropPoisson(poisson, (θFrom.ToRadians() + MathF.PI / 2f).Cos(), (θTo.ToRadians() + MathF.PI / 2f).Cos());
            foreach (var point in poisson) {
                var θ = point.Y.Clamp(-0.9999f, 0.9999f).Acos() - MathF.PI / 2f;
                var φ = point.X * MathF.PI;
                var direction = MathF.ToVector3Rad(θ, φ);
                yield return direction;
            }
        }

        private static IEnumerable<Vector3> GetLightsDistributionRandom(int size, float θFrom, float θTo) {
            var yFrom = (90f - θFrom).ToRadians().Cos();
            var yTo = (90f - θTo).ToRadians().Cos();

            if (yTo < yFrom) {
                throw new Exception("yTo < yFrom");
            }

            while (size-- > 0) {
                var vn = default(Vector3);
                var length = 0f;

                do {
                    var x = MathF.Random(-1f, 1f);
                    var y = MathF.Random(yFrom < 0f ? -1f : 0f, yTo > 0f ? 1f : 0f);
                    var z = MathF.Random(-1f, 1f);
                    if (x.Abs() < 0.01 && z.Abs() < 0.01) continue;

                    var v3 = new Vector3(x, y, z);
                    length = v3.Length();
                    vn = v3 / length;
                } while (length > 1f || vn.Y < yFrom || vn.Y > yTo);
                yield return vn;
            }
        }

        public IEnumerable<Vector3> GetLightsDistribution() {
            return PoissonSampling
                    ? GetLightsDistributionPoisson(Iterations, ΘFromDeg, ΘToDeg)
                    : GetLightsDistributionRandom(Iterations, ΘFromDeg, ΘToDeg);
        }
        #endregion

        #region Drawing
        protected abstract float DrawLight(Vector3 direction);

        protected float DrawLights(IProgress<double> progress, CancellationToken cancellation) {
            var t = Iterations;
            var iteration = 0;
            var summaryBrightness = 0f;

            foreach (var vec in GetLightsDistribution()) {
                if (++iteration % 10 == 9) {
                    progress?.Report((double)iteration / t);
                    if (cancellation.IsCancellationRequested) return 0f;
                }

                summaryBrightness += DrawLight(vec);
            }

            return summaryBrightness;
        }
        #endregion

        #region Copy actual car state from existing renderer
        private List<string> _hiddenNodes;
        private bool _leftDoorOpen;
        private bool _rightDoorOpen;
        private bool _headlightsEnabled;
        private bool[] _wingsStates;
        private bool[] _extraAnimationsStates;
        private bool _cockpitLrActive;
        private bool _seatbeltOnActive;
        private bool _blurredNodesActive;
        private bool _dataWheels;
        private CarSuspensionModifiers _suspensionModifiers;

        protected bool IsVisible(IRenderableObject obj) {
            return _hiddenNodes?.Contains(obj.Name) != true;
        }

        public void CopyStateFrom([CanBeNull] ToolsKn5ObjectRenderer renderer) {
            if (renderer == null) return;
            _hiddenNodes = renderer.GetHiddenNodesNames().ToList();

            var car = renderer.CarNode;
            if (car != null) {
                _leftDoorOpen = car.LeftDoorOpen;
                _rightDoorOpen = car.RightDoorOpen;
                _headlightsEnabled = car.HeadlightsEnabled;
                _cockpitLrActive = car.CockpitLrActive;
                _seatbeltOnActive = car.SeatbeltOnActive;
                _blurredNodesActive = car.BlurredNodesActive;
                _dataWheels = car.AlignWheelsByData;
                _suspensionModifiers = car.SuspensionModifiers;
                _wingsStates = car.Wings.Select(x => x.Value > 0.1f).ToArray();
                _extraAnimationsStates = car.Extras.Select(x => x.Value > 0.1f).ToArray();
            }
        }

        private void ApplyCarState() {
            var carData = CarData;
            if (carData == null) return;

            if (_dataWheels) {
                Kn5RenderableFile.UpdateModelMatrixInverted(CarNode);
                Kn5RenderableCar.SetWheelsByData(CarNode, carData.GetWheels(_suspensionModifiers), Matrix.Invert(carData.GetGraphicMatrix()));
            }

            Kn5RenderableCar.AdjustPosition(CarNode);

            if (_leftDoorOpen) {
                Kn5RenderableCar.CreateAnimator(carData.CarDirectory, carData.GetLeftDoorAnimation())?.SetImmediate(CarNode, 1f, null);
            }

            if (_rightDoorOpen) {
                Kn5RenderableCar.CreateAnimator(carData.CarDirectory, carData.GetRightDoorAnimation())?.SetImmediate(CarNode, 1f, null);
            }

            if (_headlightsEnabled) {
                foreach (var animation in carData.GetLightsAnimations()) {
                    Kn5RenderableCar.CreateAnimator(carData.CarDirectory, animation)?.SetImmediate(CarNode, 1f, null);
                }
            }

            Kn5RenderableCar.SetCockpitLrActive(CarNode, _cockpitLrActive);
            Kn5RenderableCar.SetSeatbeltActive(CarNode, _seatbeltOnActive);
            Kn5RenderableCar.SetBlurredObjects(CarNode, carData.GetBlurredObjects().ToArray(), _blurredNodesActive ? 100f : 0f);

            if (_wingsStates != null) {
                var i = 0;
                foreach (var animation in carData.GetWingsAnimations()) {
                    if (_wingsStates[i++]) {
                        Kn5RenderableCar.CreateAnimator(carData.CarDirectory, animation)?.SetImmediate(CarNode, 1f, null);
                    }
                }
            }

            if (_extraAnimationsStates != null) {
                var i = 0;
                foreach (var animation in carData.GetExtraAnimations()) {
                    if (_extraAnimationsStates[i++]) {
                        Kn5RenderableCar.CreateAnimator(carData.CarDirectory, animation)?.SetImmediate(CarNode, 1f, null);
                    }
                }
            }
        }
        #endregion

        private class AlphaTexturesProvider : IAlphaTexturesProvider {
            private readonly IKn5 _kn5;

            public AlphaTexturesProvider(IKn5 kn5) {
                _kn5 = kn5;
            }

            private Kn5TexturesProvider _texturesProvider;
            private readonly Dictionary<uint, Tuple<IRenderableTexture, float>[]> _cache = new Dictionary<uint, Tuple<IRenderableTexture, float>[]>();

            Tuple<IRenderableTexture, float> IAlphaTexturesProvider.GetTexture(IDeviceContextHolder contextHolder, uint materialId) {
                if (!_cache.TryGetValue(materialId, out var result)) {
                    if (_texturesProvider == null) {
                        _texturesProvider = new Kn5TexturesProvider(_kn5, false);
                    }

                    result = new Tuple<IRenderableTexture, float>[] { null };

                    var material = _kn5.GetMaterial(materialId);
                    if (material != null && (material.BlendMode != Kn5MaterialBlendMode.Opaque || material.AlphaTested)) {
                        var shader = material.ShaderName;
                        var normalsAlpha = shader == "ksPerPixelNM" || shader == "ksPerPixelNM_UV2" || shader.Contains("_AT") || shader == "ksSkinnedMesh";
                        if (normalsAlpha || !shader.Contains("MultiMap") && shader != "ksTyres" && shader != "ksBrakeDisc") {
                            var textureName = material.GetMappingByName(normalsAlpha ? "txNormal" : "txDiffuse")?.Texture;
                            var alphaRef = material.GetPropertyValueAByName("ksAlphaRef");
                            if (textureName != null && !shader.Contains("damage")) {
                                result = new[] { Tuple.Create(_texturesProvider.GetTexture(contextHolder, textureName), alphaRef) };
                            }
                        }
                    }

                    _cache[materialId] = result;
                }

                return result[0];
            }

            public void Dispose() {
                DisposeHelper.Dispose(ref _texturesProvider);
                _cache.Clear();
            }
        }
    }
}