using System.ComponentModel;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettings {
    public class CameraChaseSettings : IniSettings {
        internal CameraChaseSettings() : base("chase_cam", systemConfig: true) {
            First.PropertyChanged += OnCameraChanged;
            Second.PropertyChanged += OnCameraChanged;
        }

        private void OnCameraChanged(object sender, PropertyChangedEventArgs args) {
            Save();
        }

        public class Camera : Displayable {
            private double _distance;

            public double Distance {
                get => _distance;
                set => Apply(value, ref _distance);
            }

            private double _height;

            public double Height {
                get => _height;
                set => Apply(value, ref _height);
            }

            private double _pitch;

            public double Pitch {
                get => _pitch;
                set => Apply(value, ref _pitch, () => {
                    OnPropertyChanged(nameof(PitchDeg));
                });
            }

            public double PitchDeg {
                get => _pitch.ToDegrees().Round(0.01);
                set => Pitch = value.ToRadians();
            }

            public void Load([NotNull] IniFileSection section) {
                Distance = section.GetDouble("DISTANCE", 3d);
                Height = section.GetDouble("HEIGHT", 1.4);
                Pitch = section.GetDouble("PITCH", 0.035);
            }

            public void Save([NotNull] IniFileSection section) {
                section.Set("DISTANCE", Distance);
                section.Set("HEIGHT", Height);
                section.Set("PITCH", Pitch);
            }
        }

        public Camera First { get; } = new Camera { DisplayName = "First camera" };
        public Camera Second { get; } = new Camera { DisplayName = "Second camera" };

        protected override void LoadFromIni() {
            First.Load(Ini["CHASE_0"]);
            Second.Load(Ini["CHASE_1"]);
        }

        protected override void SetToIni() {
            First.Save(Ini["CHASE_0"]);
            Second.Save(Ini["CHASE_1"]);
        }
    }
}