using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SlimDX;

namespace AcManager.CustomShowroom {
    public class CmPreviewsSettings : DarkRendererSettings {
        // TODO: rearrange code!
        public static Func<CarObject, CarObject> SelectCarDialog;

        public new static readonly string DefaultKey = "__CmPreviewsSettings";
        public new static readonly string DefaultPresetableKeyValue = "Custom Previews";

        public CmPreviewsSettings(DarkKn5ObjectRenderer renderer) : base(renderer, DefaultPresetableKeyValue) {
            CameraPosition.PropertyChanged += OnCameraCoordinatePropertyChanged;
            CameraLookAt.PropertyChanged += OnCameraCoordinatePropertyChanged;
            Renderer.CameraMoved += OnCameraMoved;
            renderer.VisibleUi = false;

            if (LockCamera) {
                Renderer.LockCamera = true;
            }
        }

        internal new void Initialize(bool reset) {
            base.Initialize(reset);

            UpdateSize();
            UpdateCamera();
        }

        private void OnCameraCoordinatePropertyChanged(object sender, PropertyChangedEventArgs e) {
            SaveLater();
            UpdateCamera();
        }

        protected override ISaveHelper CreateSaveable() {
            return new SaveHelper<SaveableData>(DefaultKey, Save, Load, () => {
                Reset(false);
            });
        }

        protected override DarkRendererSettings.SaveableData CreateSaveableData() {
            return new SaveableData();
        }

        private SaveableData ToSaveableData() {
            var obj = new SaveableData {
                Width = Width,
                Height = Height,
                SoftwareDownsize = SoftwareDownsize,
                CameraPosition = CameraPosition.ToArray(),
                CameraLookAt = CameraLookAt.ToArray(),
                CameraFov = CameraFov,
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
            };

            Save(obj);
            return obj;
        }

        protected SaveableData Save() {
            var obj = ToSaveableData();
            obj.Checksum = obj.ToPreviewsOptions(false).GetChecksum();
            return obj;
        }

        public DarkPreviewsOptions ToPreviewsOptions() {
            return ToSaveableData().ToPreviewsOptions(true);
        }

        protected void LoadSize(SaveableData o) {
            Width = o.Width;
            Height = o.Height;
            SoftwareDownsize = o.SoftwareDownsize;
        }

        protected void LoadCamera(SaveableData o) {
            _cameraBusy = true;

            try {
                CameraPosition.Set(o.CameraPosition);
                CameraLookAt.Set(o.CameraLookAt);
                CameraFov = o.CameraFov;
                AlignCar = o.AlignCar;
                AlignCameraHorizontally = o.AlignCameraHorizontally;
                AlignCameraHorizontallyOffset = o.AlignCameraHorizontallyOffset;
                AlignCameraHorizontallyOffsetRelative = o.AlignCameraHorizontallyOffsetRelative;
                AlignCameraVertically = o.AlignCameraVertically;
                AlignCameraVerticallyOffset = o.AlignCameraVerticallyOffset;
                AlignCameraVerticallyOffsetRelative = o.AlignCameraVerticallyOffsetRelative;
            } finally {
                _cameraBusy = false;
                UpdateCamera();
            }
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
            LoadSize(o);
            LoadCamera(o);
            LoadCar(o);
            base.Load(o);
        }

        [NotNull]
        public static DarkPreviewsOptions GetSavedOptions(string presetFilename = null) {
            if (presetFilename != null && !File.Exists(presetFilename)) {
                var builtIn = PresetsManager.Instance.GetBuiltInPreset(DefaultPresetableKeyValue, presetFilename);
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

        protected new class SaveableData : DarkRendererSettings.SaveableData {
            public override Color AmbientDownColor { get; set; } = Color.FromRgb(0xc0, 0xcc, 0xcc);
            public override Color AmbientUpColor { get; set; } = Color.FromRgb(0xcc, 0xcc, 0xc0);
            public override Color BackgroundColor { get; set; } = Color.FromRgb(0, 0, 0);
            public override Color LightColor { get; set; } = Color.FromRgb(255, 255, 255);

            public override int MsaaMode { get; set; } = 0;
            public override int SsaaMode { get; set; } = 16;
            public override int ShadowMapSize { get; set; } = 4096;

            public override string ShowroomId { get; set; }
            public override string ColorGrading { get; set; }

            public override ToneMappingFn ToneMapping { get; set; } = ToneMappingFn.Reinhard;
            public override AoType AoType { get; set; } = AoType.Ssao;

            [JsonProperty("CubemapAmbientValue")]
            public override float CubemapAmbient { get; set; } = 0.5f;
            public override bool CubemapAmbientWhite { get; set; } = true;
            public override bool EnableShadows { get; set; } = true;
            public override bool FlatMirror { get; set; }
            public override bool FlatMirrorBlurred { get; set; } = true;
            public override bool UseBloom { get; set; } = true;
            public override bool UseColorGrading { get; set; }
            public override bool UseFxaa { get; set; } = true;
            public override bool UsePcss { get; set; } = true;
            public override bool UseSmaa { get; set; }
            public override bool UseAo { get; set; }
            public override bool UseSslr { get; set; } = true;
            public override bool ReflectionCubemapAtCamera { get; set; } = true;
            public override bool ReflectionsWithShadows { get; set; } = false;

            public override float AmbientBrightness { get; set; } = 3f;
            public override float BackgroundBrightness { get; set; } = 1f;
            public override float FlatMirrorReflectiveness { get; set; } = 0.6f;
            public override float LightBrightness { get; set; } = 0.5f;
            public override float Lightθ { get; set; } = 50f;
            public override float Lightφ { get; set; } = 104f;
            public override float MaterialsReflectiveness { get; set; } = 1.2f;
            public override float ToneExposure { get; set; } = 1.2f;
            public override float ToneGamma { get; set; } = 1f;
            public override float ToneWhitePoint { get; set; } = 1.2f;
            public override float PcssSceneScale { get; set; } = 0.06f;
            public override float PcssLightScale { get; set; } = 2f;
            public override float BloomRadiusMultiplier { get; set; } = 0.8f;
            public override float SsaoOpacity { get; set; } = 0.3f;


            public int Width = CommonAcConsts.PreviewWidth, Height = CommonAcConsts.PreviewHeight;
            public bool SoftwareDownsize, AlignCar = true, AlignCameraHorizontally = true, AlignCameraVertically, AlignCameraHorizontallyOffsetRelative = true,
                    AlignCameraVerticallyOffsetRelative = true, HeadlightsEnabled, BrakeLightsEnabled, LeftDoorOpen, RightDoorOpen, ShowDriver;
            public double[] CameraPosition = { 3.194, 0.342, 13.049 }, CameraLookAt = { 2.945, 0.384, 12.082 };
            public float CameraFov = 9f, AlignCameraHorizontallyOffset = 0.3f, AlignCameraVerticallyOffset, SteerDeg;

            public string Checksum { get; set; }

            /// <summary>
            /// Convert to DarkPreviewsOptions.
            /// </summary>
            /// <param name="keepChecksum">Set to True if you’re loading existing preset and want checksum to be 
            /// independent from any future format changes; otherwise, for actual checksum, set to False.</param>
            /// <returns>Instance of DarkPreviewsOptions.</returns>
            [NotNull]
            public DarkPreviewsOptions ToPreviewsOptions(bool keepChecksum) {
                var light = GetLightDirection(Lightθ, Lightφ);
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
                    FlatMirror = FlatMirror,
                    UseBloom = UseBloom,
                    FlatMirrorBlurred = FlatMirrorBlurred,
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

                    AmbientBrightness = AmbientBrightness,
                    BackgroundBrightness = BackgroundBrightness,
                    FlatMirrorReflectiveness = FlatMirrorReflectiveness,
                    LightBrightness = LightBrightness,
                    LightDirection = new double[] { light.X, light.Y, light.Z },
                    MaterialsReflectiveness = MaterialsReflectiveness,
                    ToneExposure = ToneExposure,
                    ToneGamma = ToneGamma,
                    ToneWhitePoint = ToneWhitePoint,

                    PcssSceneScale = PcssSceneScale,
                    PcssLightScale = PcssLightScale,
                    BloomRadiusMultiplier = BloomRadiusMultiplier,
                    SsaoOpacity = SsaoOpacity,

                    UseDof = UseDof,
                    DofFocusPlane = DofFocusPlane,
                    DofScale = DofScale,
                    UseAccumulationDof = UseAccumulationDof,
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

                    CameraFov = CameraFov,
                    AlignCameraHorizontallyOffset = AlignCameraHorizontallyOffset,
                    AlignCameraVerticallyOffset = AlignCameraVerticallyOffset,
                    SteerDeg = SteerDeg,

                    DelayedConvertation = false,
                    MeshDebugMode = false,
                    SuspensionDebugMode = false,
                    PreviewName = "preview.jpg",
                    WireframeMode = false,

                    FixedChecksum = keepChecksum ? Checksum : null
                };
            }
        }

        protected override void OnRendererPropertyChanged(object sender, PropertyChangedEventArgs e) {
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
            }

            base.OnRendererPropertyChanged(sender, e);
        }

        private bool? _highQualityPreview;

        public bool HighQualityPreview {
            get { return _highQualityPreview ?? (_highQualityPreview = ValuesStorage.GetBool(".cmPsHqPreview")).Value; }
            set {
                if (Equals(value, HighQualityPreview)) return;
                _highQualityPreview = value;
                ValuesStorage.Set(".cmPsHqPreview", value);
                OnPropertyChanged(save: false);
                Renderer.ResolutionMultiplier = GetRendererResolutionMultipler();
            }
        }

        private bool? _lockCamera;

        public bool LockCamera {
            get { return _lockCamera ?? (_lockCamera = ValuesStorage.GetBool(".cmPsLc")).Value; }
            set {
                if (Equals(value, LockCamera)) return;
                _lockCamera = value;
                ValuesStorage.Set(".cmPsLc", value);
                OnPropertyChanged(save: false);
                Renderer.LockCamera = value;
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
            get { return _width; }
            set {
                if (Equals(value, _width)) return;
                _width = value;
                OnPropertyChanged();
                UpdateSize();
            }
        }

        private int _height = CommonAcConsts.PreviewHeight;

        public int Height {
            get { return _height; }
            set {
                if (Equals(value, _height)) return;
                _height = value;
                OnPropertyChanged();
                UpdateSize();
            }
        }

        private bool _softwareDownsize;

        public bool SoftwareDownsize {
            get { return _softwareDownsize; }
            set {
                if (Equals(value, _softwareDownsize)) return;
                _softwareDownsize = value;
                OnPropertyChanged();
            }
        }

        private void ResetSize() {
            LoadSize((SaveableData)CreateSaveableData());
        }

        private DelegateCommand _resetSizeCommand;

        public DelegateCommand ResetSizeCommand => _resetSizeCommand ?? (_resetSizeCommand = new DelegateCommand(ResetSize));
        #endregion

        #region Camera
        public Coordinates CameraPosition { get; } = new Coordinates();

        public Coordinates CameraLookAt { get; } = new Coordinates();

        private float _cameraFov;

        public float CameraFov {
            get { return _cameraFov; }
            set {
                if (Equals(value, _cameraFov)) return;
                _cameraFov = value;
                OnPropertyChanged();
                UpdateCamera();
            }
        }

        private bool _alignCar;

        public bool AlignCar {
            get { return _alignCar; }
            set {
                if (Equals(value, _alignCar)) return;
                _alignCar = value;
                OnPropertyChanged();
                UpdateCamera();
            }
        }

        private bool _alignCameraHorizontally;

        public bool AlignCameraHorizontally {
            get { return _alignCameraHorizontally; }
            set {
                if (Equals(value, _alignCameraHorizontally)) return;
                _alignCameraHorizontally = value;
                OnPropertyChanged();
                UpdateCamera();
            }
        }

        private float _alignCameraHorizontallyOffset;

        public float AlignCameraHorizontallyOffset {
            get { return _alignCameraHorizontallyOffset; }
            set {
                if (Equals(value, _alignCameraHorizontallyOffset)) return;
                _alignCameraHorizontallyOffset = value;
                OnPropertyChanged();
                UpdateCamera();
            }
        }

        private bool _alignCameraHorizontallyOffsetRelative;

        public bool AlignCameraHorizontallyOffsetRelative {
            get { return _alignCameraHorizontallyOffsetRelative; }
            set {
                if (Equals(value, _alignCameraHorizontallyOffsetRelative)) return;
                _alignCameraHorizontallyOffsetRelative = value;
                OnPropertyChanged();
                UpdateCamera();
            }
        }

        private bool _alignCameraVertically;

        public bool AlignCameraVertically {
            get { return _alignCameraVertically; }
            set {
                if (Equals(value, _alignCameraVertically)) return;
                _alignCameraVertically = value;
                OnPropertyChanged();
                UpdateCamera();
            }
        }

        private float _alignCameraVerticallyOffset;

        public float AlignCameraVerticallyOffset {
            get { return _alignCameraVerticallyOffset; }
            set {
                if (Equals(value, _alignCameraVerticallyOffset)) return;
                _alignCameraVerticallyOffset = value;
                OnPropertyChanged();
                UpdateCamera();
            }
        }

        private bool _alignCameraVerticallyOffsetRelative;

        public bool AlignCameraVerticallyOffsetRelative {
            get { return _alignCameraVerticallyOffsetRelative; }
            set {
                if (Equals(value, _alignCameraVerticallyOffsetRelative)) return;
                _alignCameraVerticallyOffsetRelative = value;
                OnPropertyChanged();
                UpdateCamera();
            }
        }

        private void OnCameraMoved(object sender, EventArgs e) {
            if (_cameraIgnoreNext) {
                _cameraIgnoreNext = false;
            } else {
                if (LockCamera) {
                    UpdateCamera();
                } else {
                    SyncCamera();
                }
            }
        }

        private bool _cameraBusy, _cameraIgnoreNext;

        private void SyncCamera() {
            if (_cameraBusy) return;
            _cameraBusy = true;

            try {
                var offset = AlignCar ? -Renderer.MainSlot.CarCenter : Vector3.Zero;
                CameraPosition.Set(Renderer.Camera.Position + offset);
                CameraLookAt.Set((Renderer.CameraOrbit?.Target ?? Renderer.Camera.Position + Renderer.Camera.Look) + offset);
                CameraFov = Renderer.Camera.FovY.ToDegrees();
                AlignCameraHorizontally = false;
                AlignCameraVertically = false;
            } finally {
                _cameraBusy = false;
            }
        }

        private void UpdateCamera() {
            if (_cameraBusy) return;
            _cameraBusy = true;

            try {
                Renderer.SetCamera(CameraPosition.ToVector(), CameraLookAt.ToVector(), CameraFov.ToRadians());

                if (AlignCar) {
                    Renderer.AlignCar();
                }

                Renderer.AlignCamera(AlignCameraHorizontally, AlignCameraHorizontallyOffset, AlignCameraHorizontallyOffsetRelative,
                        AlignCameraVertically, AlignCameraVerticallyOffset, AlignCameraVerticallyOffsetRelative);

                _cameraIgnoreNext = true;
            } finally {
                _cameraBusy = false;
            }
        }

        private void ResetCamera() {
            LoadCamera((SaveableData)CreateSaveableData());
        }

        private DelegateCommand _resetCameraCommand;

        public DelegateCommand ResetCameraCommand => _resetCameraCommand ?? (_resetCameraCommand = new DelegateCommand(ResetCamera));
        #endregion

        #region Car params
        private AsyncCommand _changeCarCommand;

        public AsyncCommand ChangeCarCommand => _changeCarCommand ?? (_changeCarCommand = new AsyncCommand(() => {
            var currentCar = Renderer.CarNode?.CarId;
            var car = SelectCarDialog?.Invoke(currentCar == null ? null : CarsManager.Instance.GetById(currentCar));
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
            get { return _steerDeg; }
            set {
                if (Equals(value, _steerDeg)) return;
                _steerDeg = value;
                OnPropertyChanged();
                SetCarProperty(car => car.SteerDeg = value);
            }
        }

        private bool _headlightsEnabled;

        public bool HeadlightsEnabled {
            get { return _headlightsEnabled; }
            set {
                if (Equals(value, _headlightsEnabled)) return;
                _headlightsEnabled = value;
                OnPropertyChanged();
                SetCarProperty(car => car.HeadlightsEnabled = value);
            }
        }

        private bool _brakeLightsEnabled;

        public bool BrakeLightsEnabled {
            get { return _brakeLightsEnabled; }
            set {
                if (Equals(value, _brakeLightsEnabled)) return;
                _brakeLightsEnabled = value;
                OnPropertyChanged();
                SetCarProperty(car => car.BrakeLightsEnabled = value);
            }
        }

        private bool _leftDoorOpen;

        public bool LeftDoorOpen {
            get { return _leftDoorOpen; }
            set {
                if (Equals(value, _leftDoorOpen)) return;
                _leftDoorOpen = value;
                OnPropertyChanged();
                SetCarProperty(car => car.LeftDoorOpen = value);
            }
        }

        private bool _rightDoorOpen;

        public bool RightDoorOpen {
            get { return _rightDoorOpen; }
            set {
                if (Equals(value, _rightDoorOpen)) return;
                _rightDoorOpen = value;
                OnPropertyChanged();
                SetCarProperty(car => car.RightDoorOpen = value);
            }
        }

        private bool _showDriver;

        public bool ShowDriver {
            get { return _showDriver; }
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
                var relative = FileUtils.GetRelativePath(filename, PresetsManager.Instance.Combine(DarkRendererSettings.DefaultPresetableKeyValue));
                filename = relative == filename ? null : Path.Combine(PresetsManager.Instance.Combine(DefaultPresetableKeyValue), relative);
            }

            PresetsManager.Instance.SavePresetUsingDialog(null, DefaultPresetableKeyValue, SaveHelper<SaveableData>.Serialize(data), filename);
        }
    }

    public class Coordinates : NotifyPropertyChanged {
        private double _x;

        public double X {
            get { return _x; }
            set {
                if (Equals(value, _x)) return;
                _x = value;
                OnPropertyChanged();
            }
        }

        private double _y;

        public double Y {
            get { return _y; }
            set {
                if (Equals(value, _y)) return;
                _y = value;
                OnPropertyChanged();
            }
        }

        private double _z;

        public double Z {
            get { return _z; }
            set {
                if (Equals(value, _z)) return;
                _z = value;
                OnPropertyChanged();
            }
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