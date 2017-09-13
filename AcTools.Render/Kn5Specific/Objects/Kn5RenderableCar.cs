// #define BB_PERF_PROFILE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Animations;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using TaskExtension = AcTools.Utils.Helpers.TaskExtension;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar : Kn5RenderableFile, INotifyPropertyChanged {
        /// <summary>
        /// Fix messed up KNH file by loading steering animation immediately.
        /// </summary>
        public static bool OptionFixKnh = true;

        public const string DefaultSkin = "";
        public static bool OptionRepositionLod = false;

        private readonly string _rootDirectory, _skinsDirectory;
        private readonly bool _scanForSkins;

        private readonly CarData _carData;
        private Kn5OverrideableTexturesProvider _texturesProvider;

        [NotNull]
        private readonly Kn5 _lodA;
        private readonly RenderableList _ambientShadows;

        [CanBeNull]
        private DataWrapper _listeningData;

        public string RootDirectory => _rootDirectory;

        public Kn5RenderableCar(CarDescription car, Matrix matrix, [CanBeNull] IAcCarSoundFactory soundFactory, string selectSkin = DefaultSkin,
                bool scanForSkins = true, float shadowsHeight = 0.0f, bool asyncTexturesLoading = true, bool asyncOverrideTexturesLoading = false,
                bool allowSkinnedObjects = false)
                : base(car.Kn5LoadedRequire, matrix, asyncTexturesLoading, allowSkinnedObjects) {
            CreateSoundEmittersAndSmoothers();

            _rootDirectory = car.CarDirectoryRequire;

            _skinsDirectory = FileUtils.GetCarSkinsDirectory(_rootDirectory);
            _scanForSkins = scanForSkins;
            _shadowsHeight = shadowsHeight;
            _asyncOverrideTexturesLoading = asyncOverrideTexturesLoading;

            // Data = DataWrapper.FromDirectory(_rootDirectory);
            _carData = car.Data != null ? new CarData(car.Data) : new CarData(car.CarDirectoryRequire);
            if (car.Data != null) {
                _listeningData = car.Data;
                car.Data.DataChanged += OnDataChanged;
            }

            _ambientShadows = new RenderableList("_shadows", Matrix.Identity, LoadAmbientShadows());
            Add(_ambientShadows);

            if (_scanForSkins) {
                ReloadSkins(null, selectSkin);
            }

            var mainKn5 = _carData.IsEmpty ? car.MainKn5File : _carData.GetMainKn5(_rootDirectory);
            _lodA = FileUtils.ArePathsEqual(car.MainKn5File, mainKn5) ? car.Kn5LoadedRequire : Kn5.FromFile(mainKn5);

            _lods = _carData.GetLods().ToList();
            _currentLod = _lods.FindIndex(x => string.Equals(x.FileName, Path.GetFileName(car.MainKn5File), StringComparison.OrdinalIgnoreCase));
            _currentLodObject = _mainLodObject = new LodObject(RootObject);
            _lodsObjects[_currentLod] = _currentLodObject;

            /*foreach (var r in this.GetAllChildren().OfType<RenderableList>()) {
                r.HighlightBoundingBoxes = true;
            }*/

            AdjustPosition();
            UpdatePreudoSteer();
            UpdateTogglesInformation();

            IsReflectable = false;
            SuspensionModifiers.PropertyChanged += OnSuspensionModifiersChanged;

            if (soundFactory != null) {
                TaskExtension.Forget(InitializeSoundAsync(soundFactory));
            }
        }

        protected override ITexturesProvider InitializeTextures(IDeviceContextHolder contextHolder) {
            _texturesProvider = new Kn5OverrideableTexturesProvider(_lodA, AsyncTexturesLoading, _asyncOverrideTexturesLoading) {
                LiveReload = LiveReload,
                MagickOverride = MagickOverride
            };

            if (CurrentSkin != null) {
                _texturesProvider.SetOverridesDirectory(contextHolder, Path.Combine(_skinsDirectory, CurrentSkin));
            }

            return _texturesProvider;
        }

        private SharedMaterials _ambientShadowsMaterials;
        private DirectoryTexturesProvider _ambientShadowsTextures;
        private IDeviceContextHolder _ambientShadowsHolder;

        private void InitializeAmbientShadows(IDeviceContextHolder contextHolder) {
            _ambientShadowsTextures = new DirectoryTexturesProvider(AsyncTexturesLoading, _asyncOverrideTexturesLoading, true);
            _ambientShadowsTextures.SetDirectory(contextHolder, _rootDirectory);
            _ambientShadowsMaterials = new SharedMaterials(contextHolder.Get<IMaterialsFactory>());
            _ambientShadowsHolder = new Kn5LocalDeviceContextHolder(contextHolder, _ambientShadowsMaterials, _ambientShadowsTextures, this);
        }

        public void DrawAmbientShadows(IDeviceContextHolder contextHolder, ICamera camera) {
            /* shadows */
            if (_ambientShadowsTextures == null) {
                InitializeAmbientShadows(contextHolder);
            }

            _ambientShadows.Draw(_ambientShadowsHolder, camera, SpecialRenderMode.Simple);
        }

        public override void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (camera != null && Sound != null && mode == SpecialRenderMode.Simple && IsSoundActive) {
                UpdateSound(camera, holder);
            }

            DrawInitialize(holder);

            if (!_mirrorsInitialized) {
                LoadMirrors(holder);
            }

            /* driver, transparent */
            if (mode == SpecialRenderMode.SimpleTransparent) {
                // This way, transparent part of driver’s helmet will be rendered before car’s windows
                DrawDriver(holder, camera, mode);
            }

            /* car */
            _currentLodObject.EnsurePrepared(LocalHolder, holder, SharedMaterials, TexturesProvider, this);
            RootObject.Draw(_currentLodObject.Holder, camera, mode, filter);

            if (Skins != null && !_skinsWatcherSet) {
                SkinsWatcherSet(holder);
            }

            if (!_actuallyLoaded) {
                SelectSkin(holder, CurrentSkin);
            }

            /* driver, opaque */
            if (mode != SpecialRenderMode.SimpleTransparent) {
                DrawDriver(holder, camera, mode);
            }

            /* crew */
            DrawCrew(holder, camera, mode);

            /* collider */
            if (IsColliderVisible) {
                _colliderMesh?.Draw(holder, camera, mode, filter);
            }
        }

        private int? _trianglesCount;
        private int? _objectsCount;

        public int TrianglesCount => _trianglesCount ?? (_trianglesCount = RootObject.GetTrianglesCount()).Value;

        public int ObjectsCount => _objectsCount ?? (_objectsCount = RootObject.GetObjectsCount()).Value;

        private void InvalidateCount() {
            _trianglesCount = null;
            _objectsCount = null;
            OnPropertyChanged(nameof(TrianglesCount));
            OnPropertyChanged(nameof(ObjectsCount));
        }

        public event EventHandler ObjectsChanged;

        private void OnRootObjectChanged() {
            ReenableLights();
            ReenableWipers();
            ReenableWings();
            ReenableExtras();
            ReenableDoors();
            ReenableShiftAnimation();
            OnRootObjectChangedWheels();
            UpdateTogglesInformation();
            UpdateBoundingBox();
        }

        #region LODs
        private readonly IReadOnlyList<CarData.LodDescription> _lods;

        public int LodsCount => _lods.Count;

        [CanBeNull]
        public CarData.LodDescription CurrentLodInformation => _currentLod < 0 || _currentLod >= _lods.Count ? null : _lods[_currentLod];

        private int _currentLod;

        public int CurrentLod {
            get => _currentLod;
            set {
                if (Equals(value, _currentLod)) return;
                _currentLod = value;

                var lod = _lods.ElementAtOrDefault(value);
                if (lod == null) {
                    throw new Exception($"LOD #{value} not found");
                }

                var debugMode = _currentLodObject.DebugMode;
                Remove(_currentLodObject.Renderable);
                if (!_lodsObjects.TryGetValue(value, out _currentLodObject)) {
                    var path = Path.GetFullPath(Path.Combine(_rootDirectory, lod.FileName));
                    var kn5 = value == 0 ? _lodA : Kn5.FromFile(path);
                    _currentLodObject = new LodObject(kn5, AllowSkinnedObjects);
                    _lodsObjects[value] = _currentLodObject;
                    Insert(0, _currentLodObject.Renderable);
                    RootObject = _currentLodObject.Renderable;

                    if (OptionRepositionLod) {
                        AdjustPosition();
                    } else {
                        _currentLodObject.Renderable.LocalMatrix = _mainLodObject.Renderable.LocalMatrix;
                    }
                } else {
                    Insert(0, _currentLodObject.Renderable);
                    RootObject = _currentLodObject.Renderable;
                }

                OnRootObjectChanged();
                _currentLodObject.DebugMode = debugMode;

                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentLodInformation));
                ObjectsChanged?.Invoke(this, EventArgs.Empty);

                InvalidateCount();
            }
        }

        private class LodObject : IDisposable {
            public readonly RenderableList Renderable;
            public Kn5SharedMaterials Materials;
            internal IDeviceContextHolder Holder;
            internal readonly Kn5 NonDefaultKn5;
            private bool _prepared;

            public LodObject(RenderableList rootObject) {
                NonDefaultKn5 = null;
                Renderable = rootObject;
            }

            public LodObject(Kn5 kn5, bool allowSkinnedObjects) {
                NonDefaultKn5 = kn5;
                Renderable = (Kn5RenderableList)Convert(kn5.RootNode, allowSkinnedObjects);
            }

            private Dictionary<string, Matrix> _originalLocalMatrices;

            public Matrix GetOriginalLocalMatrix([NotNull] RenderableList node) {
                var name = node.Name ?? "";

                if (_originalLocalMatrices == null) {
                    _originalLocalMatrices = new Dictionary<string, Matrix>(3);
                }

                Matrix original;
                if (!_originalLocalMatrices.TryGetValue(name, out original)) {
                    original = _originalLocalMatrices[name] = node.LocalMatrix;
                }

                return original;
            }

            private Dictionary<string, Matrix> _originalRelativeToModelMatrices;

            public Matrix GetOriginalRelativeToModelMatrix([NotNull] Kn5RenderableList node) {
                var name = node.Name ?? "";

                if (_originalRelativeToModelMatrices == null) {
                    _originalRelativeToModelMatrices = new Dictionary<string, Matrix>(3);
                }

                Matrix original;
                if (!_originalRelativeToModelMatrices.TryGetValue(name, out original)) {
                    original = _originalRelativeToModelMatrices[name] = node.RelativeToModel;
                }

                return original;
            }

            public void Dispose() {
                Materials?.Dispose();
                Renderable?.Dispose();
            }

            public void EnsurePrepared(IDeviceContextHolder localHolder, IDeviceContextHolder globalHolder, Kn5SharedMaterials materials,
                    ITexturesProvider texturesProvider, IKn5Model model) {
                if (_prepared) return;
                _prepared = true;

                if (Materials == null) {
                    if (NonDefaultKn5 == null) {
                        Materials = materials;
                        Holder = localHolder;
                    } else {
                        Materials = new Kn5SharedMaterials(globalHolder, NonDefaultKn5);
                        Holder = new Kn5LocalDeviceContextHolder(globalHolder, Materials, texturesProvider, model);
                    }

                    if (_debugModeSetLater) {
                        SetDebugMode(Holder, true);
                        _debugModeSetLater = false;
                    }
                }
            }

            private bool _debugMode, _debugModeSetLater;

            internal bool DebugMode {
                get => _debugMode;
                set {
                    if (Equals(value, _debugMode)) return;
                    _debugMode = value;

                    if (Holder != null || !value) {
                        SetDebugMode(Holder, value);
                        _debugModeSetLater = false;
                    } else {
                        _debugModeSetLater = true;
                    }
                }
            }

            private void SetDebugMode(IDeviceContextHolder localHolder, bool enabled) {
                foreach (var node in Renderable.GetAllChildren().OfType<IKn5RenderableObject>()) {
                    node.SetDebugMode(localHolder, enabled);
                }
            }
        }

        private LodObject _currentLodObject;
        private readonly LodObject _mainLodObject;
        private readonly Dictionary<int, LodObject> _lodsObjects = new Dictionary<int, LodObject>(1);
        #endregion

        #region Skins
        private bool _liveReload = true;

        public bool LiveReload {
            get => _liveReload;
            set {
                if (Equals(value, _liveReload)) return;
                _liveReload = value;
                if (_texturesProvider != null) {
                    _texturesProvider.LiveReload = value;
                }
                OnPropertyChanged();

                if (_driver != null) {
                    _driver.LiveReload = LiveReload;
                }

                UpdateCrewParams();
            }
        }

        private bool _magickOverride;

        public bool MagickOverride {
            get => _magickOverride;
            set {
                if (Equals(value, _magickOverride)) return;
                _magickOverride = value;
                if (_texturesProvider != null) {
                    _texturesProvider.MagickOverride = value;
                }
                OnPropertyChanged();

                if (_driver != null) {
                    _driver.MagickOverride = MagickOverride;
                }

                UpdateCrewParams();
            }
        }

        [CanBeNull]
        public List<string> Skins { get; private set; }

        [CanBeNull]
        public string CurrentSkin {
            get => _currentSkin;
            private set {
                if (value == _currentSkin) return;
                _currentSkin = value;
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        private IDeviceContextHolder _skinsWatcherHolder;
        private bool _actuallyLoaded, _skinsWatcherSet;
        private FileSystemWatcher _skinsWatcher;

        private void SkinsWatcherSet(IDeviceContextHolder contextHolder) {
            if (_skinsWatcherSet || !_scanForSkins) return;
            _skinsWatcherSet = true;

            if (!Directory.Exists(_skinsDirectory)) return;
            _skinsWatcherHolder = contextHolder;
            _skinsWatcher = new FileSystemWatcher(_skinsDirectory) {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.DirectoryName
            };
            _skinsWatcher.Changed += SkinsWatcherUpdate;
            _skinsWatcher.Created += SkinsWatcherUpdate;
            _skinsWatcher.Deleted += SkinsWatcherUpdate;
            _skinsWatcher.Renamed += SkinsWatcherUpdate;
            _skinsWatcher.EnableRaisingEvents = true;
        }

        private void SkinsWatcherUpdate(object sender, FileSystemEventArgs e) {
            ReloadSkins(_skinsWatcherHolder, CurrentSkin);
        }

        private void ReloadSkins([CanBeNull] IDeviceContextHolder contextHolder, string selectSkin) {
            if (!_scanForSkins) return;
            var selectedIndex = Skins?.IndexOf(selectSkin);

            try {
                var directory = new DirectoryInfo(_skinsDirectory);
                Skins = directory.Exists ? directory.GetDirectories().Select(x => x.Name.ToLower()).ToList() : null;
            } catch (Exception) {
                Skins = null;
            }

            string skinId;
            if (Skins == null || selectSkin == null) {
                skinId = null;
            } else if (selectSkin == DefaultSkin) {
                skinId = Skins.FirstOrDefault();
            } else {
                skinId = Skins.FirstOrDefault(x => string.Equals(x, selectSkin, StringComparison.OrdinalIgnoreCase)) ??
                     Skins.ElementAtOrDefault(selectedIndex ?? 0) ?? Skins.FirstOrDefault();
            }

            if (contextHolder == null) {
                CurrentSkin = skinId;
                _actuallyLoaded = false;
            } else {
                SelectSkin(contextHolder, skinId);
                contextHolder.RaiseUpdateRequired();
            }
        }

        public void SelectNextSkin(IDeviceContextHolder contextHolder) {
            if (Skins?.Any() != true) return;

            if (contextHolder == null) {
                contextHolder = _skinsWatcherHolder;
                if (contextHolder == null) return;
            }

            var index = Skins.IndexOf(CurrentSkin);
            SelectSkin(contextHolder, index < 0 || index >= Skins.Count - 1 ? Skins[0] : Skins[index + 1]);
        }

        public void SelectPreviousSkin(IDeviceContextHolder contextHolder) {
            if (Skins?.Any() != true) return;

            if (contextHolder == null) {
                contextHolder = _skinsWatcherHolder;
                if (contextHolder == null) return;
            }

            var index = Skins.IndexOf(CurrentSkin);
            SelectSkin(contextHolder, index <= 0 ? Skins[Skins.Count - 1] : Skins[index - 1]);
        }

        public void SelectSkin(IDeviceContextHolder contextHolder, [CanBeNull] string skinId) {
            if (contextHolder == null) {
                contextHolder = _skinsWatcherHolder;
                if (contextHolder == null) return;
            }

            if (skinId == DefaultSkin) {
                skinId = Skins?.FirstOrDefault();
            }

            var skinIdLower = skinId?.ToLower();
            if (Equals(CurrentSkin, skinIdLower) && _actuallyLoaded) return;

            CurrentSkin = skinIdLower;

            if (_texturesProvider != null) {
                if (skinId == null) {
                    _texturesProvider.ClearOverridesDirectory();
                    _driver?.ClearOverridesDirectory();
                    _crewMain?.ClearOverridesDirectory();
                    _crewTyres?.ClearOverridesDirectory();
                    _crewStuff?.ClearOverridesDirectory();
                } else {
                    var skinDirectory = Path.Combine(_skinsDirectory, skinId);
                    _texturesProvider.SetOverridesDirectory(contextHolder, skinDirectory);
                    _driver?.SetOverridesDirectory(contextHolder, skinDirectory);
                    _crewMain?.SetOverridesDirectory(contextHolder, skinDirectory);
                    _crewTyres?.SetOverridesDirectory(contextHolder, skinDirectory);
                    _crewStuff?.SetOverridesDirectory(contextHolder, skinDirectory);
                }
                contextHolder.RaiseUpdateRequired();
                _actuallyLoaded = true;
            } else {
                _actuallyLoaded = false;
            }
        }
        #endregion

        #region Rims (blurred/static), cockpit (HR/LR), seatbelt (on/off)
        public CarData.BlurredObject[] BlurredObjects => _blurredObjs ?? (_blurredObjs = _carData.GetBlurredObjects().ToArray());
        private CarData.BlurredObject[] _blurredObjs;

        private void UpdateTogglesInformation() {
            var hasCockpitLr = false;
            var hasCockpitHr = false;
            var cockpitLrActive = true;
            var hasSeatbeltOn = false;
            var hasSeatbeltOff = false;
            var seatbeltOnActive = true;
            var hasBlurredNodes = false;
            var blurredNodesActive = true;

            foreach (var dummy in _currentLodObject.Renderable.GetAllChildren().OfType<Kn5RenderableList>()) {
                switch (dummy.OriginalNode.Name) {
                    case "COCKPIT_LR":
                    case "STEER_LR":
                        hasCockpitLr = true;
                        cockpitLrActive &= dummy.IsEnabled;
                        break;
                    case "COCKPIT_HR":
                        hasCockpitHr = true;
                        break;
                    case "CINTURE_ON":
                        hasSeatbeltOn = true;
                        seatbeltOnActive &= dummy.IsEnabled;
                        break;
                    case "CINTURE_OFF":
                        hasSeatbeltOff = true;
                        break;
                    default:
                        if (BlurredObjects.Any(x => x.MinSpeed >= 0f && x.Name == dummy.OriginalNode.Name)) {
                            hasBlurredNodes = true;
                            blurredNodesActive &= dummy.IsEnabled;
                        }
                        break;
                }
            }

            if (HasCockpitLr != hasCockpitLr) {
                HasCockpitLr = hasCockpitLr;
                OnPropertyChanged(nameof(HasCockpitLr));
            }

            if (HasCockpitHr != hasCockpitHr) {
                HasCockpitHr = hasCockpitHr;
                OnPropertyChanged(nameof(HasCockpitHr));
            }

            var hasCockpitBoth = hasCockpitLr && hasCockpitHr;
            if (HasCockpitBoth != hasCockpitBoth) {
                HasCockpitBoth = hasCockpitBoth;
                OnPropertyChanged(nameof(HasCockpitBoth));
            }

            cockpitLrActive &= hasCockpitLr;
            if (_cockpitLrActive != cockpitLrActive) {
                _cockpitLrActive = cockpitLrActive;
                OnPropertyChanged(nameof(CockpitLrActive));
            }

            if (HasSeatbeltOff != hasSeatbeltOff) {
                HasSeatbeltOff = hasSeatbeltOff;
                OnPropertyChanged(nameof(HasSeatbeltOff));
            }

            if (HasSeatbeltOn != hasSeatbeltOn) {
                HasSeatbeltOn = hasSeatbeltOn;
                OnPropertyChanged(nameof(HasSeatbeltOn));
            }

            seatbeltOnActive &= hasSeatbeltOn;
            if (_seatbeltOnActive != seatbeltOnActive) {
                _seatbeltOnActive = seatbeltOnActive;
                OnPropertyChanged(nameof(SeatbeltOnActive));
            }

            if (HasBlurredNodes != hasBlurredNodes) {
                HasBlurredNodes = hasBlurredNodes;
                OnPropertyChanged(nameof(HasBlurredNodes));
            }

            blurredNodesActive &= hasBlurredNodes;
            if (_blurredNodesActive != blurredNodesActive) {
                _blurredNodesActive = blurredNodesActive;
                OnPropertyChanged(nameof(BlurredNodesActive));
            }
        }

        public bool HasCockpitLr { get; private set; }

        public bool HasCockpitBoth { get; private set; }

        public bool HasCockpitHr { get; private set; }

        private bool? _cockpitLrActive;

        public bool CockpitLrActive {
            get => _cockpitLrActive ?? false;
            set {
                if (Equals(value, _cockpitLrActive)) return;
                _cockpitLrActive = value;

                if (SetCockpitLrActive(_currentLodObject.Renderable, value)) {
                    _currentLodObject.Renderable.UpdateBoundingBox();
                    InvalidateCount();
                    _skinsWatcherHolder?.RaiseSceneUpdated();
                }

                OnPropertyChanged();
            }
        }

        public bool HasSeatbeltOn { get; private set; }

        public bool HasSeatbeltOff { get; private set; }

        private bool? _seatbeltOnActive;

        public bool SeatbeltOnActive {
            get => _seatbeltOnActive ?? false;
            set {
                if (Equals(value, _seatbeltOnActive)) return;
                _seatbeltOnActive = value;

                if (SetSeatbeltActive(_currentLodObject.Renderable, value)) {
                    _currentLodObject.Renderable.UpdateBoundingBox();
                    InvalidateCount();
                    _skinsWatcherHolder?.RaiseSceneUpdated();
                }

                OnPropertyChanged();
            }
        }

        public bool HasBlurredNodes { get; private set; }

        private bool? _blurredNodesActive;

        public bool BlurredNodesActive {
            get => _blurredNodesActive ?? false;
            set {
                if (Equals(value, _blurredNodesActive)) return;
                _blurredNodesActive = value;

                if (SetBlurredObjects(this, BlurredObjects, value ? 100f : 0f)) {
                    _currentLodObject.Renderable.UpdateBoundingBox();
                    InvalidateCount();
                    _skinsWatcherHolder?.RaiseSceneUpdated();
                }

                OnPropertyChanged();
            }
        }
        #endregion

        #region Adjust position
        private Matrix? _initiallyCalculatedPosition;

        public static void AdjustPosition([NotNull] RenderableList parent, Kn5RenderableCar car = null) {
            RenderableList DummyByName(string s) => car?.GetDummyByName(s) ?? parent.GetDummyByName(s);

            var node = parent;

#if BB_PERF_PROFILE
            var sw = Stopwatch.StartNew();
            node.UpdateBoundingBox();
            AcToolsLogging.Write($"Initial BB update: {sw.Elapsed.TotalMilliseconds:F1} ms");

            node.LocalMatrix = Matrix.Translation(1f, 0f, 0f);
            sw = Stopwatch.StartNew();
            node.UpdateBoundingBox();
            AcToolsLogging.Write($"Second BB update: {sw.Elapsed.TotalMilliseconds:F1} ms");

            node.LocalMatrix = Matrix.Identity;
#else
            node.UpdateBoundingBox();
#endif

            var wheelLf = DummyByName("WHEEL_LF");
            var wheelRf = DummyByName("WHEEL_RF");
            var wheelLr = DummyByName("WHEEL_LR");
            var wheelRr = DummyByName("WHEEL_RR");

            if (wheelLf == null || wheelRf == null || wheelLr == null || wheelRr == null ||
                    !wheelLf.BoundingBox.HasValue || !wheelRf.BoundingBox.HasValue ||
                    !wheelLr.BoundingBox.HasValue || !wheelRr.BoundingBox.HasValue) goto Fallback;

            var y1 = Math.Min((float)wheelLf.BoundingBox?.Minimum.Y, (float)wheelRf.BoundingBox?.Minimum.Y) - 0.001f;
            var y2 = Math.Min((float)wheelLr.BoundingBox?.Minimum.Y, (float)wheelRr.BoundingBox?.Minimum.Y) - 0.001f;
            if (float.IsPositiveInfinity(y1) || float.IsPositiveInfinity(y2)) goto Fallback;

            var x1 = node.GetDummyByName("WHEEL_LF")?.BoundingBox?.GetCenter().Z ?? 0f;
            var x2 = -node.GetDummyByName("WHEEL_LR")?.BoundingBox?.GetCenter().Z ?? 0f;
            var xSum = x1 + x2;
            if (xSum < 0.01f) goto Fallback;

            var y = y2 + x2 * (y1 - y2) / xSum;
            if ((float)Math.Atan2(y1 - y2, xSum).Abs() > 0.3f) {
                Debugger.Break();
            }
            node.LocalMatrix = Matrix.RotationX((float)Math.Atan2(y1 - y2, xSum)) * Matrix.Translation(0, -y, 0) * node.LocalMatrix;
            return;

            Fallback:
            node.LocalMatrix = Matrix.Translation(0, -node.BoundingBox?.Minimum.Y ?? 0f, 0) * node.LocalMatrix;
        }

        private void AdjustPosition() {
            AdjustPosition(RootObject, this);
            if (!_initiallyCalculatedPosition.HasValue) {
                _initiallyCalculatedPosition = RootObject.LocalMatrix;
            }
        }
        #endregion

        #region Mirrors
        private List<IKn5RenderableObject> _mirrors;
        private bool _mirrorsInitialized;

        private void LoadMirrors(IDeviceContextHolder holder) {
            _mirrorsInitialized = true;

            if (_mirrors != null) {
                foreach (var obj in _mirrors) {
                    obj.SetMirrorMode(holder, false);
                }
            }

            _mirrors = _carData.GetMirrorsNames().Select(name => RootObject.GetByName(name)).NonNull().ToList();
            foreach (var obj in _mirrors) {
                obj.SetMirrorMode(holder, true);
            }
        }
        #endregion

        #region Visible collider
        private IRenderableObject _colliderMesh;

        private bool _isColliderVisible;

        public bool IsColliderVisible {
            get => _isColliderVisible;
            set {
                if (Equals(value, _isColliderVisible)) return;
                _isColliderVisible = value;
                OnPropertyChanged();

                if (_colliderMesh == null) {
                    try {
                        _colliderMesh = new Kn5RenderableCollider(Kn5.FromFile(Path.Combine(_rootDirectory, "collider.kn5")), Matrix.Identity);
                    } catch (Exception e) {
                        AcToolsLogging.Write(e);
                        _colliderMesh = new InvisibleObject();
                    }
                }

                _skinsWatcherHolder?.RaiseUpdateRequired();

                if (value) {
                    Add(_colliderMesh);
                } else {
                    Remove(_colliderMesh);
                }

                ObjectsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion

        #region OnTick
        public void OnTick(float dt) {
            var dirty = false;

            if (_carLightsAnimators != null) {
                for (var i = 0; i < _carLightsAnimators.Length; i++) {
                    dirty |= _carLightsAnimators[i].OnTick(dt);
                }
            }

            if (_wings != null) {
                for (var i = 0; i < _wings.Length; i++) {
                    if (_wings[i].OnTick(dt)) {
                        UpdateWingLineAngle(_wings[i].Description.Id, _wings[i].Angle.ToRadians());
                        dirty = true;
                    }
                }
            }

            if (_extras != null) {
                for (var i = 0; i < _extras.Length; i++) {
                    dirty |= _extras[i].OnTick(dt);
                }
            }

            if (FansEnabled && _rotatingObjects != null) {
                for (var i = 0; i < _rotatingObjects.Length; i++) {
                    dirty |= _rotatingObjects[i].OnTick(RootObject, dt);
                }
            }

            dirty |= _driverShiftAnimator?.IsSet == true && (_driverShiftAnimator.Value?.OnTick(dt) ?? false);
            dirty |= _wipersAnimator?.IsSet == true && (_wipersAnimator.Value?.OnTick(dt) ?? false);
            dirty |= _doorLeftAnimator?.IsSet == true && (_doorLeftAnimator.Value?.OnTick(dt) ?? false);
            dirty |= _doorRightAnimator?.IsSet == true && (_doorRightAnimator.Value?.OnTick(dt) ?? false);

            if (IsCrewVisible) {
                dirty |= _crewAnimator?.IsSet == true && (_crewAnimator.Value?.OnTick(dt) ?? false);
            }

            dirty |= OnTickWheels(dt);

            if (dirty) {
                _skinsWatcherHolder?.RaiseSceneUpdated();
            }
        }
        #endregion

        #region Fans enabled
        private RotatingObject[] _rotatingObjects;

        public class RotatingObject {
            private readonly CarData.RotatingObject _description;
            private bool _dummySet;
            private Kn5RenderableList _dummy;
            private Matrix _baseMatrix;
            private RenderableList _parent;
            private float _rotation;

            public RotatingObject(CarData.RotatingObject description) {
                _description = description;
            }

            public bool OnTick(RenderableList parent, float dt) {
                if (!_dummySet || _parent != parent) {
                    _dummySet = true;

                    if (_dummy != null) {
                        _dummy.LocalMatrix = _baseMatrix;
                    }

                    _parent = parent;
                    _dummy = _parent.GetDummyByName(_description.NodeName);

                    if (_dummy != null) {
                        _baseMatrix = _dummy.LocalMatrix;
                    }
                }

                _rotation += _description.Rpm * dt * MathF.PI / 30f;

                if (_dummy != null) {
                    _dummy.LocalMatrix = Matrix.RotationAxis(_description.Axis, _rotation) * _baseMatrix;
                    return true;
                } else {
                    return false;
                }
            }
        }

        private void InitializeRotatingObjects() {
            if (_rotatingObjects != null) return;

            _rotatingObjects = _carData.GetRotatingObjects()
                             .Select(x => new RotatingObject(x))
                             .ToArray();
        }

        public bool HasFans {
            get {
                InitializeRotatingObjects();
                return _rotatingObjects.Length > 0;
            }
        }

        private bool _fansEnabled;

        public bool FansEnabled {
            get => _fansEnabled;
            set {
                if (Equals(value, _fansEnabled)) return;
                _fansEnabled = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                OnPropertyChanged();

                InitializeRotatingObjects();
            }
        }
        #endregion

        #region Extra animations
        public class ExtraAnimationEntry : AnimationEntryBase {
            public ExtraAnimationEntry(Kn5RenderableCar carNode, string ksAnimName, float duration) : base(carNode, ksAnimName, duration) {}

            protected override void OnActiveChanged(bool newValue) {
                CarNode.ToggleExtra(CarNode.Extras.IndexOf(this), newValue);
            }
        }

        private ExtraAnimationEntry[] _extras;

        public IReadOnlyList<ExtraAnimationEntry> Extras {
            get {
                InitializeExtras();
                return _extras;
            }
        }

        private void InitializeExtras() {
            if (_extras != null) return;

            _extras = _carData.GetExtraAnimations()
                             .Select(x => new ExtraAnimationEntry(this, x.KsAnimName, x.Duration))
                             .ToArray();
            OnPropertyChanged(nameof(Extras));
        }

        public void ToggleExtra(int index, bool? value = null) {
            InitializeExtras();

            var extra = _extras.ElementAtOrDefault(index);
            if (extra == null) return;

            var actualValue = value ?? Equals(extra.Value, 0f);
            extra.Update(RootObject, actualValue ? 1f : 0f, _skinsWatcherHolder);
            _skinsWatcherHolder?.RaiseSceneUpdated();
        }

        private void ResetExtras() {
            if (_extras != null) {
                for (var i = 0; i < _extras.Length; i++) {
                    _extras[i].Update(RootObject, 0f, _skinsWatcherHolder);
                }
            }

            if (_rotatingObjects != null) {
                _rotatingObjects = null;
                OnPropertyChanged(nameof(HasFans));

                if (FansEnabled) {
                    _fansEnabled = false;
                    FansEnabled = true;
                }
            }
        }

        private void ReenableExtras() {
            if (_extras == null) return;
            for (var i = 0; i < _extras.Length; i++) {
                _extras[i].Update(RootObject, _extras[i].Value, _skinsWatcherHolder);
            }
        }
        #endregion

        #region Wipers
        private Lazier<KsAnimAnimator> _wipersAnimator;

        private void InitializeWipers() {
            if (_wipersAnimator != null) return;
            _wipersAnimator = Lazier.Create(() => CreateAnimator(_rootDirectory, "car_wiper.ksanim"));
        }

        private void ReenableWipers() {
            if (_wipersAnimator?.IsSet == true) {
                if (_wipersEnabled) {
                    _wipersAnimator.Value?.Loop(RootObject);
                } else {
                    _wipersAnimator.Value?.SetTarget(RootObject, 0f, _skinsWatcherHolder);
                }
            }
        }

        private bool _wipersEnabled;

        public bool WipersEnabled {
            get => _wipersEnabled;
            set {
                if (Equals(value, _wipersEnabled)) return;
                _wipersEnabled = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                OnPropertyChanged();

                InitializeWipers();
                if (value) {
                    _wipersAnimator.Value?.Loop(RootObject);
                } else if (_wipersAnimator.Value?.Position > 0.5) {
                    _wipersAnimator.Value?.Loop(RootObject, 1);
                } else {
                    _wipersAnimator.Value?.SetTarget(RootObject, 0f, _skinsWatcherHolder);
                }
            }
        }

        public bool HasWipers {
            get {
                InitializeWipers();
                return _wipersAnimator.Value != null;
            }
        }
        #endregion

        #region Doors
        private readonly float DoorCloseSoundDuration = 0.21f;

        private Lazier<CarData.AnimationBase> _doorLeftAnimation;
        private Lazier<CarData.AnimationBase> _doorRightAnimation;
        private Lazier<KsAnimAnimator> _doorLeftAnimator;
        private Lazier<KsAnimAnimator> _doorRightAnimator;

        private void InitializeDoors() {
            if (_doorLeftAnimator != null) return;

            _doorLeftAnimation = Lazier.Create(() => _carData.GetLeftDoorAnimation());
            _doorRightAnimation = Lazier.Create(() => _carData.GetRightDoorAnimation());
            _doorLeftAnimator = Lazier.Create(() => CreateAnimator(_rootDirectory, _doorLeftAnimation.Value));
            _doorRightAnimator = Lazier.Create(() => CreateAnimator(_rootDirectory, _doorRightAnimation.Value));
        }

        private void ReenableDoors() {
            if (_doorLeftAnimator == null) return;
            if (_doorLeftAnimator.IsSet) _doorLeftAnimator.Value?.SetTarget(RootObject, _leftDoorOpen ? 1f : 0f, _skinsWatcherHolder);
            if (_doorRightAnimator.IsSet) _doorRightAnimator.Value?.SetTarget(RootObject, _rightDoorOpen ? 1f : 0f, _skinsWatcherHolder);
        }

        private bool _leftDoorOpen;

        public bool LeftDoorOpen {
            get => _leftDoorOpen;
            set {
                if (Equals(value, _leftDoorOpen)) return;
                _leftDoorOpen = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                OnPropertyChanged();

                InitializeDoors();
                _doorLeftAnimator.Value?.SetTarget(RootObject, value ? 1f : 0f, _skinsWatcherHolder);

                if (_skinsWatcherHolder?.TimeFactor == 1f && IsSoundActive) {
                    Sound?.Door(value, value ? TimeSpan.Zero :
                            TimeSpan.FromSeconds((_doorLeftAnimator.Value?.Position * _doorLeftAnimation.Value?.Duration ?? 1f) -
                                    DoorCloseSoundDuration));
                }
            }
        }

        public bool HasLeftDoorAnimation {
            get {
                InitializeDoors();
                return _doorLeftAnimator.Value != null;
            }
        }

        private bool _rightDoorOpen;

        public bool RightDoorOpen {
            get => _rightDoorOpen;
            set {
                if (Equals(value, _rightDoorOpen)) return;
                _rightDoorOpen = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                OnPropertyChanged();

                InitializeDoors();
                _doorRightAnimator.Value?.SetTarget(RootObject, value ? 1f : 0f, _skinsWatcherHolder);

                if (_skinsWatcherHolder?.TimeFactor == 1f && IsSoundActive) {
                    Sound?.Door(value, value ? TimeSpan.Zero :
                            TimeSpan.FromSeconds((_doorRightAnimator.Value?.Position * _doorRightAnimation.Value?.Duration ?? 1f) -
                                    DoorCloseSoundDuration));
                }
            }
        }

        public bool HasRightDoorAnimation {
            get {
                InitializeDoors();
                return _doorRightAnimator.Value != null;
            }
        }
        #endregion

        #region Wings
        public abstract class AnimationEntryBase : INotifyPropertyChanged {
            private readonly Lazier<KsAnimAnimator> _animator;
            protected readonly Kn5RenderableCar CarNode;
            internal float Value;

            protected AnimationEntryBase(Kn5RenderableCar carNode, string ksAnimName, float duration) {
                _animator = new Lazier<KsAnimAnimator>(() => CreateAnimator(carNode._rootDirectory, ksAnimName, duration));
                CarNode = carNode;
                DisplayName = GetKsAnimDisplayName(ksAnimName);
            }

            public string DisplayName { get; }

            private bool _active;

            public bool Active {
                get => _active;
                set {
                    if (Equals(value, _active)) return;
                    _active = value;
                    OnPropertyChanged();
                    OnActiveChanged(value);
                }
            }

            protected abstract void OnActiveChanged(bool newValue);

            internal void Update(RenderableList parent, float value, IDeviceContextHolder holder) {
                _animator.Value?.SetTarget(parent, value, holder);
                Value = value;
                OnPropertyChanged(nameof(Value));
            }

            internal bool OnTick(float dt) {
                if (!_animator.IsSet) return false;
                return _animator.Value?.OnTick(dt) ?? false;
            }

            public float Position => _animator.Value?.Position ?? 0f;

            public event PropertyChangedEventHandler PropertyChanged;

            [NotifyPropertyChangedInvocator]
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private static string GetKsAnimDisplayName(string fileName) {
            var s = Regex.Replace(fileName.ApartFromLast(".ksanim"), @"[^a-zA-Z0-9]+|(?<=[a-z])(?=[A-Z])", " ").Trim().Split(' ');
            var b = new List<string>();
            foreach (var p in s) {
                if (p.Length < 3 && !Regex.IsMatch(p, @"^(?:a|an|as|at|by|en|if|in|of|on|or|the|to|vs)$")) {
                    b.Add(p.ToUpperInvariant());
                } else {
                    b.Add(char.ToUpper(p[0]) + p.Substring(1).ToLowerInvariant());
                }
            }
            return b.JoinToString(' ');
        }

        public class SpoilerEntry : AnimationEntryBase {
            public readonly CarData.WingAnimation Description;

            public SpoilerEntry(Kn5RenderableCar carNode, CarData.WingAnimation description) : base(carNode,
                    description.KsAnimName, description.Duration) {
                Description = description;
            }

            public float Angle => Position * Description.AngleRange + Description.StartAngle;

            protected override void OnActiveChanged(bool newValue) {
                CarNode.ToggleWing(CarNode.Wings.IndexOf(this), newValue);
            }
        }

        private SpoilerEntry[] _wings;

        public IReadOnlyList<SpoilerEntry> Wings {
            get {
                InitializeWings();
                return _wings;
            }
        }

        private void InitializeWings() {
            if (_wings != null) return;

            _wings = _carData.GetWingsAnimations()
                             .Select(x => new SpoilerEntry(this, x))
                             .ToArray();
            OnPropertyChanged(nameof(Wings));
        }

        public void ToggleWing(int index, bool? value = null) {
            InitializeWings();

            var wing = _wings.ElementAtOrDefault(index);
            if (wing == null) return;

            var actualValue = value ?? Equals(wing.Value, 0f);

            if (actualValue) {
                foreach (var parent in _wings.Where(x => x.Description.Next == index && x.Value == 0f)) {
                    parent.Update(RootObject, 1f, _skinsWatcherHolder);
                }
            } else {
                _wings.ElementAtOrDefault(wing.Description.Next ?? -1)?.Update(RootObject, 0f, _skinsWatcherHolder);
            }

            wing.Update(RootObject, actualValue ? 1f : 0f, _skinsWatcherHolder);
            _skinsWatcherHolder?.RaiseSceneUpdated();
        }

        private void ResetWings() {
            if (_wings == null) return;
            for (var i = 0; i < _wings.Length; i++) {
                _wings[i].Update(RootObject, 0f, _skinsWatcherHolder);
            }
        }

        private void ReenableWings() {
            if (_wings == null) return;
            for (var i = 0; i < _wings.Length; i++) {
                _wings[i].Update(RootObject, _wings[i].Value, _skinsWatcherHolder);
            }
        }
        #endregion

        #region Measurements
        public float GetWheelbase() {
            var frontZ = RootObject.GetDummyByName("WHEEL_LF")?.Matrix.GetTranslationVector().Z ?? 0f;
            var rearZ = RootObject.GetDummyByName("WHEEL_LR")?.Matrix.GetTranslationVector().Z ?? 0f;
            return Math.Abs(frontZ - rearZ);
        }
        #endregion

        #region IKn5Model
        public override IKn5RenderableObject GetNodeByName(string name) {
            return RootObject.GetByName(name);
        }
        #endregion

        public bool ContainsNode(IKn5RenderableObject obj) {
            return _driver != null && _driver.GetAllChildren().Contains(obj) ||
                    _crewMain != null && _crewMain.GetAllChildren().Contains(obj) ||
                    _crewTyres != null && _crewTyres.GetAllChildren().Contains(obj) ||
                    _crewStuff != null && _crewStuff.GetAllChildren().Contains(obj) ||
                    RootObject.GetAllChildren().Contains(obj);
        }

        public Kn5 GetCurrentLodKn5() {
            return _currentLodObject.NonDefaultKn5 ?? OriginalFile;
        }

        [NotNull]
        public Kn5 GetKn5(IKn5RenderableObject obj) {
            if (_driver != null && _driver.GetAllChildren().Contains(obj)) {
                return _driver.OriginalFile;
            }

            if (_crewMain != null && _crewMain.GetAllChildren().Contains(obj)) {
                return _crewMain.OriginalFile;
            }

            if (_crewTyres != null && _crewTyres.GetAllChildren().Contains(obj)) {
                return _crewTyres.OriginalFile;
            }

            if (_crewStuff != null && _crewStuff.GetAllChildren().Contains(obj)) {
                return _crewStuff.OriginalFile;
            }

            return _currentLodObject.NonDefaultKn5 ?? OriginalFile;
        }

        [CanBeNull]
        public Kn5Material GetMaterial(IKn5RenderableObject obj) {
            return GetKn5(obj).GetMaterial(obj.OriginalNode.MaterialId);
        }

        #region Disposal, INotifyPropertyChanged stuff
        public override void Dispose() {
            base.Dispose();

            DisposeHelper.Dispose(ref _ambientShadowsTextures);
            _lodsObjects.Values.ApartFrom(_currentLodObject).DisposeEverything();
            if (_currentLodObject.Materials != null) {
                DisposeHelper.Dispose(ref _currentLodObject.Materials);
            }

            if (_skinsWatcher != null) {
                _skinsWatcher.EnableRaisingEvents = false;
                _skinsWatcher.Changed -= SkinsWatcherUpdate;
                _skinsWatcher.Created -= SkinsWatcherUpdate;
                _skinsWatcher.Deleted -= SkinsWatcherUpdate;
                _skinsWatcher.Renamed -= SkinsWatcherUpdate;
                DisposeHelper.Dispose(ref _skinsWatcher);
            }

            if (_listeningData != null) {
                _listeningData.DataChanged -= OnDataChanged;
                _listeningData = null;
            }

            DisposeHelper.Dispose(ref _suspensionLines);
            DisposeHelper.Dispose(ref _driver);
            DisposeHelper.Dispose(ref _driverModelWatcher);
            DisposeHelper.Dispose(ref _driverHierarchyWatcher);
            DisposeHelper.Dispose(ref _crewMain);
            DisposeHelper.Dispose(ref _crewTyres);
            DisposeHelper.Dispose(ref _colliderMesh);
            DisposeHelper.Dispose(ref _debugNode);
            DisposeHelper.Dispose(ref _debugText);

            DisposeHelper.Dispose(ref _carLightsAnimators);
            DisposeHelper.Dispose(ref _crewAnimator);
            DisposeHelper.Dispose(ref _doorLeftAnimator);
            DisposeHelper.Dispose(ref _doorRightAnimator);
            DisposeHelper.Dispose(ref _driverSteerAnimator);
            DisposeHelper.Dispose(ref _driverShiftAnimator);
            DisposeHelper.Dispose(ref _carShiftAnimator);
            DisposeHelper.Dispose(ref _wipersAnimator);

            _colliderLines.Dispose();
            _fuelTankLines.Dispose();
            _flamesLines.Dispose();
            _wheelsLines.Dispose();
            _wingsLines.Dispose();

            _sound?.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}