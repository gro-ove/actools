using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SlimDX;

namespace AcManager.Controls.CustomShowroom {
    public class DarkRendererSettings : NotifyPropertyChanged {
        [NotNull]
        public DarkKn5ObjectRenderer Renderer { get; }

        public static SettingEntry[] MsaaModes { get; } = {
            new SettingEntry(0, ToolsStrings.Common_Disabled),
            new SettingEntry(2, @"2xMSAA"),
            new SettingEntry(4, @"4xMSAA"),
            new SettingEntry(8, @"8xMSAA"),
        };

        public static SettingEntry[] SsaaModes { get; } = {
            new SettingEntry(1, ToolsStrings.Common_Disabled),
            new SettingEntry(2, @"2xSSAA"),
            new SettingEntry(4, @"4xSSAA"),
            new SettingEntry(8, @"8xSSAA"),
        };

        public static SettingEntry[] ShadowResolutions { get; } = {
            new SettingEntry(1024, "1024×1024"),
            new SettingEntry(2048, "2048×2048"),
            new SettingEntry(4096, "4096×4096"),
            new SettingEntry(8192, "8192×8192")
        };

        public DarkRendererSettings(DarkKn5ObjectRenderer renderer) {
            Renderer = renderer;

            UpdateColors();
            SyncAll();

            renderer.PropertyChanged += OnRendererPropertyChanged;
        }

        private void OnRendererPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(Renderer.MsaaSampleCount):
                case nameof(Renderer.UseMsaa):
                    SyncMsaaMode();
                    break;

                case nameof(Renderer.ResolutionMultiplier):
                    SyncSsaaMode();
                    break;
                    
                case nameof(Renderer.ShadowMapSize):
                    SyncShadowMapSize();
                    break;
                    
                case nameof(Renderer.Light):
                    SyncLight();
                    break;
            }
        }

        private void SyncAll() {
            SyncMsaaMode();
            SyncSsaaMode();
            SyncShadowMapSize();
            SyncLight();
        }

        private void SyncMsaaMode() {
            _msaaMode = Renderer?.UseMsaa != true ? MsaaModes[0] : MsaaModes.GetByIdOrDefault(Renderer?.MsaaSampleCount);
            OnPropertyChanged(nameof(MsaaMode));
        }

        private void SyncSsaaMode() {
            _ssaaMode = SsaaModes.GetByIdOrDefault<SettingEntry, int?>(Math.Pow(Renderer?.ResolutionMultiplier ?? 1d, 2d).RoundToInt());
            OnPropertyChanged(nameof(SsaaMode));
        }

        private void SyncShadowMapSize() {
            _shadowMapSize = ShadowResolutions.GetByIdOrDefault<SettingEntry, int?>(Renderer?.ShadowMapSize ?? 2048);
            OnPropertyChanged(nameof(ShadowMapSize));
        }

        private void SyncLight() {
            var v = Renderer.Light;
            _lightθ = v.Y.Acos();
            _lightφ = v.X == 0f && v.Z == 0f ? 0f : MathF.AngleFromXY(v.X, v.Z);
            OnPropertyChanged(nameof(Lightθ));
            OnPropertyChanged(nameof(Lightφ));
        }

        private SettingEntry _msaaMode = MsaaModes[0];

        public SettingEntry MsaaMode {
            get { return _msaaMode; }
            set {
                if (!MsaaModes.Contains(value)) value = MsaaModes[0];
                if (Equals(value, _msaaMode)) return;
                _msaaMode = value;
                OnPropertyChanged();

                Renderer.MsaaSampleCount = value.IntValue > 0 ? value.IntValue ?? 4 : 4;
                Renderer.UseMsaa = value.IntValue > 0;
            }
        }

        private SettingEntry _ssaaMode = SsaaModes[0];

        public SettingEntry SsaaMode {
            get { return _ssaaMode; }
            set {
                if (!SsaaModes.Contains(value)) value = SsaaModes[0];
                if (Equals(value, _ssaaMode)) return;
                _ssaaMode = value;
                OnPropertyChanged();

                Renderer.ResolutionMultiplier = Math.Sqrt(value.IntValue ?? 1);
            }
        }

        private SettingEntry _shadowMapSize = ShadowResolutions[1];

        public SettingEntry ShadowMapSize {
            get { return _shadowMapSize; }
            set {
                if (Equals(value, _shadowMapSize)) return;
                _shadowMapSize = value;
                OnPropertyChanged();

                Renderer.ShadowMapSize = value.IntValue ?? 2048;
            }
        }

        private void SetLight() {
            var sinθ = MathF.Sin(_lightθ);
            var cosθ = MathF.Cos(_lightθ);
            var sinφ = MathF.Sin(_lightφ);
            var cosφ = MathF.Cos(_lightφ);
            Renderer.Light = new Vector3(sinθ * cosφ, cosθ, sinθ * sinφ);
        }

        private float _lightθ;

        public float Lightθ {
            get { return 90f - _lightθ.ToDegrees(); }
            set {
                value = (90f - value).ToRadians();
                if (Equals(value, _lightθ)) return;
                _lightθ = value;
                OnPropertyChanged();
                SetLight();
            }
        }

        private float _lightφ;

        public float Lightφ {
            get { return _lightφ.ToDegrees(); }
            set {
                value = value.ToRadians();
                if (Equals(value, _lightφ)) return;
                _lightφ = value;
                OnPropertyChanged();
                SetLight();
            }
        }

        private ShowroomObject _showroom;

        public ShowroomObject Showroom {
            get { return _showroom; }
            set {
                if (Equals(value, _showroom)) return;
                _showroom = value;
                OnPropertyChanged();
            }
        }

        #region Visual params, colors
        private void UpdateColors() {
            if (BackgroundColor != Colors.Transparent) {
                Renderer.BackgroundColor = BackgroundColor.ToColor();
            } else {
                OnPropertyChanged(nameof(BackgroundColor));
            }

            if (LightColor != Colors.Transparent) {
                Renderer.LightColor = LightColor.ToColor();
            } else {
                OnPropertyChanged(nameof(LightColor));
            }

            if (AmbientDownColor != Colors.Transparent) {
                Renderer.AmbientDown = AmbientDownColor.ToColor();
            } else {
                OnPropertyChanged(nameof(AmbientDownColor));
            }

            if (AmbientUpColor != Colors.Transparent) {
                Renderer.AmbientUp = AmbientUpColor.ToColor();
            } else {
                OnPropertyChanged(nameof(AmbientUpColor));
            }
        }

        public Color BackgroundColor {
            get { return Renderer?.BackgroundColor.ToColor() ?? Colors.Transparent; }
            set {
                if (Equals(value, BackgroundColor)) return;
                if (Renderer != null) {
                    Renderer.BackgroundColor = value.ToColor();
                    OnPropertyChanged();
                }
            }
        }

        public Color LightColor {
            get { return Renderer?.LightColor.ToColor() ?? Colors.Transparent; }
            set {
                if (Equals(value, LightColor)) return;
                if (Renderer != null) {
                    Renderer.LightColor = value.ToColor();
                    OnPropertyChanged();
                }
            }
        }

        public Color AmbientDownColor {
            get { return Renderer?.AmbientDown.ToColor() ?? Colors.Transparent; }
            set {
                if (Equals(value, AmbientDownColor)) return;
                if (Renderer != null) {
                    Renderer.AmbientDown = value.ToColor();
                    OnPropertyChanged();
                }
            }
        }

        public Color AmbientUpColor {
            get { return Renderer?.AmbientUp.ToColor() ?? Colors.Transparent; }
            set {
                if (Equals(value, AmbientUpColor)) return;
                if (Renderer != null) {
                    Renderer.AmbientUp = value.ToColor();
                    OnPropertyChanged();
                }
            }
        }
        #endregion
    }
}