using System;
using AcTools.Utils;
using AcTools.Windows;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class ExposureSettings : IniSettings {
            internal ExposureSettings() : base("exposure") {}

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
            public enum ReplayQuality {
                Minimum = 0,
                Low = 1,
                Medium = 2,
                High = 3,
                Ultra = 4
            }

            public ReplayQuality[] Qualities { get; } = {
                ReplayQuality.Minimum,
                ReplayQuality.Low,
                ReplayQuality.Medium,
                ReplayQuality.High,
                ReplayQuality.Ultra
            };

            internal ReplaySettings() : base("replay") { }

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

            private ReplayQuality _quality;

            public ReplayQuality Quality {
                get { return _quality; }
                set {
                    if (Equals(value, _quality)) return;
                    _quality = value;
                    OnPropertyChanged();
                }
            }

            protected override void LoadFromIni() {
                MaxSize = Ini["REPLAY"].GetInt("MAX_SIZE_MB", 200);
                Quality = Ini["QUALITY"].GetEnum("LEVEL", ReplayQuality.High);
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
