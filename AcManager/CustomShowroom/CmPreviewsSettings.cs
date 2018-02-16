using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcTools;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SlimDX;

namespace AcManager.CustomShowroom {
    public class CmPreviewsSettings : DarkRendererSettings {
        public new static readonly string DefaultKey = "__CmPreviewsSettings";
        public new static readonly string DefaultPresetableKeyValue = "Custom Previews";
        protected override string KeyLockCamera => ".cmPsLc";

        public CmPreviewsSettings(DarkKn5ObjectRenderer renderer) : base(renderer, DefaultPresetableKeyValue) {
            renderer.VisibleUi = false;
            if (LockCamera) {
                Renderer.LockCamera = true;
            }
        }

        public new static void ResetHeavy() {
            if (!ValuesStorage.Contains(DefaultKey)) return;

            try {
                var data = JsonConvert.DeserializeObject<SaveableData>(ValuesStorage.Get<string>(DefaultKey));
                data.Width = CommonAcConsts.PreviewWidth;
                data.Height = CommonAcConsts.PreviewHeight;
                data.SsaaMode = 1;
                data.MsaaMode = 0;
                data.SsaaMode = 1;
                data.ShadowMapSize = 2048;
                data.CubemapReflectionMapSize = 1024;
                data.CubemapReflectionFacesPerFrame = 2;
                data.UsePcss = false;
                data.UseSslr = false;
                data.UseAo = false;
                data.UseDof = false;
                data.UseAccumulationDof = false;
                data.FlatMirrorBlurred = false;
                ValuesStorage.Set(DefaultKey, JsonConvert.SerializeObject(data));
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        internal new void Initialize(bool reset) {
            base.Initialize(reset);
            UpdateSize();
        }

        protected override ISaveHelper CreateSaveable() {
            return new SaveHelper<SaveableData>(DefaultKey, Save, Load, () => Reset(false));
        }

        protected override DarkRendererSettings.SaveableData CreateSaveableData() {
            return new SaveableData();
        }

        public override bool LoadCameraEnabled {
            get => true;
            set { }
        }

        private SaveableData ToSaveableData() {
            var obj = new SaveableData {
                Width = Width,
                Height = Height,
                SoftwareDownsize = SoftwareDownsize,
                FileName = FileName,
                AlignCar = AlignCar,
                AlignCameraHorizontally = AlignCameraHorizontally,
                AlignCameraHorizontallyOffset = AlignCameraHorizontallyOffset,
                AlignCameraHorizontallyOffsetRelative = AlignCameraHorizontallyOffsetRelative,
                AlignCameraVertically = AlignCameraVertically,
                AlignCameraVerticallyOffset = AlignCameraVerticallyOffset,
                AlignCameraVerticallyOffsetRelative = AlignCameraVerticallyOffsetRelative,
                SteerDeg = SteerDeg,
                HeadlightsEnabled = HeadlightsEnabled,
                BrakeLightsEnabled = BrakeLightsEnabled,
                LeftDoorOpen = LeftDoorOpen,
                RightDoorOpen = RightDoorOpen,
                ShowDriver = ShowDriver,
                TryToGuessCarLights = Renderer.TryToGuessCarLights,
                LoadCarLights = Renderer.LoadCarLights,
                LoadShowroomLights = Renderer.LoadShowroomLights
            };

            Save(obj);
            return obj;
        }

        protected SaveableData Save() {
            var obj = ToSaveableData();
            // obj.Checksum = obj.ToPreviewsOptions(false).GetChecksum();
            return obj;
        }

        public DarkPreviewsOptions ToPreviewsOptions() {
            return ToSaveableData().ToPreviewsOptions(true);
        }

        protected void LoadSize(SaveableData o) {
            Width = o.Width;
            Height = o.Height;
            SoftwareDownsize = o.SoftwareDownsize;
            FileName = o.FileName;
        }

        protected override void LoadCamera(DarkRendererSettings.SaveableData o) {
            LoadCamera((SaveableData)o);
        }

        protected void LoadCamera(SaveableData o) {
            CameraBusy.Do(() => {
                CameraPosition.Set(o.CameraPosition);
                CameraLookAt.Set(o.CameraLookAt);
                CameraTilt = o.CameraTilt;
                CameraFov = o.CameraFov;

                AlignCar = o.AlignCar;
                AlignCameraHorizontally = o.AlignCameraHorizontally;
                AlignCameraHorizontallyOffset = o.AlignCameraHorizontallyOffset;
                AlignCameraHorizontallyOffsetRelative = o.AlignCameraHorizontallyOffsetRelative;
                AlignCameraVertically = o.AlignCameraVertically;
                AlignCameraVerticallyOffset = o.AlignCameraVerticallyOffset;
                AlignCameraVerticallyOffsetRelative = o.AlignCameraVerticallyOffsetRelative;
            });

            PushCamera();
        }

        protected void LoadCar(SaveableData o) {
            SteerDeg = o.SteerDeg;
            HeadlightsEnabled = o.HeadlightsEnabled;
            BrakeLightsEnabled = o.BrakeLightsEnabled;
            LeftDoorOpen = o.LeftDoorOpen;
            RightDoorOpen = o.RightDoorOpen;
            ShowDriver = o.ShowDriver;
        }

        protected void Load(SaveableData o) {
            Renderer.TryToGuessCarLights = o.TryToGuessCarLights;
            Renderer.LoadCarLights = o.LoadCarLights;
            Renderer.LoadShowroomLights = o.LoadShowroomLights;

            LoadSize(o);
            LoadCar(o);
            base.Load(o);
        }

        [NotNull]
        public static DarkPreviewsOptions GetSavedOptions(string presetFilename = null) {
            if (presetFilename != null && !File.Exists(presetFilename)) {
                var builtIn = PresetsManager.Instance.GetBuiltInPreset(new PresetsCategory(DefaultPresetableKeyValue), presetFilename);
                if (builtIn != null) {
                    return (SaveHelper<SaveableData>.LoadSerialized(builtIn.ReadData()) ?? new SaveableData())
                            .ToPreviewsOptions(true);
                }

                NonfatalError.NotifyBackground("Can’t load preset", $"File “{presetFilename}” not found.");
            } else {
                try {
                    return ((presetFilename != null ?
                            SaveHelper<SaveableData>.LoadSerialized(File.ReadAllText(presetFilename)) :
                            SaveHelper<SaveableData>.Load(DefaultKey)) ?? new SaveableData()).ToPreviewsOptions(true);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t load preset", e);
                }
            }

            return new SaveableData().ToPreviewsOptions(true);
        }

        protected new sealed class SaveableData : DarkRendererSettings.SaveableData {
            public override Color AmbientDownColor { get; set; } = Color.FromRgb(0x51, 0x5E, 0x6D);
            public override Color AmbientUpColor { get; set; } = Color.FromRgb(0xC6, 0xE6, 0xFF);
            public override Color BackgroundColor { get; set; } = Colors.White;
            public override Color LightColor { get; set; } = Color.FromRgb(0xFF, 0xEA, 0xCC);

            public override int MsaaMode { get; set; }
            public override int SsaaMode { get; set; } = 4;
            public override int ShadowMapSize { get; set; } = 1024;

            public override string ShowroomId { get; set; }
            public override string ColorGrading { get; set; }

            [JsonConstructor]
            public SaveableData(string showroomId) {
                ShowroomId = showroomId;
            }

            public SaveableData() {
                ShowroomId = "at_previews";
            }

            public override ToneMappingFn ToneMapping { get; set; } = ToneMappingFn.Filmic;
            public override AoType AoType { get; set; } = AoType.Ssao;

            [JsonProperty("CubemapAmbientValue")]
            public override float CubemapAmbient { get; set; }
            public override bool CubemapAmbientWhite { get; set; } = true;
            public override bool EnableShadows { get; set; } = true;
            public override bool FlatMirror { get; set; }
            public override bool FlatMirrorBlurred { get; set; } = true;
            public override float FlatMirrorBlurMuiltiplier { get; set; } = 1f;
            public override bool UseBloom { get; set; } = true;
            public override bool UseDither { get; set; }
            public override bool UseColorGrading { get; set; }
            public override bool UseFxaa { get; set; } = true;
            public override bool UsePcss { get; set; }
            public override bool UseSmaa { get; set; }
            public override bool UseAo { get; set; }
            public override bool UseSslr { get; set; } = true;
            public override bool ReflectionCubemapAtCamera { get; set; } = true;
            public override bool ReflectionsWithShadows { get; set; } = false;
            public override bool ReflectionsWithMultipleLights { get; set; } = false;

            public override float AmbientBrightness { get; set; } = 3f;
            public override float BackgroundBrightness { get; set; } = 3f;
            public override float FlatMirrorReflectiveness { get; set; } = 0.6f;
            public override bool FlatMirrorReflectedLight { get; set; } = false;
            public override float LightBrightness { get; set; } = 12f;
            public override float Lightθ { get; set; } = 45f;
            public override float Lightφ { get; set; } = 0f;
            public override float MaterialsReflectiveness { get; set; } = 0.66f;
            public override float CarShadowsOpacity { get; set; } = 1f;
            public override float ToneExposure { get; set; } = 1.634f;
            public override float ToneGamma { get; set; } = 0.85f;
            public override float ToneWhitePoint { get; set; } = 2.0f;
            public override float PcssSceneScale { get; set; } = 0.06f;
            public override float PcssLightScale { get; set; } = 2f;
            public override float BloomRadiusMultiplier { get; set; } = 1f;

            [JsonProperty("SsaoOpacity")]
            public override float AoOpacity { get; set; } = 0.3f;

            public int Width = CommonAcConsts.PreviewWidth, Height = CommonAcConsts.PreviewHeight;
            public bool SoftwareDownsize, AlignCar = true,
                    AlignCameraHorizontally, AlignCameraVertically, AlignCameraHorizontallyOffsetRelative, AlignCameraVerticallyOffsetRelative,
                    HeadlightsEnabled, BrakeLightsEnabled, LeftDoorOpen, RightDoorOpen, ShowDriver,
                    TryToGuessCarLights = true, LoadCarLights, LoadShowroomLights;
            public string FileName { get; set; } = "preview.jpg";

            public override bool UseDof { get; set; } = true;
            public override float DofFocusPlane { get; set; } = 1.6f;
            public override float DofScale { get; set; } = 1f;
            public override bool UseAccumulationDof { get; set; } = true;
            public override bool AccumulationDofBokeh { get; set; }
            public override int AccumulationDofIterations { get; set; } = 40;
            public override float AccumulationDofApertureSize { get; set; } = 0f;

            public override double[] CameraPosition { get; set; } = { -3.8676, 1.4236, 4.7038 };
            public override double[] CameraLookAt { get; set; } = { 0, 0.7, 0.5 };
            public override float CameraFov { get; set; } = 30f;

            public float AlignCameraHorizontallyOffset, AlignCameraVerticallyOffset, SteerDeg;
            // public string Checksum { get; set; }

            /// <summary>
            /// Convert to DarkPreviewsOptions.
            /// </summary>
            /// <param name="keepChecksum">Set to True if you’re loading existing preset and want checksum to be
            /// independent from any future format changes; otherwise, for actual checksum, set to False.</param>
            /// <returns>Instance of DarkPreviewsOptions.</returns>
            [NotNull]
            public DarkPreviewsOptions ToPreviewsOptions(bool keepChecksum) {
                var light = MathF.ToVector3Deg(Lightθ, Lightφ);
                return new DarkPreviewsOptions {
                    AmbientDown = AmbientDownColor.ToColor(),
                    AmbientUp = AmbientUpColor.ToColor(),
                    BackgroundColor = BackgroundColor.ToColor(),
                    LightColor = LightColor.ToColor(),

                    UseMsaa = MsaaMode != 0,
                    MsaaSampleCount = MsaaMode,
                    SsaaMultiplier = Math.Sqrt(SsaaMode),
                    ShadowMapSize = ShadowMapSize,

                    Showroom = ShowroomId,
                    ColorGradingData = UseColorGrading ? ColorGradingData : null,

                    CubemapAmbient = CubemapAmbient,
                    CubemapAmbientWhite = CubemapAmbientWhite,
                    EnableShadows = EnableShadows,
                    AnyGround = AnyGround,
                    FlatMirror = FlatMirror,
                    UseBloom = UseBloom,
                    FlatMirrorBlurred = FlatMirrorBlurred,
                    FlatMirrorBlurMuiltiplier = FlatMirrorBlurMuiltiplier,
                    UseDither = UseDither,
                    UseColorGrading = UseColorGrading,
                    UseFxaa = UseFxaa,
                    UseSmaa = UseSmaa,
                    UsePcss = UsePcss,
                    UseAo = UseAo,
                    AoType = AoType,
                    UseSslr = UseSslr,
                    ToneMapping = ToneMapping,
                    ReflectionCubemapAtCamera = ReflectionCubemapAtCamera,
                    ReflectionsWithShadows = ReflectionsWithShadows,
                    ReflectionsWithMultipleLights = ReflectionsWithMultipleLights,

                    AmbientBrightness = AmbientBrightness,
                    BackgroundBrightness = BackgroundBrightness,
                    FlatMirrorReflectiveness = FlatMirrorReflectiveness,
                    FlatMirrorReflectedLight = FlatMirrorReflectedLight,
                    LightBrightness = LightBrightness,
                    LightDirection = new double[] { light.X, light.Y, light.Z },
                    MaterialsReflectiveness = MaterialsReflectiveness,
                    CarShadowsOpacity = CarShadowsOpacity,
                    ToneExposure = ToneExposure,
                    ToneGamma = ToneGamma,
                    ToneWhitePoint = ToneWhitePoint,

                    PcssSceneScale = PcssSceneScale,
                    PcssLightScale = PcssLightScale,
                    BloomRadiusMultiplier = BloomRadiusMultiplier,
                    AoOpacity = AoOpacity,
                    AoRadius = AoRadius,

                    UseDof = UseDof,
                    DofFocusPlane = DofFocusPlane,
                    DofScale = DofScale,
                    UseAccumulationDof = UseAccumulationDof,
                    AccumulationDofBokeh = AccumulationDofBokeh,
                    AccumulationDofIterations = AccumulationDofIterations,
                    AccumulationDofApertureSize = AccumulationDofApertureSize,

                    PreviewWidth = Width,
                    PreviewHeight = Height,

                    SoftwareDownsize = SoftwareDownsize,
                    AlignCar = AlignCar,
                    AlignCameraHorizontally = AlignCameraHorizontally,
                    AlignCameraVertically = AlignCameraVertically,
                    AlignCameraHorizontallyOffsetRelative = AlignCameraHorizontallyOffsetRelative,
                    AlignCameraVerticallyOffsetRelative = AlignCameraVerticallyOffsetRelative,
                    HeadlightsEnabled = HeadlightsEnabled,
                    BrakeLightsEnabled = BrakeLightsEnabled,
                    LeftDoorOpen = LeftDoorOpen,
                    RightDoorOpen = RightDoorOpen,
                    ShowDriver = ShowDriver,

                    CameraPosition = CameraPosition,
                    CameraLookAt = CameraLookAt,
                    CameraTilt = CameraTilt,
                    CameraFov = CameraFov,
                    AlignCameraHorizontallyOffset = AlignCameraHorizontallyOffset,
                    AlignCameraVerticallyOffset = AlignCameraVerticallyOffset,
                    SteerDeg = SteerDeg,

                    TryToGuessCarLights = TryToGuessCarLights,
                    LoadCarLights = LoadCarLights,
                    LoadShowroomLights= LoadShowroomLights,

                    DelayedConvertation = false,
                    MeshDebugMode = false,
                    SuspensionDebugMode = false,
                    PreviewName = FileName,
                    WireframeMode = false,
                    SerializedLights = ExtraLights != null ? new JArray(ExtraLights.OfType<object>()).ToString() : null,

                    // FixedChecksum = keepChecksum ? Checksum : null
                };
            }
        }

        protected override void OnRendererPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (Renderer.ShotInProcess) return;

            switch (e.PropertyName) {
                /*case nameof(Renderer.UseSsaa):
                    if (!HighQualityPreview) {
                        //SyncSsaaMode();
                        return;
                    }
                    break;

                case nameof(Renderer.ResolutionMultiplier):
                    if (!HighQualityPreview) return;
                    break;*/

                case nameof(Renderer.ActualWidth):
                case nameof(Renderer.ActualHeight):
                    SyncSize();
                    return;

                case nameof(Renderer.LoadCarLights):
                case nameof(Renderer.LoadShowroomLights):
                case nameof(Renderer.TryToGuessCarLights):
                    ActionExtension.InvokeInMainThread(SaveLater);
                    return;
            }

            base.OnRendererPropertyChanged(sender, e);
        }

        private bool? _highQualityPreview;

        public bool HighQualityPreview {
            get => _highQualityPreview ?? (_highQualityPreview = ValuesStorage.Get(".cmPsHqPreview", false)).Value;
            set {
                if (Equals(value, HighQualityPreview)) return;
                _highQualityPreview = value;
                ValuesStorage.Set(".cmPsHqPreview", value);
                OnPropertyChangedSkipSaving();
                Renderer.ResolutionMultiplier = GetRendererResolutionMultipler();
            }
        }

        protected override double GetRendererResolutionMultipler() {
            return HighQualityPreview ? base.GetRendererResolutionMultipler() : Math.Min(base.GetRendererResolutionMultipler(), 2d);
        }

        #region Size
        private bool _sizeBusy;

        private void SyncSize() {
            try {
                if (_sizeBusy) return;
                _sizeBusy = true;

                Width = Renderer.ActualWidth;
                Height = Renderer.ActualHeight;
            } finally {
                _sizeBusy = false;
            }
        }

        private void UpdateSize() {
            try {
                if (_sizeBusy) return;
                _sizeBusy = true;

                var form = Renderer.AssociatedWindow;
                if (form != null) {
                    var width = Width;
                    var height = Height;
                    var scale = 1.0;

                    var screen = Screen.FromControl(form).Bounds;
                    var maxWidth = screen.Width * 0.9;
                    var maxHeight = screen.Height * 0.9;

                    if (width > maxWidth) {
                        scale *= maxWidth / width;
                        height = (int)(height * maxWidth / width);
                        width = (int)maxWidth;
                    }

                    if (height > maxHeight) {
                        scale *= maxHeight / height;
                        width = (int)(width * maxHeight / height);
                        height = (int)maxHeight;
                    }

                    var borderWidth = form.Width - form.ClientSize.Width;
                    var borderHeight = form.Height - form.ClientSize.Height;
                    form.Width = width + borderWidth;
                    form.Height = height + borderHeight;
                    Renderer.Width = width;
                    Renderer.Height = height;
                    Renderer.ResolutionMultiplier = GetRendererResolutionMultipler() / scale;
                }
            } finally {
                _sizeBusy = false;
            }
        }

        private int _width = CommonAcConsts.PreviewWidth;

        public int Width {
            get => _width;
            set {
                if (Equals(value, _width)) return;
                _width = value;
                OnPropertyChanged();
                UpdateSize();
            }
        }

        private int _height = CommonAcConsts.PreviewHeight;

        public int Height {
            get => _height;
            set {
                if (Equals(value, _height)) return;
                _height = value;
                OnPropertyChanged();
                UpdateSize();
            }
        }

        private bool _softwareDownsize;

        public bool SoftwareDownsize {
            get => _softwareDownsize;
            set => Apply(value, ref _softwareDownsize);
        }

        private string _fileName;

        public string FileName {
            get => _fileName;
            set => Apply(value, ref _fileName);
        }

        private void ResetSize() {
            LoadSize((SaveableData)CreateSaveableData());
        }

        private DelegateCommand _resetSizeCommand;

        public DelegateCommand ResetSizeCommand => _resetSizeCommand ?? (_resetSizeCommand = new DelegateCommand(ResetSize));
        #endregion

        #region Camera
        private bool _alignCar;

        public bool AlignCar {
            get => _alignCar;
            set {
                if (Equals(value, _alignCar)) return;
                _alignCar = value;
                OnPropertyChanged();
                PushCamera();
            }
        }

        private bool _alignCameraHorizontally;

        public bool AlignCameraHorizontally {
            get => _alignCameraHorizontally;
            set {
                if (Equals(value, _alignCameraHorizontally)) return;
                _alignCameraHorizontally = value;
                OnPropertyChanged();
                PushCamera();
            }
        }

        private float _alignCameraHorizontallyOffset;

        public float AlignCameraHorizontallyOffset {
            get => _alignCameraHorizontallyOffset;
            set {
                if (Equals(value, _alignCameraHorizontallyOffset)) return;
                _alignCameraHorizontallyOffset = value;
                OnPropertyChanged();
                PushCamera();
            }
        }

        private bool _alignCameraHorizontallyOffsetRelative;

        public bool AlignCameraHorizontallyOffsetRelative {
            get => _alignCameraHorizontallyOffsetRelative;
            set {
                if (Equals(value, _alignCameraHorizontallyOffsetRelative)) return;
                _alignCameraHorizontallyOffsetRelative = value;
                OnPropertyChanged();
                PushCamera();
            }
        }

        private bool _alignCameraVertically;

        public bool AlignCameraVertically {
            get => _alignCameraVertically;
            set {
                if (Equals(value, _alignCameraVertically)) return;
                _alignCameraVertically = value;
                OnPropertyChanged();
                PushCamera();
            }
        }

        private float _alignCameraVerticallyOffset;

        public float AlignCameraVerticallyOffset {
            get => _alignCameraVerticallyOffset;
            set {
                if (Equals(value, _alignCameraVerticallyOffset)) return;
                _alignCameraVerticallyOffset = value;
                OnPropertyChanged();
                PushCamera();
            }
        }

        private bool _alignCameraVerticallyOffsetRelative;

        public bool AlignCameraVerticallyOffsetRelative {
            get => _alignCameraVerticallyOffsetRelative;
            set {
                if (Equals(value, _alignCameraVerticallyOffsetRelative)) return;
                _alignCameraVerticallyOffsetRelative = value;
                OnPropertyChanged();
                PushCamera();
            }
        }

        protected override void PullCamera() {
            CameraBusy.Do(() => {
                var offset = AlignCar ? -Renderer.MainSlot.CarCenter : Vector3.Zero;
                CameraPosition.Set(Renderer.Camera.Position + offset);
                CameraLookAt.Set((Renderer.CameraOrbit?.Target ?? Renderer.Camera.Position + Renderer.Camera.Look) + offset);
                CameraFov = Renderer.Camera.FovY.ToDegrees();
                AlignCameraHorizontally = false;
                AlignCameraVertically = false;
            });
        }

        protected override void PushCamera(bool force = false) {
            CameraBusy.Do(() => {
                Renderer.SetCamera(CameraPosition.ToVector(), CameraLookAt.ToVector(), CameraFov.ToRadians(), CameraTilt.ToRadians());
                if (AlignCar) {
                    Renderer.AlignCar();
                }

                Renderer.AlignCamera(AlignCameraHorizontally, AlignCameraHorizontallyOffset, AlignCameraHorizontallyOffsetRelative,
                        AlignCameraVertically, AlignCameraVerticallyOffset, AlignCameraVerticallyOffsetRelative);
                CameraIgnoreNext = true;
            });
        }
        #endregion

        #region Car params
        private AsyncCommand _changeCarCommand;

        public AsyncCommand ChangeCarCommand => _changeCarCommand ?? (_changeCarCommand = new AsyncCommand(() => {
            var currentCar = Renderer.CarNode?.CarId;
            var car = SelectCarDialog.Show(currentCar == null ? null : CarsManager.Instance.GetById(currentCar));
            return car == null ? Task.Delay(0) : Renderer.MainSlot.SetCarAsync(CarDescription.FromDirectory(car.Location, car.AcdData), car.SelectedSkin?.Id);
        }));

        private void SetCarProperty(Action<Kn5RenderableCar> a) {
            var car = Renderer.CarNode;
            if (car != null) {
                Renderer.ResetDelta();
                a.Invoke(car);
                Renderer.IsDirty = true;
            }
        }

        private float _steerDeg;

        public float SteerDeg {
            get => _steerDeg;
            set {
                if (Equals(value, _steerDeg)) return;
                _steerDeg = value;
                OnPropertyChanged();
                SetCarProperty(car => car.SteerDeg = value);
            }
        }

        private bool _headlightsEnabled;

        public bool HeadlightsEnabled {
            get => _headlightsEnabled;
            set {
                if (Equals(value, _headlightsEnabled)) return;
                _headlightsEnabled = value;
                OnPropertyChanged();
                SetCarProperty(car => car.HeadlightsEnabled = value);
            }
        }

        private bool _brakeLightsEnabled;

        public bool BrakeLightsEnabled {
            get => _brakeLightsEnabled;
            set {
                if (Equals(value, _brakeLightsEnabled)) return;
                _brakeLightsEnabled = value;
                OnPropertyChanged();
                SetCarProperty(car => car.BrakeLightsEnabled = value);
            }
        }

        private bool _leftDoorOpen;

        public bool LeftDoorOpen {
            get => _leftDoorOpen;
            set {
                if (Equals(value, _leftDoorOpen)) return;
                _leftDoorOpen = value;
                OnPropertyChanged();
                SetCarProperty(car => car.LeftDoorOpen = value);
            }
        }

        private bool _rightDoorOpen;

        public bool RightDoorOpen {
            get => _rightDoorOpen;
            set {
                if (Equals(value, _rightDoorOpen)) return;
                _rightDoorOpen = value;
                OnPropertyChanged();
                SetCarProperty(car => car.RightDoorOpen = value);
            }
        }

        private bool _showDriver;

        public bool ShowDriver {
            get => _showDriver;
            set {
                if (Equals(value, _showDriver)) return;
                _showDriver = value;
                OnPropertyChanged();
                SetCarProperty(car => car.IsDriverVisible = value);
            }
        }

        private void ResetCar() {
            LoadCar((SaveableData)CreateSaveableData());
        }

        private DelegateCommand _resetCarCommand;

        public DelegateCommand ResetCarCommand => _resetCarCommand ?? (_resetCarCommand = new DelegateCommand(ResetCar));
        #endregion

        protected override void Reset(bool saveLater) {
            ResetSize();
            ResetCamera();
            ResetCar();
            base.Reset(saveLater);

            Renderer.LoadCarLights = false;
            Renderer.LoadShowroomLights = false;
            Renderer.TryToGuessCarLights = true;
        }

        protected override async Task Share() {
            var data = ExportToPresetData();
            if (data == null) return;
            await SharingUiHelper.ShareAsync(SharedEntryType.CustomPreviewsPreset,
                    Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(DefaultPresetableKeyValue)), null,
                    data);
        }

        [CanBeNull]
        private static SaveableData ToSaveableData(DarkRendererSettings settings, DarkKn5ObjectRenderer renderer) {
            var data = SaveHelper<SaveableData>.LoadSerialized(settings.ExportToPresetData());
            if (data == null) {
                Logging.Unexpected();
                return null;
            }

            data.CameraPosition = Coordinates.Create(renderer.Camera.Position).ToArray();
            data.CameraLookAt = Coordinates.Create(renderer.CameraOrbit?.Target ?? renderer.Camera.Look).ToArray();
            data.CameraFov = renderer.Camera.FovY.ToDegrees();
            data.AlignCar = false;
            data.AlignCameraHorizontally = false;
            data.AlignCameraVertically = false;
            data.LoadCarLights = renderer.LoadCarLights;
            data.LoadShowroomLights = renderer.LoadShowroomLights;
            data.TryToGuessCarLights = renderer.TryToGuessCarLights;

            var car = renderer.CarNode;
            if (car != null) {
                data.SteerDeg = car.SteerDeg;
                data.LeftDoorOpen = car.LeftDoorOpen;
                data.RightDoorOpen = car.RightDoorOpen;
                data.HeadlightsEnabled = car.HeadlightsEnabled;
                data.BrakeLightsEnabled = car.BrakeLightsEnabled;
                data.ShowDriver = car.IsDriverVisible;
            }

            return data;
        }

        public static void Transfer(DarkRendererSettings settings, DarkKn5ObjectRenderer renderer) {
            var data = ToSaveableData(settings, renderer);

            var filename = UserPresetsControl.GetCurrentFilename(DarkRendererSettings.DefaultPresetableKeyValue);
            if (filename != null) {
                var relative = FileUtils.GetPathWithin(filename, PresetsManager.Instance.Combine(DarkRendererSettings.DefaultPresetableKeyValue));
                filename = relative == null ? null : Path.Combine(PresetsManager.Instance.Combine(DefaultPresetableKeyValue), relative);
            }

            PresetsManager.Instance.SavePresetUsingDialog(null, new PresetsCategory(DefaultPresetableKeyValue),
                    SaveHelper<SaveableData>.Serialize(data), filename);
        }
    }

    public class Coordinates : NotifyPropertyChanged {
        private double _x;

        public double X {
            get => _x;
            set => Apply(value, ref _x);
        }

        private double _y;

        public double Y {
            get => _y;
            set => Apply(value, ref _y);
        }

        private double _z;

        public double Z {
            get => _z;
            set => Apply(value, ref _z);
        }

        public void Set(double x, double y, double z) {
            X = x;
            Y = y;
            Z = z;
        }

        public void Set(double[] v) {
            X = v[0];
            Y = v[1];
            Z = v[2];
        }

        public void Set(Vector3 v) {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }

        public static Coordinates Create(Vector3 v) {
            var c = new Coordinates();
            c.Set(v);
            return c;
        }

        public Vector3 ToVector() {
            return new Vector3((float)X, (float)Y, (float)Z);
        }

        public double[] ToArray() {
            return new double[] { (float)X, (float)Y, (float)Z };
        }
    }
}