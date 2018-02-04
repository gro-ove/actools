using System;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.CustomShowroom {
    public class AmbientShadowParams : NotifyPropertyChanged, IUserPresetable {
        private const string KeyOldSavedData = "__LiteShowroomTools";
        private const string KeySavedData = "__LiteShowroomTools_AS";

        private readonly ISaveHelper _saveable;

        private void SaveLater() {
            _saveable.SaveLater();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public AmbientShadowParams() {
            if (ValuesStorage.Contains(KeyOldSavedData) && !ValuesStorage.Contains(KeySavedData)) {
                ValuesStorage.Set(KeySavedData, ValuesStorage.Get<string>(KeyOldSavedData));
            }

            _saveable = new SaveHelper<SaveableData>(KeySavedData, () => new SaveableData {
                AmbientShadowDiffusion = Diffusion,
                AmbientShadowBrightness = Brightness,
                AmbientShadowIterations = Iterations,
                AmbientShadowHideWheels = HideWheels,
                AmbientShadowFade = Fade,
                AmbientShadowCorrectLighting = CorrectLighting,
                AmbientShadowPoissonSampling = PoissonSampling,
                AmbientShadowExtraBlur = ExtraBlur,
                AmbientShadowUpDelta = UpDelta,
                AmbientShadowBodyMultiplier = BodyMultiplier,
                AmbientShadowWheelMultiplier = WheelMultiplier,
            }, Load);

            _saveable.Initialize();
        }

        public class SaveableData {
            public double AmbientShadowDiffusion = 60d;

            [JsonProperty("asb")]
            public double AmbientShadowBrightness = 200d;

            public int AmbientShadowIterations = 8000;
            public bool AmbientShadowHideWheels;
            public bool AmbientShadowFade = true;

            [JsonProperty("asa")]
            public bool AmbientShadowCorrectLighting = true;

            [JsonProperty("asp")]
            public bool AmbientShadowPoissonSampling = true;

            [JsonProperty("asu")]
            public bool AmbientShadowExtraBlur;

            [JsonProperty("asd")]
            public float AmbientShadowUpDelta = 0.05f;

            [JsonProperty("asbm")]
            public float AmbientShadowBodyMultiplier = 0.75f;

            [JsonProperty("aswm")]
            public float AmbientShadowWheelMultiplier = 0.35f;
        }

        /*public static SaveableData LoadAmbientShadowParams() {
            if (!ValuesStorage.Contains(KeySavedData)) {
                return new SaveableData();
            }

            try {
                return JsonConvert.DeserializeObject<SaveableData>(ValuesStorage.GetString(KeySavedData));
            } catch (Exception e) {
                Logging.Warning(e);
                return new SaveableData();
            }
        }*/

        private void Load(SaveableData o) {
            Diffusion = o.AmbientShadowDiffusion;
            Brightness = o.AmbientShadowBrightness;
            Iterations = o.AmbientShadowIterations;
            HideWheels = o.AmbientShadowHideWheels;
            Fade = o.AmbientShadowFade;
            CorrectLighting = o.AmbientShadowCorrectLighting;
            PoissonSampling = o.AmbientShadowPoissonSampling;
            ExtraBlur = o.AmbientShadowExtraBlur;
            UpDelta = o.AmbientShadowUpDelta;
            BodyMultiplier = o.AmbientShadowBodyMultiplier;
            WheelMultiplier = o.AmbientShadowWheelMultiplier;
        }

        private double _diffusion;

        public double Diffusion {
            get => _diffusion;
            set {
                value = value.Clamp(0.0, 100.0);
                if (Equals(value, _diffusion)) return;
                _diffusion = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double _brightness;

        public double Brightness {
            get => _brightness;
            set {
                value = value.Clamp(150.0, 800.0);
                if (Equals(value, _brightness)) return;
                _brightness = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private int _iterations;

        public int Iterations {
            get => _iterations;
            set {
                value = value.Clamp(10, 40000);
                if (Equals(value, _iterations)) return;
                _iterations = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _hideWheels;

        public bool HideWheels {
            get => _hideWheels;
            set {
                if (Equals(value, _hideWheels)) return;
                _hideWheels = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _fade;

        public bool Fade {
            get => _fade;
            set {
                if (Equals(value, _fade)) return;
                _fade = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _correctLighting;

        public bool CorrectLighting {
            get => _correctLighting;
            set {
                if (Equals(value, _correctLighting)) return;
                _correctLighting = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _poissonSampling;

        public bool PoissonSampling {
            get => _poissonSampling;
            set {
                if (Equals(value, _poissonSampling)) return;
                _poissonSampling = value;
                OnPropertyChanged();
            }
        }

        private bool _extraBlur;

        public bool ExtraBlur {
            get => _extraBlur;
            set {
                if (Equals(value, _extraBlur)) return;
                _extraBlur = value;
                OnPropertyChanged();
            }
        }

        private float _upDelta = 0.05f;

        public float UpDelta {
            get => _upDelta;
            set {
                if (Equals(value, _upDelta)) return;
                _upDelta = value;
                OnPropertyChanged();
            }
        }

        private float _bodyMultiplier = 0.8f;

        public float BodyMultiplier {
            get => _bodyMultiplier;
            set {
                if (Equals(value, _bodyMultiplier)) return;
                _bodyMultiplier = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private float _wheelMultiplier = 0.69f;

        public float WheelMultiplier {
            get => _wheelMultiplier;
            set {
                if (Equals(value, _wheelMultiplier)) return;
                _wheelMultiplier = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        /*private void Reset(bool saveLater) {
            Load(new SaveableData());
            if (saveLater) {
                SaveLater();
            }
        }

        private DelegateCommand _resetCommand;

        public DelegateCommand ResetCommand => _resetCommand ?? (_resetCommand = new DelegateCommand(() => {
            Reset(true);
        }));*/

        #region Presets
        public bool CanBeSaved => true;
        public string PresetableKey => KeySavedData;
        public PresetsCategory PresetableCategory => new PresetsCategory("Ambient Shadows");

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            _saveable.FromSerializedString(data);
        }

        public string ExportToPresetData() {
            return _saveable.ToSerializedString();
        }
        #endregion

        /*#region Share
        private ICommand _shareCommand;

        public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

        protected async Task Share() {
            var data = ExportToPresetData();
            if (data == null) return;
            await SharingUiHelper.ShareAsync(SharedEntryType.CustomShowroomPreset,
                    Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(KeySavedData)), null,
                    data);
        }
        #endregion*/
    }
}