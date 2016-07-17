using System.Linq;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class CameraOnboardSettings : IniSettings {
            internal CameraOnboardSettings() : base(@"camera_onboard") { }

            private int _fieldOfView;

            public int FieldOfView {
                get { return _fieldOfView; }
                set {
                    value = value.Clamp(10, 120);
                    if (Equals(value, _fieldOfView)) return;
                    _fieldOfView = value;
                    OnPropertyChanged();
                }
            }

            private bool _worldAligned;

            public bool WorldAligned {
                get { return _worldAligned; }
                set {
                    if (Equals(value, _worldAligned)) return;
                    _worldAligned = value;
                    OnPropertyChanged();
                }
            }

            private int _glancingSpeed;

            public int GlancingSpeed {
                get { return _glancingSpeed; }
                set {
                    value = value.Clamp(1, 40);
                    if (Equals(value, _glancingSpeed)) return;
                    _glancingSpeed = value;
                    OnPropertyChanged();
                }
            }

            private int _glancingAngle;

            public int GlancingAngle {
                get { return _glancingAngle; }
                set {
                    value = value.Clamp(5, 90);
                    if (Equals(value, _glancingAngle)) return;
                    _glancingAngle = value;
                    OnPropertyChanged();
                }
            }

            public bool GForcesBinded {
                get { return ValuesStorage.GetBool("AcSettings.CameraOnboard.GForceBinded", true); }
                set {
                    if (Equals(value, GForcesBinded)) return;
                    ValuesStorage.Set("AcSettings.CameraOnboard.GForceBinded", value);
                    OnPropertyChanged(false);

                    if (value) {
                        GForceY = GForceX;
                        GForceZ = GForceX;
                    }
                }
            }

            private int _gForceX;

            public int GForceX {
                get { return _gForceX; }
                set {
                    value = value.Clamp(0, 300);
                    if (Equals(value, _gForceX)) return;
                    _gForceX = value;
                    OnPropertyChanged();

                    if (GForcesBinded) {
                        GForceY = GForceX;
                        GForceZ = GForceX;
                    }
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

            private static readonly double[] DefaultShaking = { 0.001, 0.0002, 0.00015 };
            private static readonly double[] DefaultGForces = { -0.002, -0.0020, -0.0025 };

            protected override void LoadFromIni() {
                FieldOfView = Ini["MODE"].GetInt("FOV", 56);
                WorldAligned = Ini["MODE"].GetBool("IS_WORLD_ALIGNED", false);
                GlancingSpeed = Ini["ROTATION"].GetInt("SPEED", 20);
                GlancingAngle = Ini["ROTATION"].GetInt("HEAD_MAX_DEGREES", 60);

                var shaking = Ini["SHAKE"].GetVector3("SCALE");
                HighSpeedShaking = (int)(100 * shaking[0] / DefaultShaking[0]);

                var gForces = Ini["GFORCES"].GetVector3("MIX");
                var gForceX = (int)(100 * gForces[0] / DefaultGForces[0]);
                var gForceY = (int)(100 * gForces[1] / DefaultGForces[1]);
                var gForceZ = (int)(100 * gForces[2] / DefaultGForces[2]);

                if (gForceX != gForceY || gForceX != gForceZ) {
                    GForcesBinded = false;
                }

                GForceX = gForceX;
                GForceY = gForceY;
                GForceZ = gForceZ;
            }

            protected override void SetToIni() {
                Ini["MODE"].Set("FOV", FieldOfView);
                Ini["MODE"].Set("IS_WORLD_ALIGNED", WorldAligned);
                Ini["ROTATION"].Set("SPEED", GlancingSpeed);
                Ini["ROTATION"].Set("HEAD_MAX_DEGREES", GlancingAngle);

                Ini["SHAKE"].Set("SCALE", DefaultShaking.Select(x => x * HighSpeedShaking / 100));
                Ini["GFORCES"].Set("MIX", GForcesBinded ? DefaultGForces.Select(x => x * GForceX / 100)
                        : new[] {
                            DefaultGForces[0] * GForceX / 100,
                            DefaultGForces[1] * GForceY / 100,
                            DefaultGForces[2] * GForceZ / 100
                        });
            }
        }

        private static CameraOnboardSettings _cameraOnboard;

        public static CameraOnboardSettings CameraOnboard => _cameraOnboard ?? (_cameraOnboard = new CameraOnboardSettings());
    }
}
