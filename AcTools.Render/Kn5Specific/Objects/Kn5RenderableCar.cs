// #define BB_PERF_PROFILE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.KnhFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Animations;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

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

        public static CarDescription FromDirectory(string carDirectory, DataWrapper data) {
            return new CarDescription(FileUtils.GetMainCarFilename(carDirectory, data), carDirectory, data);
        }

        public static CarDescription FromKn5(Kn5 kn5) {
            return new CarDescription(kn5.OriginalFilename) {
                Kn5Loaded = kn5
            };
        }

        public static CarDescription FromKn5(Kn5 kn5, string carDirectory, DataWrapper data = null) {
            return new CarDescription(kn5.OriginalFilename, carDirectory, data) {
                Kn5Loaded = kn5
            };
        }
    }

    public interface IExtraModelProvider {
        [ItemCanBeNull]
        Task<byte[]> GetModel([CanBeNull] string key);
    }

    public static class ExtraModels {
        public static readonly string KeyCrewExtra = "Crew.Extra";

        private static readonly List<IExtraModelProvider> Providers = new List<IExtraModelProvider>(1);

        public static void Register(IExtraModelProvider provider) {
            Providers.Add(provider);
        }

        [ItemCanBeNull]
        public static async Task<byte[]> GetAsync(string key) {
            foreach (var provider in Providers) {
                var data = await provider.GetModel(key).ConfigureAwait(false);
                if (data != null) return data;
            }

            return null;
        }
    }

    public partial class Kn5RenderableCar : Kn5RenderableFile, INotifyPropertyChanged, IMoveable {
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

            var mainKn5 = _carData.IsEmpty ? car.MainKn5File : _carData.GetMainKn5(_rootDirectory);
            _lodA = FileUtils.ArePathsEqual(car.MainKn5File, mainKn5) ? car.Kn5LoadedRequire : Kn5.FromFile(mainKn5);

            _lods = _carData.GetLods().ToList();
            _currentLod = _lods.FindIndex(x => string.Equals(x.FileName, Path.GetFileName(car.MainKn5File), StringComparison.OrdinalIgnoreCase));
            _currentLodObject = _mainLodObject = new LodObject(RootObject);
            _lodsObjects[_currentLod] = _currentLodObject;

            AdjustPosition();
            UpdatePreudoSteer();
            UpdateTogglesInformation();

            IsReflectable = false;
            SuspensionModifiers.PropertyChanged += OnSuspensionModifiersChanged;
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
            OnRootObjectChangedWheels();
            UpdateTogglesInformation();
            UpdateBoundingBox();
        }

        #region Driver
        private bool _driverSet;
        private Kn5RenderableDriver _driver;
        private Lazier<KsAnimAnimator> _driverSteerAnimator;
        private float _driverSteerLock;

        private void InitializeDriver() {
            if (_driverSet) return;
            _driverSet = true;

            var driver = _carData.GetDriverDescription();
            if (driver == null) return;

            var contentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(_rootDirectory));
            if (contentDirectory == null) return;

            var driversDirectory = Path.Combine(contentDirectory, "driver");
            var filename = Path.Combine(driversDirectory, driver.Name + ".kn5");
            if (!File.Exists(filename)) return;

            _driver = new Kn5RenderableDriver(Kn5.FromFile(filename), Matrix.Translation(driver.Offset),
                    _currentSkin == null ? null : Path.Combine(_skinsDirectory, _currentSkin),
                    AsyncTexturesLoading, _asyncOverrideTexturesLoading, AllowSkinnedObjects) {
                LiveReload = LiveReload,
                MagickOverride = MagickOverride
            };

            var knh = Path.Combine(_rootDirectory, "driver_base_pos.knh");
            if (File.Exists(knh)) {
                _driver.AlignNodes(Knh.FromFile(knh));
            }

            _driverSteerAnimator = Lazier.Create(() => CreateAnimator(_rootDirectory, driver.SteerAnimation, clampEnabled: false));
            _driverSteerLock = driver.SteerAnimationLock;

            _driver.LocalMatrix = RootObject.LocalMatrix;
            Add(_driver);
            ObjectsChanged?.Invoke(this, EventArgs.Empty);

            if (_steerDeg != 0 || OptionFixKnh) {
                UpdateDriverSteerAnimation(GetSteerOffset());
            }

            if (DebugMode) {
                _driver.DebugMode = true;
            }
        }

        private bool _useUp;

        public bool UseUp {
            get => _useUp;
            set {
                if (value == _useUp) return;
                _useUp = value;
                OnPropertyChanged();
            }
        }

        private Up _up;

        private void UpdateDriverSteerAnimation(float offset) {
            if (_driver == null) return;

            if (UseUp) {
                if (_up == null) {
                    _up = new Up(_driver, GetDummyByName("STEER_HR"));
                }

                _up.Update(offset, GetSteeringWheelParams(offset));
            } else {
                // one animation is 720°
                var steerLock = _steerLock ?? 360;
                var steer = offset * steerLock / _driverSteerLock;
                _driverSteerAnimator.Value?.SetImmediate(_driver.RootObject, 0.5f - steer / 2f);
            }
        }

        private void DrawDriver(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!_isDriverVisible) return;
            InitializeDriver();
            _driver?.Draw(contextHolder, camera, mode);
            _up?.Draw(contextHolder, camera, mode);
        }

        internal string DebugString => _up?.DebugString;

        private bool _isDriverVisible;

        public bool IsDriverVisible {
            get => _isDriverVisible;
            set {
                if (Equals(value, _isDriverVisible)) return;
                _isDriverVisible = value;
                OnPropertyChanged();

                if (_driver == null) return;
                if (!value) {
                    Remove(_driver);
                } else {
                    Add(_driver);
                }

                ObjectsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion

        #region Crew
        private bool _crewSet;

        [CanBeNull]
        private Kn5RenderableSkinnable _crewMain, _crewTyres, _crewStuff;
        private Lazier<KsAnimAnimator> _crewAnimator;

        private void InitializeCrewMain() {
            var contentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(_rootDirectory));
            if (contentDirectory == null) return;

            var driversDirectory = Path.Combine(contentDirectory, "objects3D");
            var filename = Path.Combine(driversDirectory, "pitcrew.kn5");
            if (!File.Exists(filename)) return;

            _crewMain = new Kn5RenderableSkinnable(Kn5.FromFile(filename), Matrix.RotationY(MathF.PI) * Matrix.Translation(-1.6f, 0f, 2f),
                    _currentSkin == null ? null : Path.Combine(_skinsDirectory, _currentSkin),
                    AsyncTexturesLoading, _asyncOverrideTexturesLoading, AllowSkinnedObjects) {
                LiveReload = LiveReload,
                MagickOverride = MagickOverride
            };

            _crewAnimator = Lazier.Create(() => CreateAnimator(Path.Combine(driversDirectory, "pitcrew_idle_dw.ksanim"), 10f));
            _crewAnimator.Value?.Loop(_crewMain.RootObject);

            Add(_crewMain);
        }

        private void InitializeCrewTyres() {
            var contentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(_rootDirectory));
            if (contentDirectory == null) return;

            var driversDirectory = Path.Combine(contentDirectory, "objects3D");
            var filename = Path.Combine(driversDirectory, "pitcrewtyre.kn5");
            if (!File.Exists(filename)) return;

            _crewTyres = new Kn5RenderableSkinnable(Kn5.FromFile(filename), Matrix.RotationY(-MathF.PI * 0.6f) * Matrix.Translation(1.9f, 0f, 0.8f),
                    _currentSkin == null ? null : Path.Combine(_skinsDirectory, _currentSkin),
                    AsyncTexturesLoading, _asyncOverrideTexturesLoading, AllowSkinnedObjects) {
                LiveReload = LiveReload,
                MagickOverride = MagickOverride
            };

            Add(_crewTyres);
        }

        private async Task InitializeCrewStuff() {
            var data = await ExtraModels.GetAsync(ExtraModels.KeyCrewExtra);
            if (data == null) return;

            _crewStuff = new Kn5RenderableSkinnable(Kn5.FromBytes(data), Matrix.RotationY(-MathF.PI * 0.5f) * Matrix.Translation(0.09f, 0f, 0.08f),
                    _currentSkin == null ? null : Path.Combine(_skinsDirectory, _currentSkin),
                    AsyncTexturesLoading, _asyncOverrideTexturesLoading, AllowSkinnedObjects) {
                LiveReload = LiveReload,
                MagickOverride = MagickOverride
            };

            if (!_isCrewVisible) return;
            Add(_crewStuff);
            ObjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InitializeCrew() {
            if (_crewSet) return;
            _crewSet = true;

            InitializeCrewMain();
            InitializeCrewTyres();
            InitializeCrewStuff().Forget();

            ObjectsChanged?.Invoke(this, EventArgs.Empty);
            UpdateCrewDebugMode();
        }

        private void UpdateCrewDebugMode() {
            if (_crewMain != null) {
                _crewMain.DebugMode = DebugMode;
            }

            if (_crewTyres != null) {
                _crewTyres.DebugMode = DebugMode;
            }

            if (_crewStuff != null) {
                _crewStuff.DebugMode = DebugMode;
            }
        }

        private void UpdateCrewParams() {
            if (_crewMain != null) {
                _crewMain.LiveReload = LiveReload;
                _crewMain.MagickOverride = MagickOverride;
            }

            if (_crewTyres != null) {
                _crewTyres.LiveReload = LiveReload;
                _crewTyres.MagickOverride = MagickOverride;
            }

            if (_crewStuff != null) {
                _crewStuff.LiveReload = LiveReload;
                _crewStuff.MagickOverride = MagickOverride;
            }
        }

        private void DrawCrew(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!_isCrewVisible) return;
            InitializeCrew();
            _crewMain?.Draw(contextHolder, camera, mode);
            _crewTyres?.Draw(contextHolder, camera, mode);
            _crewStuff?.Draw(contextHolder, camera, mode);
        }

        private bool _isCrewVisible;

        public bool IsCrewVisible {
            get => _isCrewVisible;
            set {
                if (Equals(value, _isCrewVisible)) return;
                _isCrewVisible = value;
                OnPropertyChanged();

                if (_crewMain == null) return;
                if (!value) {
                    Remove(_crewMain);
                    Remove(_crewTyres);
                    Remove(_crewStuff);
                } else {
                    if (_crewMain != null) Add(_crewMain);
                    if (_crewTyres != null) Add(_crewTyres);
                    if (_crewStuff != null) Add(_crewStuff);
                }

                ObjectsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion

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
        public class ExtraAnimationEntry : AnimationEntry {
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
            extra.Update(RootObject, actualValue ? 1f : 0f);
            _skinsWatcherHolder?.RaiseSceneUpdated();
        }

        private void ResetExtras() {
            if (_extras != null) {
                for (var i = 0; i < _extras.Length; i++) {
                    _extras[i].Update(RootObject, 0f);
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
                _extras[i].Update(RootObject, _extras[i].Value);
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
                    _wipersAnimator.Value?.SetTarget(RootObject, 0f);
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
                    _wipersAnimator.Value?.SetTarget(RootObject, 0f);
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
        private Lazier<KsAnimAnimator> _doorLeftAnimator;
        private Lazier<KsAnimAnimator> _doorRightAnimator;

        private void InitializeDoors() {
            if (_doorLeftAnimator != null) return;

            _doorLeftAnimator = Lazier.Create(() => CreateAnimator(_rootDirectory, _carData.GetLeftDoorAnimation()));
            _doorRightAnimator = Lazier.Create(() => CreateAnimator(_rootDirectory, _carData.GetRightDoorAnimation()));
        }

        private void ReenableDoors() {
            if (_doorLeftAnimator == null) return;
            if (_doorLeftAnimator.IsSet) _doorLeftAnimator.Value?.SetTarget(RootObject, _leftDoorOpen ? 1f : 0f);
            if (_doorRightAnimator.IsSet) _doorRightAnimator.Value?.SetTarget(RootObject, _rightDoorOpen ? 1f : 0f);
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
                _doorLeftAnimator.Value?.SetTarget(RootObject, value ? 1f : 0f);
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
                _doorRightAnimator.Value?.SetTarget(RootObject, value ? 1f : 0f);
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
        public abstract class AnimationEntry : INotifyPropertyChanged {
            private readonly Lazier<KsAnimAnimator> _animator;
            protected readonly Kn5RenderableCar CarNode;
            internal float Value;

            public AnimationEntry(Kn5RenderableCar carNode, string ksAnimName, float duration) {
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

            internal void Update(RenderableList parent, float value) {
                _animator.Value?.SetTarget(parent, value);
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

        public class SpoilerEntry : AnimationEntry {
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
                    parent.Update(RootObject, 1f);
                }
            } else {
                _wings.ElementAtOrDefault(wing.Description.Next ?? -1)?.Update(RootObject, 0f);
            }

            wing.Update(RootObject, actualValue ? 1f : 0f);
            _skinsWatcherHolder?.RaiseSceneUpdated();
        }

        private void ResetWings() {
            if (_wings == null) return;
            for (var i = 0; i < _wings.Length; i++) {
                _wings[i].Update(RootObject, 0f);
            }
        }

        private void ReenableWings() {
            if (_wings == null) return;
            for (var i = 0; i < _wings.Length; i++) {
                _wings[i].Update(RootObject, _wings[i].Value);
            }
        }
        #endregion

        #region Lights
        [CanBeNull]
        private CarLight[] _carLights;
        private KsAnimAnimator[] _carLightsAnimators;

        protected IEnumerable<T> LoadLights<T>() where T : CarLight, new() {
            return _carData.GetLights().Select(x => {
                var light = new T();
                light.Initialize(x, RootObject);
                return light;
            });
        }

        public IEnumerable<CarLight> GetCarLights() {
            var carLights = _carLights;
            if (carLights == null) {
                carLights = LoadLights().ToArray();
                _carLights = carLights;
            }

            return _carLights;
        }

        protected virtual IEnumerable<CarLight> LoadLights() {
            return LoadLights<CarLight>();
        }

        [ItemNotNull]
        private IEnumerable<KsAnimAnimator> LoadLightsAnimators() {
            return _carData.GetLightsAnimations()
                           .Select(x => CreateAnimator(_rootDirectory, x))
                           .NonNull();
        }

        private void ResetLights() {
            if (_carLights != null) {
                foreach (var carLight in _carLights) {
                    carLight.IsHeadlightEnabled = false;
                    carLight.IsBrakeEnabled = false;
                }
            }

            if (_carLightsAnimators != null) {
                for (var i = 0; i < _carLightsAnimators.Length; i++) {
                    _carLightsAnimators[i].SetTarget(RootObject, _headlightsEnabled ? 1f : 0f);
                    _carLightsAnimators[i].OnTick(float.PositiveInfinity);
                }
            }

            _carLightsAnimators = null;
            ReenableLights();
        }

        private void ReenableLights() {
            if (_carLights == null) return;

            _carLights = LoadLights().ToArray();

            for (var i = 0; i < _carLights.Length; i++) {
                _carLights[i].IsHeadlightEnabled = _headlightsEnabled;
                _carLights[i].IsBrakeEnabled = _brakeLightsEnabled;
            }

            if (_carLightsAnimators != null) {
                for (var i = 0; i < _carLightsAnimators.Length; i++) {
                    _carLightsAnimators[i].SetTarget(RootObject, _headlightsEnabled ? 1f : 0f);
                    _carLightsAnimators[i].OnTick(float.PositiveInfinity);
                }
            }
        }

        private bool _headlightsEnabled;

        public bool HeadlightsEnabled {
            get => _headlightsEnabled;
            set {
                if (Equals(value, _headlightsEnabled)) return;

                var carLights = _carLights;
                if (carLights == null) {
                    carLights = LoadLights().ToArray();
                    _carLights = carLights;
                }

                if (_carLightsAnimators == null) {
                    _carLightsAnimators = LoadLightsAnimators().ToArray();
                }

                _headlightsEnabled = value;

                for (var i = 0; i < carLights.Length; i++) {
                    carLights[i].IsHeadlightEnabled = value;
                }

                for (var i = 0; i < _carLightsAnimators.Length; i++) {
                    _carLightsAnimators[i].SetTarget(RootObject, value ? 1f : 0f);
                }

                _skinsWatcherHolder?.RaiseSceneUpdated();
                OnPropertyChanged();
            }
        }

        public TimeSpan? GetApproximateHeadlightsDelay() {
            var carLights = _carLights;
            if (carLights == null) {
                carLights = LoadLights().ToArray();
                _carLights = carLights;
            }

            var total = TimeSpan.Zero;
            var count = 0;

            for (var i = 0; i < carLights.Length; i++) {
                var l = carLights[i];
                if (l.Description?.HeadlightColor == null) continue;

                var v = l.GetDuration();
                if (v.HasValue) {
                    total += v.Value;
                    count++;
                }
            }

            if (count == 0) return null;
            return TimeSpan.FromSeconds(total.TotalSeconds / count);
        }

        public TimeSpan? GetApproximateBrakeLightsDelay() {
            var carLights = _carLights;
            if (carLights == null) {
                carLights = LoadLights().ToArray();
                _carLights = carLights;
            }

            var total = TimeSpan.Zero;
            var count = 0;

            for (var i = 0; i < carLights.Length; i++) {
                var l = carLights[i];
                if (l.Description?.BrakeColor == null) continue;

                var v = l.GetDuration();
                if (v.HasValue) {
                    total += v.Value;
                    count++;
                }
            }

            if (count == 0) return null;
            return TimeSpan.FromSeconds(total.TotalSeconds / count);
        }

        private bool _brakeLightsEnabled;

        public bool BrakeLightsEnabled {
            get => _brakeLightsEnabled;
            set {
                if (Equals(value, _brakeLightsEnabled)) return;

                if (_carLights == null) {
                    _carLights = LoadLights().ToArray();
                }

                _brakeLightsEnabled = value;

                for (var i = 0; i < _carLights.Length; i++) {
                    _carLights[i].IsBrakeEnabled = value;
                }

                _skinsWatcherHolder?.RaiseSceneUpdated();
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
            get => _ambientShadowSize;
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

        private AmbientShadow LoadWheelAmbientShadow(string nodeName, string textureName) {
            var node = GetDummyByName(nodeName);
            return node == null ? null : new AmbientShadow(textureName, GetWheelAmbientShadowMatrix(node));
        }

        public IReadOnlyList<IRenderableObject> GetAmbientShadows() {
            return _ambientShadows;
        }

        public ShaderResourceView GetAmbientShadowView(IDeviceContextHolder holder, AmbientShadow shadow) {
            if (_ambientShadowsTextures == null) {
                InitializeAmbientShadows(holder);
            }

            return shadow.GetView(_ambientShadowsHolder);
        }

        private AmbientShadow _wheelLfShadow, _wheelRfShadow, _wheelLrShadow, _wheelRrShadow;

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

        private void UpdateRearWheelsShadowsRotation() {
            if (_wheelLrShadow != null) {
                var node = GetDummyByName("WHEEL_LR");
                if (node != null) {
                    _wheelLrShadow.Transform = GetWheelAmbientShadowMatrix(node);
                }
            }

            if (_wheelRrShadow != null) {
                var node = GetDummyByName("WHEEL_RR");
                if (node != null) {
                    _wheelRrShadow.Transform = GetWheelAmbientShadowMatrix(node);
                }
            }
        }

        private IEnumerable<IRenderableObject> LoadAmbientShadows() {
            return _carData.IsEmpty ? new IRenderableObject[0] : new[] {
                LoadBodyAmbientShadow(),
                _wheelLfShadow = LoadWheelAmbientShadow("WHEEL_LF", "tyre_0_shadow.png"),
                _wheelRfShadow = LoadWheelAmbientShadow("WHEEL_RF", "tyre_1_shadow.png"),
                _wheelLrShadow = LoadWheelAmbientShadow("WHEEL_LR", "tyre_2_shadow.png"),
                _wheelRrShadow = LoadWheelAmbientShadow("WHEEL_RR", "tyre_3_shadow.png")
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

        public bool OverrideTexture(DeviceContextHolder device, string textureName, [CanBeNull] byte[] textureBytes) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return false;
            }

            var texture = _texturesProvider.GetTexture(device, textureName);
            texture.SetProceduralOverride(device, textureBytes);
            return texture.Exists ||
                    _crewMain?.OverrideTexture(device, textureName, textureBytes) == true ||
                    _crewTyres?.OverrideTexture(device, textureName, textureBytes) == true ||
                    _crewStuff?.OverrideTexture(device, textureName, textureBytes) == true ||
                    _driver?.OverrideTexture(device, textureName, textureBytes) == true;
        }

        public bool OverrideTexture(DeviceContextHolder device, string textureName, [CanBeNull] ShaderResourceView textureView, bool disposeLater) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return false;
            }

            var texture = _texturesProvider?.GetTexture(device, textureName);
            texture.SetProceduralOverride(device, textureView, disposeLater);
            return texture.Exists ||
                    _crewMain?.OverrideTexture(device, textureName, textureView, disposeLater) == true ||
                    _crewTyres?.OverrideTexture(device, textureName, textureView, disposeLater) == true ||
                    _crewStuff?.OverrideTexture(device, textureName, textureView, disposeLater) == true ||
                    _driver?.OverrideTexture(device, textureName, textureView, disposeLater) == true;
        }

        public void ClearProceduralOverrides() {
            foreach (var texture in _texturesProvider.GetExistingTextures()) {
                texture.SetProceduralOverride(null, null);
            }

            foreach (var extra in new [] {
                _crewMain, _crewTyres, _crewStuff, _driver
            }.NonNull()) {
                extra.ClearProceduralOverrides();
            }
        }
        #endregion

        #region Movement
        private MoveableHelper _movable;
        public MoveableHelper Movable => _movable ?? (_movable = new MoveableHelper(this, MoveableRotationAxis.Y));

        public void DrawMovementArrows(DeviceContextHolder holder, CameraBase camera) {
            Movable.ParentMatrix = Matrix;
            Movable.Draw(holder, camera, SpecialRenderMode.Simple);
        }

        private Matrix? _originalPosition;

        public void ResetPosition() {
            if (_originalPosition.HasValue) {
                LocalMatrix = _originalPosition.Value;
            }
        }

        void IMoveable.Move(Vector3 delta) {
            if (!_originalPosition.HasValue) {
                _originalPosition = LocalMatrix;
            }

            LocalMatrix = LocalMatrix * Matrix.Translation(delta);
            UpdateBoundingBox();
        }

        void IMoveable.Rotate(Quaternion delta) {
            if (!_originalPosition.HasValue) {
                _originalPosition = LocalMatrix;
            }

            LocalMatrix = Matrix.RotationQuaternion(delta) * LocalMatrix;
            UpdateBoundingBox();
        }
        #endregion

        #region Cameras
        public event EventHandler CamerasChanged;
        public event EventHandler ExtraCamerasChanged;

        [CanBeNull]
        public CameraBase GetDriverCamera() {
            return _carData.GetDriverCamera()?.ToCamera(Matrix);
        }

        [CanBeNull]
        public CameraBase GetDashboardCamera() {
            return _carData.GetDashboardCamera()?.ToCamera(Matrix);
        }

        [CanBeNull]
        public CameraBase GetBonnetCamera() {
            return _carData.GetBonnetCamera()?.ToCamera(Matrix);
        }

        [CanBeNull]
        public CameraBase GetBumperCamera() {
            return _carData.GetBumperCamera()?.ToCamera(Matrix);
        }

        public int GetCamerasCount() {
            return _carData.GetExtraCameras().Count();
        }

        [CanBeNull]
        public CameraBase GetCamera(int index) {
            return _carData.GetExtraCameras().Skip(index).Select(x => x.ToCamera(Matrix)).FirstOrDefault();
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

        IMoveable IMoveable.Clone() {
            return null;
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
            DisposeHelper.Dispose(ref _crewMain);
            DisposeHelper.Dispose(ref _crewTyres);
            DisposeHelper.Dispose(ref _colliderMesh);
            DisposeHelper.Dispose(ref _debugNode);
            DisposeHelper.Dispose(ref _debugText);

            _colliderLines.Dispose();
            _fuelTankLines.Dispose();
            _flamesLines.Dispose();
            _wheelsLines.Dispose();
            _wingsLines.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}