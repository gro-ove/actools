using System;
using System.Linq;
using AcTools.Utils;
using AcTools.Windows;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class ExposureSettings : IniSettings {
            internal ExposureSettings() : base(@"exposure") {}

            private int _value;

            public int Value {
                get { return _value; }
                set {
                    value = value.Clamp(-100, 300);
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                Value = Ini["EXPOSURE"].GetDouble("VALUE", 1d).ToIntPercentage();
            }

            protected override void SetToIni() {
                Ini["EXPOSURE"].Set("VALUE", Value.ToDoublePercentage());
            }
        }

        private static ExposureSettings _exposure;

        public static ExposureSettings Exposure => _exposure ?? (_exposure = new ExposureSettings());

        public class ReplaySettings : IniSettings {
            public SettingEntry[] Qualities { get; } = {
                new SettingEntry(0, Resources.AcSettings_Quality_Minimum),
                new SettingEntry(1, Resources.AcSettings_Quality_Low),
                new SettingEntry(2, Resources.AcSettings_Quality_Medium),
                new SettingEntry(3, Resources.AcSettings_Quality_High),
                new SettingEntry(4, Resources.AcSettings_Quality_Ultra)
            };

            internal ReplaySettings() : base(@"replay") { }

            private int _maxSize;

            public int MaxSize {
                get { return _maxSize; }
                set {
                    value = value.Clamp(10, 2000);
                    if (Equals(value, _maxSize)) return;
                    _maxSize = value;
                    OnPropertyChanged();
                }
            }

            private static int? _recommendedSize;

            public int? RecommendedSize {
                get {
                    if (_recommendedSize != null) return _recommendedSize;

                    var memStatus = new Kernel32.MEMORYSTATUSEX();
                    if (!Kernel32.GlobalMemoryStatusEx(memStatus)) return null;

                    var installedMemory = memStatus.ullTotalPhys;
                    _recommendedSize = Math.Min((int)(0.1 * installedMemory / 1024 / 1024), 1000);
                    return _recommendedSize;
                }
            }

            private SettingEntry _quality;

            public SettingEntry Quality {
                get { return _quality; }
                set {
                    if (!Qualities.Contains(value)) value = Qualities[0];
                    if (Equals(value, _quality)) return;
                    _quality = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                MaxSize = Ini["REPLAY"].GetInt("MAX_SIZE_MB", 200);
                Quality = Ini["QUALITY"].GetEntry("LEVEL", Qualities, 3);
            }

            protected override void SetToIni() {
                Ini["REPLAY"].Set("MAX_SIZE_MB", MaxSize);
                Ini["QUALITY"].Set("LEVEL", Quality);
            }
        }

        private static ReplaySettings _replay;

        public static ReplaySettings Replay => _replay ?? (_replay = new ReplaySettings());
    }
}
