using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Temporary;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificSpecial {
    public abstract class UtilsRendererBase : BaseRenderer {
        [NotNull]
        protected static IEnumerable<IKn5RenderableObject> Flatten(RenderableList root, Func<IRenderableObject, bool> filter = null) {
            return root
                    .SelectManyRecursive(x => {
                        var list = x as Kn5RenderableList;
                        if (list == null || !list.IsEnabled) return null;
                        return filter?.Invoke(list) == false ? null : list;
                    })
                    .OfType<IKn5RenderableObject>()
                    .Where(x => x.IsEnabled && filter?.Invoke(x) != false);
        }

        [NotNull]
        protected static IEnumerable<IKn5RenderableObject> Flatten(Kn5 kn5, RenderableList root, [CanBeNull] string textureName,
                [CanBeNull] string objectPath) {
            var split = Lazier.Create(() => objectPath?.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));

            bool TestObjectPath(IKn5RenderableObject obj) {
                var s = split.Value;
                if (s == null || s.Length < 1) return true;
                if (s[s.Length - 1] != obj.OriginalNode.Name) return false;
                return kn5.GetObjectPath(obj.OriginalNode) == objectPath;
            }

            return Flatten(root, x => {
                var k = x as IKn5RenderableObject;
                if (k == null) return true;
                if (!TestObjectPath(k)) return false;
                if (textureName == null) return true;
                var material = kn5.GetMaterial(k.OriginalNode.MaterialId);
                return material != null && material.TextureMappings.Where(y => y.Name != "txDetail" && y.Name != "txNormalDetail")
                                                   .Any(m => m.Texture == textureName);
            });
        }
    }

    public abstract class ShadowsRendererBase : UtilsRendererBase {
        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        [NotNull]
        protected readonly Kn5 Kn5;

        [NotNull]
        protected readonly RenderableList Scene;

        [CanBeNull]
        protected readonly CarData CarData;

        protected RenderableList CarNode { get; private set; }

        protected ShadowsRendererBase([NotNull] Kn5 kn5, [CanBeNull] DataWrapper carData) {
            Kn5 = kn5;
            Scene = new RenderableList();
            CarData = carData == null ? null : new CarData(carData);
        }

        protected override void ResizeInner() { }

        public float UpDelta = 0.0f;

        protected override void InitializeInner() {
            DeviceContextHolder.Set<IMaterialsFactory>(new DepthMaterialsFactory());
            DeviceContextHolder.Set<IAlphaTexturesProvider>(new AlphaTexturesProvider(Kn5));
            CarNode = (RenderableList)Kn5RenderableDepthOnlyObject.Convert(Kn5.RootNode);
            Scene.Add(CarNode);

            ApplyCarState();
            Scene.UpdateBoundingBox();
        }

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
            private readonly Kn5 _kn5;

            public AlphaTexturesProvider(Kn5 kn5) {
                _kn5 = kn5;
            }

            private Kn5TexturesProvider _texturesProvider;
            private readonly Dictionary<uint, Tuple<IRenderableTexture, float>[]> _cache = new Dictionary<uint, Tuple<IRenderableTexture, float>[]>();

            Tuple<IRenderableTexture, float> IAlphaTexturesProvider.GetTexture(IDeviceContextHolder contextHolder, uint materialId) {
                Tuple<IRenderableTexture, float>[] result;
                if (!_cache.TryGetValue(materialId, out result)) {
                    if (_texturesProvider == null) {
                        _texturesProvider = new Kn5TexturesProvider(_kn5, false);
                    }

                    result = new Tuple<IRenderableTexture, float>[1];

                    var material = _kn5.GetMaterial(materialId);
                    if (material != null && (material.BlendMode != Kn5MaterialBlendMode.Opaque || material.AlphaTested)) {
                        var normalsAlpha = material.ShaderName == "ksPerPixelNM" || material.ShaderName == "ksPerPixelNM_UV2" ||
                                material.ShaderName.Contains("_AT") || material.ShaderName == "ksSkinnedMesh";
                        var textureName = material.GetMappingByName(normalsAlpha ? "txNormal" : "txDiffuse")?.Texture;
                        var alphaRef = material.GetPropertyValueAByName("ksAlphaRef");
                        if (textureName != null && !material.ShaderName.Contains("damage") && alphaRef > 0f) {
                            var texture = _texturesProvider.GetTexture(contextHolder, textureName);
                            result = new[] { Tuple.Create(texture, alphaRef) };
                        } else {
                            result = new Tuple<IRenderableTexture, float>[] { null };
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