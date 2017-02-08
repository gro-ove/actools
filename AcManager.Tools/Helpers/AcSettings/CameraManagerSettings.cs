using System.Windows.Media;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class CameraManagerSettings : IniSettings {
        internal CameraManagerSettings() : base("camera_manager") { }

        private int _gForceX;

        public int GForceX {
            get { return _gForceX; }
            set {
                value = value.Clamp(0, 300);
                if (Equals(value, _gForceX)) return;
                _gForceX = value;
                OnPropertyChanged();
            }
        }

        private int _gForceY;

        public int GForceY {
            get { return _gForceY; }
            set {
                value = value.Clamp(0, 300);
                if (Equals(value, _gForceY)) return;
                _gForceY = value;
                OnPropertyChanged();
            }
        }

        private int _gForceZ;

        public int GForceZ {
            get { return _gForceZ; }
            set {
                value = value.Clamp(0, 300);
                if (Equals(value, _gForceZ)) return;
                _gForceZ = value;
                OnPropertyChanged();
            }
        }

        private int _highSpeedShaking;

        public int HighSpeedShaking {
            get { return _highSpeedShaking; }
            set {
                value = value.Clamp(0, 200);
                if (Equals(value, _highSpeedShaking)) return;
                _highSpeedShaking = value;
                OnPropertyChanged();
            }
        }

        private Color _fadeColor;

        public Color FadeColor {
            get { return _fadeColor; }
            set {
                if (Equals(value, _fadeColor)) return;
                _fadeColor = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            FadeColor = Ini["FADE"].GetColor("COLOR", Colors.Black);

            var shake = Ini["SHAKE"];
            GForceX = shake.GetDouble("GFORCEX", 1d).ToIntPercentage();
            GForceY = shake.GetDouble("GFORCEY", 1d).ToIntPercentage();
            GForceZ = shake.GetDouble("GFORCEZ", 1d).ToIntPercentage();
            HighSpeedShaking = shake.GetDouble("SHAKEMULT", 1d).ToIntPercentage();
        }

        protected override void SetToIni() {
            Ini["FADE"].Set("COLOR", FadeColor);

            var shake = Ini["SHAKE"];
            shake.Set("GFORCEX", GForceX.ToDoublePercentage());
            shake.Set("GFORCEY", GForceY.ToDoublePercentage());
            shake.Set("GFORCEZ", GForceZ.ToDoublePercentage());
            shake.Set("SHAKEMULT", HighSpeedShaking.ToDoublePercentage());
        }
    }
}