using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public class Kn5RenderableCar : Kn5RenderableFile, INotifyPropertyChanged {
        public const string DefaultSkin = "";
        public static bool OptionRepositionLod = false;

        private readonly string _rootDirectory, _skinsDirectory;
        private readonly bool _scanForSkins;

        public readonly DataWrapper Data;
        private Kn5OverrideableTexturesProvider _texturesProvider;

        private readonly Kn5 _lodA;
        private readonly RenderableList _ambientShadows;

        public Kn5RenderableCar(Kn5 kn5, string rootDirectory, Matrix matrix, string selectSkin = DefaultSkin, bool scanForSkins = true, float shadowsHeight = 0.0f)
                : base(kn5, matrix) { 
            _rootDirectory = rootDirectory ?? Path.GetDirectoryName(kn5.OriginalFilename);

            _skinsDirectory = FileUtils.GetCarSkinsDirectory(_rootDirectory);
            _scanForSkins = scanForSkins;
            _shadowsHeight = shadowsHeight;

            Data = DataWrapper.FromDirectory(_rootDirectory);

            _blurredObjects = Data.GetIniFile("blurred_objects.ini").GetSections("OBJECT").GroupBy(x => x.GetInt("WHEEL_INDEX", -1))
                                  .Select(x => new BlurredObject {
                                      WheelIndex = x.First().GetInt("WHEEL_INDEX", -1),
                                      StaticName = x.FirstOrDefault(y => y.GetInt("MIN_SPEED", 0) == 0)?.GetNonEmpty("NAME"),
                                      BlurredName = x.FirstOrDefault(y => y.GetInt("MIN_SPEED", 0) > 0)?.GetNonEmpty("NAME")
                                  }).Where(x => x.WheelIndex >= 0 && x.StaticName != null && x.BlurredName != null).ToList();

            _ambientShadows = new RenderableList("_shadows", Matrix.Identity, LoadAmbientShadows());
            Add(_ambientShadows);

            if (_scanForSkins) {
                ReloadSkins(null, selectSkin);
            }

            var mainKn5 = FileUtils.GetMainCarFilename(_rootDirectory, Data);
            _lodA = FileUtils.ArePathsEqual(kn5.OriginalFilename, mainKn5) ? kn5 : Kn5.FromFile(mainKn5);

            var lodsIni = Data.GetIniFile("lods.ini");
            _lods = lodsIni.GetSections("LOD").Select(x => new LodDescription(x)).Where(x => x.FileName != null).ToList();
            _currentLod = _lods.FindIndex(x => string.Equals(x.FileName, Path.GetFileName(kn5.OriginalFilename), StringComparison.OrdinalIgnoreCase));
            _currentLodObject = _mainLodObject = new LodObject(RootObject);
            _lodsObjects[_currentLod] = _currentLodObject;

            AdjustPosition();
            UpdateTogglesInformation();

            IsReflectable = false;
        }

        protected override ITexturesProvider InitializeTextures(IDeviceContextHolder contextHolder) {
            _texturesProvider = new Kn5OverrideableTexturesProvider(_lodA) { LiveReload = LiveReload };
            if (CurrentSkin != null) {
                _texturesProvider.SetOverridesDirectory(contextHolder, Path.Combine(_skinsDirectory, CurrentSkin));
            }
            return _texturesProvider;
        }

        private SharedMaterials _ambientShadowsMaterials;
        private DirectoryTexturesProvider _ambientShadowsTextures;
        private IDeviceContextHolder _ambientShadowsHolder;

        private void InitializeAmbientShadows(IDeviceContextHolder contextHolder) {
            _ambientShadowsTextures = new DirectoryTexturesProvider();
            _ambientShadowsTextures.SetDirectory(contextHolder, _rootDirectory);
            _ambientShadowsMaterials = new SharedMaterials(contextHolder.Get<IMaterialsFactory>());
            _ambientShadowsHolder = new Kn5LocalDeviceContextHolder(contextHolder, _ambientShadowsMaterials, _ambientShadowsTextures);
        }

        public override void Draw(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            DrawInitialize(contextHolder);

            if (!_currentLodObject.Prepared) {
                _currentLodObject.Prepared = true;

                if (_currentLodObject.Materials == null) {
                    if (_currentLodObject.NonDefaultKn5 == null) {
                        _currentLodObject.Materials = SharedMaterials;
                        _currentLodObject.Holder = LocalHolder;
                    } else {
                        _currentLodObject.Materials = new Kn5SharedMaterials(contextHolder, _currentLodObject.NonDefaultKn5);
                        _currentLodObject.Holder = new Kn5LocalDeviceContextHolder(contextHolder, _currentLodObject.Materials, TexturesProvider);
                    }
                }

                LoadMirrors(contextHolder);
            }

            _currentLodObject.Renderable.Draw(_currentLodObject.Holder, camera, mode, filter);

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

        public int TrianglesCount => _trianglesCount ?? (_trianglesCount = _currentLodObject.Renderable.GetTrianglesCount()).Value;

        public int ObjectsCount => _objectsCount ?? (_objectsCount = _currentLodObject.Renderable.GetObjectsCount()).Value;

        private void InvalidateCount() {
            _trianglesCount = null;
            _objectsCount = null;
            OnPropertyChanged(nameof(TrianglesCount));
            OnPropertyChanged(nameof(ObjectsCount));
        }

        #region LODs
        public class LodDescription {
            public string FileName { get; }

            public float In { get; }

            public float Out { get; }

            internal LodDescription(IniFileSection fileSection) {
                FileName = fileSection.GetNonEmpty("FILE");
                In = (float)fileSection.GetDouble("IN", 0d);
                Out = (float)fileSection.GetDouble("OUT", 0d);
            }
        }

        private readonly IReadOnlyList<LodDescription> _lods;

        public int LodsCount => _lods.Count;

        public LodDescription CurrentLodInformation => _lods[_currentLod];

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

                Remove(_currentLodObject.Renderable);
                if (!_lodsObjects.TryGetValue(value, out _currentLodObject)) {
                    var path = Path.GetFullPath(Path.Combine(_rootDirectory, lod.FileName));
                    var kn5 = value == 0 ? _lodA : Kn5.FromFile(path);
                    _currentLodObject = new LodObject(kn5);
                    _lodsObjects[value] = _currentLodObject;
                    Insert(0, _currentLodObject.Renderable);
                    if (OptionRepositionLod) {
                        AdjustPosition();
                    } else {
                        _currentLodObject.Renderable.LocalMatrix = _mainLodObject.Renderable.LocalMatrix;
                    }
                } else {
                    Insert(0, _currentLodObject.Renderable);
                }

                ReenableLights();
                UpdateTogglesInformation();
                UpdateBoundingBox();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentLodInformation));

                InvalidateCount();
            }
        }

        private class LodObject : IDisposable {
            public readonly Kn5 NonDefaultKn5;
            public readonly RenderableList Renderable;
            public Kn5SharedMaterials Materials;
            internal IDeviceContextHolder Holder;
            public bool Prepared;

            public LodObject(Kn5RenderableList rootObject) {
                NonDefaultKn5 = null;
                Renderable = rootObject;
            }

            public LodObject(Kn5 kn5) {
                NonDefaultKn5 = kn5;
                Renderable = (Kn5RenderableList)Convert(kn5.RootNode);
            }

            public void Dispose() {
                Materials?.Dispose();
                Renderable?.Dispose();
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
        private IReadOnlyList<BlurredObject> _blurredObjects;

        private class BlurredObject {
            public int WheelIndex;
            public string StaticName;
            public string BlurredName;
        }

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
                        if (_blurredObjects.Any(x => x.BlurredName == dummy.OriginalNode.Name)) {
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

                foreach (var blurredObject in _blurredObjects) {
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
            var node = _currentLodObject.Renderable;
            node.UpdateBoundingBox();

            var wheelLf = node.GetDummyByName("WHEEL_LF");
            var wheelRf = node.GetDummyByName("WHEEL_RF");
            var wheelLr = node.GetDummyByName("WHEEL_LR");
            var wheelRr = node.GetDummyByName("WHEEL_RR");
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
        public void LoadMirrors(IDeviceContextHolder holder) {
            if (Data.IsEmpty) return;
            foreach (var obj in from section in Data.GetIniFile("mirrors.ini").GetSections("MIRROR")
                                select _currentLodObject.Renderable.GetByName(section.GetNonEmpty("NAME"))) {
                obj?.SwitchToMirror(holder);
            }
        }
        #endregion

        #region  Lights
        [CanBeNull]
        private IReadOnlyList<CarLight> _carLights;

        protected IEnumerable<T> LoadLights<T>() where T : CarLight, new() {
            if (Data.IsEmpty) yield break;

            var lightsIni = Data.GetIniFile("lights.ini");

            foreach (var section in lightsIni.GetSections("LIGHT")) {
                var light = new T();
                light.Initialize(CarLightType.Headlight, _currentLodObject.Renderable, section);
                yield return light;
            }

            foreach (var section in lightsIni.GetSections("BRAKE")) {
                var light = new T();
                light.Initialize(CarLightType.Brake, _currentLodObject.Renderable, section);
                yield return light;
            }
        }

        protected virtual IEnumerable<CarLight> LoadLights() {
            return LoadLights<CarLight>();
        }

        private void ReenableLights() {
            _carLights = null;
            if (_lightsEnabled) {
                _lightsEnabled = false;
                LightsEnabled = true;
            }
        }

        private bool _lightsEnabled;

        public bool LightsEnabled {
            get { return _lightsEnabled; }
            set {
                if (_carLights == null) {
                    _carLights = LoadLights().ToIReadOnlyListIfItsNot();
                }

                if (Equals(value, _lightsEnabled)) return;
                _lightsEnabled = value;

                foreach (var light in _carLights) {
                    light.IsEnabled = value;
                }

                OnPropertyChanged();
            }
        }
        #endregion

        #region Ambient shadows
        public AmbientShadow AmbientShadowNode;

        private float _shadowsHeight;
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
            var iniFile = Data.GetIniFile("ambient_shadows.ini");
            AmbientShadowSize = new Vector3(
                    (float)iniFile["SETTINGS"].GetDouble("WIDTH", 1d), 1.0f,
                    (float)iniFile["SETTINGS"].GetDouble("LENGTH", 1d));
        }

        public static Vector3 GetWheelShadowSize() {
            return new Vector3(0.3f, 1.0f, 0.3f);
        }

        private IRenderableObject LoadWheelAmbientShadow(string nodeName, string textureName) {
            var node = RootObject.GetDummyByName(nodeName);
            if (node == null) return null;

            var wheel = node.Matrix.GetTranslationVector() - LocalMatrix.GetTranslationVector();
            wheel.Y = _shadowsHeight;

            return new AmbientShadow(textureName,
                    Matrix.Scaling(GetWheelShadowSize()) * Matrix.RotationY(MathF.PI) * Matrix.Translation(wheel));
        }

        private IEnumerable<IRenderableObject> LoadAmbientShadows() {
            return Data.IsEmpty ? new IRenderableObject[0] : new[] {
                LoadBodyAmbientShadow(),
                LoadWheelAmbientShadow("WHEEL_LF", "tyre_0_shadow.png"),
                LoadWheelAmbientShadow("WHEEL_RF", "tyre_1_shadow.png"),
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
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}