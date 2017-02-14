using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public class CarDescription {
        [NotNull]
        public string MainKn5File { get; }

        [CanBeNull]
        public string CarDirectory { get; }

        [NotNull]
        public string CarDirectoryRequire => CarDirectory ?? Path.GetDirectoryName(MainKn5File) ?? "";

        [CanBeNull]
        public DataWrapper Data { get; }

        [CanBeNull]
        internal Kn5 Kn5Loaded { get; private set; }

        [NotNull]
        public Kn5 Kn5LoadedRequire => Kn5Loaded ?? (Kn5Loaded = Kn5.FromFile(MainKn5File));

        public Task LoadAsync() {
            return Task.Run(() => {
                Kn5Loaded = Kn5.FromFile(MainKn5File);
            });
        }

        public CarDescription(string mainKn5File, string carDirectory = null, DataWrapper data = null) {
            MainKn5File = mainKn5File;
            CarDirectory = carDirectory;
            Data = data;
        }

        public static CarDescription FromDirectory(string carDirectory) {
            return new CarDescription(FileUtils.GetMainCarFilename(carDirectory), carDirectory);
        }

        public static CarDescription FromKn5(Kn5 kn5) {
            return new CarDescription(kn5.OriginalFilename) {
                Kn5Loaded = kn5
            };
        }
    }

    public class Kn5RenderableCar : Kn5RenderableFile, INotifyPropertyChanged {
        public const string DefaultSkin = "";
        public static bool OptionRepositionLod = false;

        private readonly string _rootDirectory, _skinsDirectory;
        private readonly bool _scanForSkins;
        
        private readonly CarData _carData;
        private Kn5OverrideableTexturesProvider _texturesProvider;

        [NotNull]
        private readonly Kn5 _lodA;
        private readonly RenderableList _ambientShadows;

        private DataWrapper _listeningData;

        public Kn5RenderableCar(CarDescription car, Matrix matrix, string selectSkin = DefaultSkin, bool scanForSkins = true,
                float shadowsHeight = 0.0f, bool asyncTexturesLoading = true, bool asyncOverrideTexturesLoading = false, bool allowSkinnedObjects = false)
                : base(car.Kn5LoadedRequire, matrix, asyncTexturesLoading, allowSkinnedObjects) {
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

            var mainKn5 = _carData.GetMainKn5(_rootDirectory);
            _lodA = FileUtils.ArePathsEqual(car.MainKn5File, mainKn5) ? car.Kn5LoadedRequire : Kn5.FromFile(mainKn5);
            
            _lods = _carData.GetLods().ToList();
            _currentLod = _lods.FindIndex(x => string.Equals(x.FileName, Path.GetFileName(car.MainKn5File), StringComparison.OrdinalIgnoreCase));
            _currentLodObject = _mainLodObject = new LodObject(RootObject);
            _lodsObjects[_currentLod] = _currentLodObject;

            AdjustPosition();
            UpdatePreudoSteer();
            UpdateTogglesInformation();

            IsReflectable = false;
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
            _ambientShadowsTextures = new DirectoryTexturesProvider(AsyncTexturesLoading, _asyncOverrideTexturesLoading);
            _ambientShadowsTextures.SetDirectory(contextHolder, _rootDirectory);
            _ambientShadowsMaterials = new SharedMaterials(contextHolder.Get<IMaterialsFactory>());
            _ambientShadowsHolder = new Kn5LocalDeviceContextHolder(contextHolder, _ambientShadowsMaterials, _ambientShadowsTextures, this);
        }

        public override void Draw(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            DrawInitialize(contextHolder);

            _currentLodObject.EnsurePrepared(LocalHolder, contextHolder, SharedMaterials, TexturesProvider, this);
            RootObject.Draw(_currentLodObject.Holder, camera, mode, filter);

            if (Skins != null && !_skinsWatcherSet) {
                SkinsWatcherSet(contextHolder);
            }

            if (!_actuallyLoaded) {
                SelectSkin(contextHolder, CurrentSkin);
            }

            if (_ambientShadowsTextures == null) {
                InitializeAmbientShadows(contextHolder);
            }
            
            _ambientShadows.Draw(_ambientShadowsHolder, camera, mode, filter);
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

        #region LODs
        private readonly IReadOnlyList<CarData.LodDescription> _lods;

        public int LodsCount => _lods.Count;

        public CarData.LodDescription CurrentLodInformation => _lods[_currentLod];

        private int _currentLod;

        public int CurrentLod {
            get { return _currentLod; }
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
                
                ReenableLights();
                ReupdatePreudoSteer();
                UpdateTogglesInformation();
                UpdateBoundingBox();
                _currentLodObject.DebugMode = debugMode;

                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentLodInformation));

                InvalidateCount();
            }
        }

        private class LodObject : IDisposable {
            public readonly Kn5RenderableList Renderable;
            public Kn5SharedMaterials Materials;
            internal IDeviceContextHolder Holder;
            internal Dictionary<string, Matrix> OriginalMatrices;
            private readonly Kn5 _nonDefaultKn5;
            private bool _prepared;

            public LodObject(Kn5RenderableList rootObject) {
                _nonDefaultKn5 = null;
                Renderable = rootObject;
            }

            public LodObject(Kn5 kn5, bool allowSkinnedObjects) {
                _nonDefaultKn5 = kn5;
                Renderable = (Kn5RenderableList)Convert(kn5.RootNode, allowSkinnedObjects);
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
                    if (_nonDefaultKn5 == null) {
                        Materials = materials;
                        Holder = localHolder;
                    } else {
                        Materials = new Kn5SharedMaterials(globalHolder, _nonDefaultKn5);
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
                get { return _debugMode; }
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
            get { return _liveReload; }
            set {
                if (Equals(value, _liveReload)) return;
                _liveReload = value;
                if (_texturesProvider != null) {
                    _texturesProvider.LiveReload = value;
                }
                OnPropertyChanged();
            }
        }

        private bool _magickOverride;

        public bool MagickOverride {
            get { return _magickOverride; }
            set {
                if (Equals(value, _magickOverride)) return;
                _magickOverride = value;
                if (_texturesProvider != null) {
                    _texturesProvider.MagickOverride = value;
                }
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        public List<string> Skins { get; private set; }

        [CanBeNull]
        public string CurrentSkin {
            get { return _currentSkin; }
            private set {
                if (value == _currentSkin) return;
                _currentSkin = value;
                OnPropertyChanged();
            }
        }

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

            var index = Skins.IndexOf(CurrentSkin);
            SelectSkin(contextHolder, index < 0 || index >= Skins.Count - 1 ? Skins[0] : Skins[index + 1]);
        }

        public void SelectPreviousSkin(IDeviceContextHolder contextHolder) {
            if (Skins?.Any() != true) return;

            var index = Skins.IndexOf(CurrentSkin);
            SelectSkin(contextHolder, index <= 0 ? Skins[Skins.Count - 1] : Skins[index - 1]);
        }

        public void SelectSkin(IDeviceContextHolder contextHolder, [CanBeNull] string skinId) {
            if (skinId == DefaultSkin) {
                skinId = Skins?.FirstOrDefault();
            }

            var skinIdLower = skinId?.ToLower();
            if (Equals(CurrentSkin, skinIdLower) && _actuallyLoaded) return;

            CurrentSkin = skinIdLower;

            if (_texturesProvider != null) {
                if (skinId == null) {
                    _texturesProvider.ClearOverridesDirectory();
                } else {
                    _texturesProvider.SetOverridesDirectory(contextHolder, Path.Combine(_skinsDirectory, skinId));
                }
                contextHolder.RaiseUpdateRequired();
                _actuallyLoaded = true;
            } else {
                _actuallyLoaded = false;
            }
        }
        #endregion

        #region Rims (blurred/static), cockpit (HR/LR), seatbelt (on/off)
        public IList<CarData.BlurredObject> BlurredObjects => _blurredObjs ?? (_blurredObjs = _carData.GetBlurredObjects().ToList());
        private IList<CarData.BlurredObject> _blurredObjs;

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
                        if (BlurredObjects.Any(x => x.BlurredName == dummy.OriginalNode.Name)) {
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

        private bool _cockpitLrActive;

        public bool CockpitLrActive {
            get { return _cockpitLrActive; }
            set {
                if (Equals(value, _cockpitLrActive)) return;
                _cockpitLrActive = value;

                foreach (var child in _currentLodObject.Renderable.GetAllChildren().OfType<Kn5RenderableList>()) {
                    switch (child.OriginalNode.Name) {
                        case "COCKPIT_LR":
                        case "STEER_LR":
                        case "SHIFT_LD":
                            child.IsEnabled = value;
                            break;
                        case "COCKPIT_HR":
                        case "STEER_HR":
                        case "SHIFT_HD":
                            child.IsEnabled = !value;
                            break;
                    }
                }

                _currentLodObject.Renderable.UpdateBoundingBox();
                InvalidateCount();
                OnPropertyChanged();
            }
        }

        public bool HasSeatbeltOn { get; private set; }

        public bool HasSeatbeltOff { get; private set; }

        private bool _seatbeltOnActive;

        public bool SeatbeltOnActive {
            get { return _seatbeltOnActive; }
            set {
                if (Equals(value, _seatbeltOnActive)) return;
                _seatbeltOnActive = value;

                var onNode = _currentLodObject.Renderable.GetDummyByName("CINTURE_ON");
                if (onNode != null) {
                    onNode.IsEnabled = value;
                }

                var offNode = _currentLodObject.Renderable.GetDummyByName("CINTURE_OFF");
                if (offNode != null) {
                    offNode.IsEnabled = !value;
                }

                _currentLodObject.Renderable.UpdateBoundingBox();
                InvalidateCount();
                OnPropertyChanged();
            }
        }

        public bool HasBlurredNodes { get; private set; }

        private bool _blurredNodesActive;

        public bool BlurredNodesActive {
            get { return _blurredNodesActive; }
            set {
                if (Equals(value, _blurredNodesActive)) return;
                _blurredNodesActive = value;

                foreach (var blurredObject in BlurredObjects) {
                    var staticNode = _currentLodObject.Renderable.GetDummyByName(blurredObject.StaticName);
                    if (staticNode != null) {
                        staticNode.IsEnabled = !value;
                    }

                    var blurredNode = _currentLodObject.Renderable.GetDummyByName(blurredObject.BlurredName);
                    if (blurredNode != null) {
                        blurredNode.IsEnabled = value;
                    }
                }

                _currentLodObject.Renderable.UpdateBoundingBox();
                InvalidateCount();
                OnPropertyChanged();
            }
        }
        #endregion
        
        #region Adjust position
        private void AdjustPosition() {
            var node = RootObject;
            node.UpdateBoundingBox();

            var wheelLf = GetDummyByName("WHEEL_LF");
            var wheelRf = GetDummyByName("WHEEL_RF");
            var wheelLr = GetDummyByName("WHEEL_LR");
            var wheelRr = GetDummyByName("WHEEL_RR");
            if (wheelLf == null || wheelRf == null || wheelLr == null || wheelRr == null) return;

            if (!wheelLf.BoundingBox.HasValue ||
                    !wheelRf.BoundingBox.HasValue ||
                    !wheelLr.BoundingBox.HasValue ||
                    !wheelRr.BoundingBox.HasValue) {
                node.LocalMatrix = Matrix.Translation(0, -node.BoundingBox?.Minimum.Y ?? 0f, 0) * node.LocalMatrix;
                return;
            }

            var y1 = Math.Min((float)wheelLf.BoundingBox?.Minimum.Y,
                    (float)wheelRf.BoundingBox?.Minimum.Y);
            var y2 = Math.Min((float)wheelLr.BoundingBox?.Minimum.Y,
                    (float)wheelRr.BoundingBox?.Minimum.Y);

            if (float.IsPositiveInfinity(y1) || float.IsPositiveInfinity(y2)) {
                node.LocalMatrix = Matrix.Translation(0, -node.BoundingBox?.Minimum.Y ?? 0f, 0) * node.LocalMatrix;
            } else {
                var x1 = node.GetDummyByName("WHEEL_LF")?.BoundingBox?.GetCenter().Z ?? 0f;
                var x2 = -node.GetDummyByName("WHEEL_LR")?.BoundingBox?.GetCenter().Z ?? 0f;
                var y = y2 + x2 * (y1 - y2) / (x1 + x2);
                node.LocalMatrix = Matrix.RotationX((float)Math.Atan2(y1 - y2, x1 + x2)) * Matrix.Translation(0, -y, 0) * node.LocalMatrix;
            }
        }
        #endregion

        #region Mirrors
        private List<IKn5RenderableObject> _mirrors;

        private void LoadMirrors(IDeviceContextHolder holder) {
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

        #region  Lights
        [CanBeNull]
        private IReadOnlyList<CarLight> _carLights;

        protected IEnumerable<T> LoadLights<T>() where T : CarLight, new() {
            return _carData.GetLights().Select(x => {
                var light = new T();
                light.Initialize(x, RootObject);
                return light;
            });
        }

        protected virtual IEnumerable<CarLight> LoadLights() {
            return LoadLights<CarLight>();
        }

        private void ResetLights() {
            if (_carLights != null) {
                foreach (var carLight in _carLights) {
                    carLight.IsHeadlightEnabled = false;
                    carLight.IsBrakeEnabled = false;
                }
            }
            ReenableLights();
        }

        private void ReenableLights() {
            _carLights = null;
            if (_lightsEnabled) {
                _lightsEnabled = false;
                LightsEnabled = true;
            }
            if (_brakeLightsEnabled) {
                _brakeLightsEnabled = false;
                BrakeLightsEnabled = true;
            }
        }

        private bool _lightsEnabled;

        public bool LightsEnabled {
            get { return _lightsEnabled; }
            set {
                if (_carLights == null) {
                    _carLights = LoadLights().ToIReadOnlyListIfItIsNot();
                }

                if (Equals(value, _lightsEnabled)) return;
                _lightsEnabled = value;

                foreach (var light in _carLights) {
                    light.IsHeadlightEnabled = value;
                }

                OnPropertyChanged();
            }
        }

        private bool _brakeLightsEnabled;

        public bool BrakeLightsEnabled {
            get { return _brakeLightsEnabled; }
            set {
                if (_carLights == null) {
                    _carLights = LoadLights().ToIReadOnlyListIfItIsNot();
                }

                if (Equals(value, _brakeLightsEnabled)) return;
                _brakeLightsEnabled = value;

                foreach (var light in _carLights) {
                    light.IsBrakeEnabled = value;
                }

                OnPropertyChanged();
            }
        }
        #endregion

        #region Ambient shadows
        public AmbientShadow AmbientShadowNode;

        private readonly float _shadowsHeight;
        private readonly bool _asyncOverrideTexturesLoading;
        private Vector3 _ambientShadowSize;
        private string _currentSkin;

        private IRenderableObject LoadBodyAmbientShadow() {
            AmbientShadowNode = new AmbientShadow("body_shadow.png", Matrix.Identity);
            ResetAmbientShadowSize();
            return AmbientShadowNode;
        }

        public Vector3 AmbientShadowSize {
            get { return _ambientShadowSize; }
            set {
                if (Equals(value, _ambientShadowSize)) return;
                _ambientShadowSize = value;

                if (AmbientShadowNode != null) {
                    AmbientShadowNode.Transform = Matrix.Scaling(AmbientShadowSize) * Matrix.RotationY(MathF.PI) *
                            Matrix.Translation(0f, _shadowsHeight, 0f);
                }
            }
        }

        public void FitAmbientShadowSize() {
            if (!RootObject.BoundingBox.HasValue) return;
            var size = RootObject.BoundingBox.Value;
            AmbientShadowSize = new Vector3(Math.Max(-size.Minimum.X, size.Maximum.X) * 1.1f, 1.0f, Math.Max(-size.Minimum.Z, size.Maximum.Z) * 1.1f);
        }

        public void ResetAmbientShadowSize() {
            AmbientShadowSize = _carData.GetBodyShadowSize();
        }

        public Vector3 GetWheelShadowSize() {
            return _carData.GetWheelShadowSize();
        }

        private Matrix GetWheelAmbientShadowMatrix([NotNull] RenderableList wheel) {
            var m = wheel.Matrix.GetTranslationVector() - LocalMatrix.GetTranslationVector();
            m.Y = _shadowsHeight;
            return Matrix.Scaling(GetWheelShadowSize()) * Matrix.RotationY(MathF.PI + _steerDeg * MathF.PI / 180f) * Matrix.Translation(m);
        }

        private AmbientShadow LoadWheelAmbientShadow(string nodeName, string textureName) {
            var node = GetDummyByName(nodeName);
            return node == null ? null : new AmbientShadow(textureName, GetWheelAmbientShadowMatrix(node));
        }

        private AmbientShadow _wheelLfShadow, _wheelRfShadow;

        private void UpdateFrontWheelsShadowsRotation() {
            if (_wheelLfShadow != null) {
                var node = GetDummyByName("WHEEL_LF");
                if (node != null) {
                    _wheelLfShadow.Transform = GetWheelAmbientShadowMatrix(node);
                }
            }

            if (_wheelRfShadow != null) {
                var node = GetDummyByName("WHEEL_RF");
                if (node != null) {
                    _wheelRfShadow.Transform = GetWheelAmbientShadowMatrix(node);
                }
            }
        }

        private IEnumerable<IRenderableObject> LoadAmbientShadows() {
            return _carData.IsEmpty ? new IRenderableObject[0] : new[] {
                LoadBodyAmbientShadow(),
                _wheelLfShadow = LoadWheelAmbientShadow("WHEEL_LF", "tyre_0_shadow.png"),
                _wheelRfShadow = LoadWheelAmbientShadow("WHEEL_RF", "tyre_1_shadow.png"),
                LoadWheelAmbientShadow("WHEEL_LR", "tyre_2_shadow.png"),
                LoadWheelAmbientShadow("WHEEL_RR", "tyre_3_shadow.png")
            }.Where(x => x != null);
        }
        #endregion

        #region Measurements
        public float GetWheelbase() {
            var frontZ = RootObject.GetDummyByName("WHEEL_LF")?.Matrix.GetTranslationVector().Z ?? 0f;
            var rearZ = RootObject.GetDummyByName("WHEEL_LR")?.Matrix.GetTranslationVector().Z ?? 0f;
            return Math.Abs(frontZ - rearZ);
        }
        #endregion

        #region Override textures
        public bool OverrideTexture(DeviceContextHolder device, string textureName, byte[] textureBytes) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
            }

            var texture = _texturesProvider?.GetTexture(device, textureName);
            texture?.SetProceduralOverride(device.Device, textureBytes);
            return texture != null;
        }

        public void ClearProceduralOverrides() {
            foreach (var texture in _texturesProvider.GetExistingTextures()) {
                texture.SetProceduralOverride(null, null);
            }
        }
        #endregion

        #region Live reload
        private void OnDataChanged(object sender, DataChangedEventArgs e) {
            var holder = _skinsWatcherHolder;
            if (holder == null) return;

            switch (e.PropertyName) {
                case null:
                    ReloadSteeringWheelLock();
                    LoadMirrors(holder);
                    ResetLights();
                    ResetAmbientShadowSize();
                    ReloadSuspension();
                    break;
                case "car.ini":
                    ReloadSteeringWheelLock();
                    break;
                case "mirrors.ini":
                    LoadMirrors(holder);
                    break;
                case "lights.ini":
                    ResetLights();
                    break;
                case "ambient_shadows.ini":
                    ResetAmbientShadowSize();
                    break;
                case "suspensions.ini":
                    ReloadSuspension();
                    break;
            }

            holder.RaiseUpdateRequired();
        }

        private void ReloadSuspension() {
            _suspensionsPack = null;
            DisposeHelper.Dispose(ref _suspensionLines);
            ReupdatePreudoSteer();
        }
        #endregion

        #region Pseudo-movements
        private float _steerDeg;

        public float SteerDeg {
            get { return _steerDeg; }
            set {
                value = value.Clamp(-50f, 50f).Round(0.1f);
                if (Equals(value, _steerDeg)) return;
                _steerDeg = value;
                OnPropertyChanged();
                UpdatePreudoSteer();
            }
        }
        
        private Vector3 _wheelLfCon;
        private float _steerDegPrevious;

        private Matrix? GetSteerWheelMatrix(string name, [NotNull] CarData.SuspensionsPack pack, [CanBeNull] CarData.SuspensionBase suspension, float angle) {
            var axis = suspension?.WheelSteerAxis;
            if (axis == null) return null;

            if (_currentLodObject.OriginalMatrices == null) {
                _currentLodObject.OriginalMatrices = new Dictionary<string, Matrix>(3);
                UpdateModelMatrixInverted();
            }

            axis = Tuple.Create(
                    pack.TranslateRelativeToCarModel(suspension, axis.Item1),
                    pack.TranslateRelativeToCarModel(suspension, axis.Item2));
            var rotationAxis = Vector3.Normalize(axis.Item2 - axis.Item1);

            var node = GetDummyByName(name);
            if (node == null) return null;

            Matrix original;
            if (!_currentLodObject.OriginalMatrices.TryGetValue(name, out original)) {
                original = _currentLodObject.OriginalMatrices[name] = node.RelativeToModel;
            }

            Vector3 position, scale;
            Quaternion rotation;
            original.Decompose(out scale, out rotation, out position);
            
            var p = new Plane(position, rotationAxis);
            Vector3 con;
            if (!Plane.Intersects(p, axis.Item1 - rotationAxis * 10f, axis.Item2 + rotationAxis * 10f, out con)) {
                AcToolsLogging.Write("10f is not enough!?");
                return null;
            }

            _wheelLfCon = con;
            var delta = con - position;

            var transform = Matrix.Translation(-delta) * Matrix.RotationAxis(rotationAxis, angle * MathF.PI / 180f) * Matrix.Translation(delta);
            //var camber = Matrix.RotationZ((left ? -1000f : 1000f) * suspension.StaticCamber * MathF.PI / 180f);
            // node.LocalMatrix = Matrix.RotationAxis(rotationAxis, _steerDeg * MathF.PI / 180f) * Matrix.Translation(Vector3.Transform(suspension.RefPoint, pack.GraphicOffset).GetXyz());
            // node.LocalMatrix = transform * Matrix.Translation(Vector3.Transform(suspension.RefPoint, pack.GraphicOffset).GetXyz());
            // node.LocalMatrix = transform * Matrix.Translation(position);

            return Matrix.RotationQuaternion(rotation) * transform * Matrix.Translation(position);
        }

        private void SteerWheel(bool left, [NotNull] CarData.SuspensionsPack pack, [CanBeNull] CarData.SuspensionBase suspension, float angle) {
            var namePostfix = left ? "LF" : "RF";

            var range = (angle.Abs() / 30f).Saturate();
            angle += (left ? -1.5f : 1.5f) * MathF.Pow(range, 2f);

            var wheelMatrix = GetSteerWheelMatrix($@"WHEEL_{namePostfix}", pack, suspension, angle);
            if (!wheelMatrix.HasValue) return;

            foreach (var node in new[] { "HUB" }.Select(x => GetDummyByName($@"{x}_{namePostfix}")).NonNull()) {
                node.LocalMatrix = (GetSteerWheelMatrix(node.Name, pack, suspension, angle) ?? wheelMatrix.Value) *
                        Matrix.Invert(node.ParentMatrix * node.ModelMatrixInverted);
            }

            foreach (var node in new[] { "WHEEL", "SUSP", "DISC" }.Select(x => GetDummyByName($@"{x}_{namePostfix}")).NonNull()) {
                node.LocalMatrix = wheelMatrix.Value * Matrix.Invert(node.ParentMatrix * node.ModelMatrixInverted);
            }
        }

        private float? _steerLock;

        private void SteerSteeringWheel(float offset) {
            if (_currentLodObject.OriginalMatrices == null) {
                _currentLodObject.OriginalMatrices = new Dictionary<string, Matrix>(3);
                UpdateModelMatrixInverted();
            }

            foreach (var node in new[] { "HR", "LR" }.Select(x => GetDummyByName($@"STEER_{x}")).NonNull()) {
                var name = node.Name ?? "";

                Matrix original;
                if (!_currentLodObject.OriginalMatrices.TryGetValue(name, out original)) {
                    original = _currentLodObject.OriginalMatrices[name] = node.LocalMatrix;
                }

                if (!_steerLock.HasValue) {
                    _steerLock = _carData.GetSteerLock();
                }

                node.LocalMatrix = Matrix.RotationZ(_steerLock.Value * offset * MathF.PI / 180f) * original;
            }
        }

        private void ReloadSteeringWheelLock() {
            _steerLock = null;
            SteerSteeringWheel((SteerDeg / 50f).Clamp(-1f, 1f));
        }

        private void ReupdatePreudoSteer() {
            _steerDegPrevious = float.NaN;
            UpdatePreudoSteer();
        }

        private void UpdatePreudoSteer() {
            var pack = SuspensionsPack;
            var front = pack.Front as CarData.IndependentSuspensionsGroup;
            if (front == null) return;

            var angle = SteerDeg;
            if (Equals(_steerDegPrevious, angle)) return;
            _steerDegPrevious = angle;

            SteerWheel(true, pack, front.Left, angle);
            SteerWheel(false, pack, front.Right, angle);
            SteerSteeringWheel((angle / 50f).Clamp(-1f, 1f));

            UpdateFrontWheelsShadowsRotation();
            _skinsWatcherHolder?.RaiseSceneUpdated();
        }
        #endregion

        #region Suspension debug
        public CarData.SuspensionsPack SuspensionsPack => _suspensionsPack ?? (_suspensionsPack = _carData.GetSuspensionsPack());
        private CarData.SuspensionsPack _suspensionsPack;

        private IRenderableObject _suspensionLines;
        private DebugObject _debugNode;

        private static int CountDebugSuspensionPoints(CarData.SuspensionsGroupBase group, 
                out CarData.IndependentSuspensionsGroup independent, out CarData.DependentSuspensionGroup dependent) {
            independent = group as CarData.IndependentSuspensionsGroup;
            if (independent != null) {
                dependent = null;
                return independent.Left.DebugLines.Length + independent.Right.DebugLines.Length;
            }

            dependent = group as CarData.DependentSuspensionGroup;
            return dependent?.Both.DebugLines.Length ?? 0;
        }

        private static void AddDebugSuspensionPoints(CarData.SuspensionsPack pack, CarData.SuspensionBase suspension, InputLayouts.VerticePC[] result,
                ref int index) {
            for (var i = 0; i < suspension.DebugLines.Length; i++) {
                var line = suspension.DebugLines[i];
                result[index++] = new InputLayouts.VerticePC(pack.TranslateRelativeToCarModel(suspension, line.Start), line.Color.ToVector4());
                result[index++] = new InputLayouts.VerticePC(pack.TranslateRelativeToCarModel(suspension, line.End), line.Color.ToVector4());
            }
        }

        private static void AddDebugSuspensionPoints(CarData.SuspensionsPack pack, InputLayouts.VerticePC[] result,
                CarData.IndependentSuspensionsGroup independent, CarData.DependentSuspensionGroup dependent, ref int index) {
            if (independent != null) {
                AddDebugSuspensionPoints(pack, independent.Left, result, ref index);
                AddDebugSuspensionPoints(pack, independent.Right, result, ref index);
            } else if (dependent != null) {
                AddDebugSuspensionPoints(pack, dependent.Both, result, ref index);
            }
        }

        private static InputLayouts.VerticePC[] GetDebugSuspensionVertices(CarData.SuspensionsPack pack) {
            CarData.IndependentSuspensionsGroup ifg, irg;
            CarData.DependentSuspensionGroup dfg, drg;

            var index = 0;
            var result = new InputLayouts.VerticePC[(CountDebugSuspensionPoints(pack.Front, out ifg, out dfg) +
                    CountDebugSuspensionPoints(pack.Rear, out irg, out drg)) * 2];
            AddDebugSuspensionPoints(pack, result, ifg, dfg, ref index);
            AddDebugSuspensionPoints(pack, result, irg, drg, ref index);
            return result;
        }

        public void DrawSuspensionDebugStuff(DeviceContextHolder holder, ICamera camera) {
            if (_suspensionLines == null) {
                _suspensionLines = new DebugLinesObject(Matrix.Identity, GetDebugSuspensionVertices(SuspensionsPack));
            }

            _suspensionLines.ParentMatrix = RootObject.Matrix;
            _suspensionLines.Draw(holder, camera, SpecialRenderMode.Simple);

            if (_debugNode == null) {
                _debugNode = new DebugObject(Matrix.Translation(_wheelLfCon), GeometryGenerator.CreateSphere(0.02f, 6, 6));
            }

            _debugNode.Transform = Matrix.Translation(_wheelLfCon);
            _debugNode.ParentMatrix = RootObject.Matrix;

            holder.DeviceContext.OutputMerger.DepthStencilState = holder.States.DisabledDepthState;
            _debugNode.Draw(holder, camera, SpecialRenderMode.Simple);
        }

        public void SetDebugMode(bool enabled) {
            _currentLodObject.DebugMode = enabled;
        }
        #endregion

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
            DisposeHelper.Dispose(ref _debugNode);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region IKn5Model
        public override IKn5RenderableObject GetNodeByName(string name) {
            return RootObject.GetByName(name);
        }
        #endregion
    }
}