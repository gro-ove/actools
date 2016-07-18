using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools.Objects {
    public class WeatherObject : AcIniObject {
        public WeatherObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        private WeatherType _type;

        public WeatherType Type {
            get { return _type; }
            set { 
                if (Equals(_type, value)) return;
                _type = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private double _temperatureCoefficient;

        public double TemperatureCoefficient {
            get { return _temperatureCoefficient; }
            set { 
                if (Equals(_temperatureCoefficient, value)) return;
                _temperatureCoefficient = value;

                if (Loaded) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TemperatureCoefficientPercentage));
                    Changed = true;
                }
            }
        }

        public int TemperatureCoefficientPercentage {
            get { return TemperatureCoefficient.ToIntPercentage(); }
            set { TemperatureCoefficient = value.ToDoublePercentage(); }
        }

        private static WeatherType TryToDetectWeatherTypeById(string id) {
            var l = id.ToLower();

            if (l.Contains(@"mid_clear")) return WeatherType.FewClouds;
            if (l.Contains(@"light_clouds")) return WeatherType.ScatteredClouds;
            if (l.Contains(@"heavy_clouds")) return WeatherType.OvercastClouds;

            if (l.Contains(@"clouds")) return WeatherType.BrokenClouds;
            if (l.Contains(@"clear")) return WeatherType.Clear;
            
            if (l.Contains(@"light_fog")) return WeatherType.Mist;
            if (l.Contains(@"fog")) return WeatherType.Fog;

            return WeatherType.None;
        }

        protected override void InitializeLocations() {
            base.InitializeLocations();
            IniFilename = Path.Combine(Location, "weather.ini");
            ColorCurvesIniFilename = Path.Combine(Location, "colorCurves.ini");
        }

        public string ColorCurvesIniFilename { get; private set; }

        private IniFile _colorCurvesIniObject;

        public IniFile ColorCurvesIniObject {
            get { return _colorCurvesIniObject; }
            private set {
                if (Equals(_colorCurvesIniObject, value)) return;
                _colorCurvesIniObject = value;
            }
        }

        protected override void LoadData(IniFile ini) {
            Name = ini["LAUNCHER"].Get("NAME");
            TemperatureCoefficient = ini["LAUNCHER"].GetDouble("TEMPERATURE_COEFF", 0d);

            WeatherType? type;
            try {
                 type = ini["__LAUNCHER_CM"].GetEnumNullable<WeatherType>("WEATHER_TYPE");
            } catch (Exception) {
                type = null;
            }

            Type = type ?? TryToDetectWeatherTypeById(Id);

            if (_loadedExtended) {
                LoadExtended(ini);
            }
        }

        public override void SaveData(IniFile ini) {
            ini["LAUNCHER"].Set("NAME", Name);
            ini["LAUNCHER"].Set("TEMPERATURE_COEFF", TemperatureCoefficient);
            ini["__LAUNCHER_CM"].Set("WEATHER_TYPE", Type);

            if (_loadedExtended) {
                SaveExtended(ini);
            }
        }

        #region Extended parametes (needed in AC and loaded only for editing)
        private bool _forceCarLights;

        public bool ForceCarLights {
            get { return _forceCarLights; }
            set {
                if (Equals(value, _forceCarLights)) return;
                _forceCarLights = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _cloudsCover;

        public int CloudsCover {
            get { return _cloudsCover; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _cloudsCover)) return;
                _cloudsCover = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _cloudsCutoff;

        public int CloudsCutoff {
            get { return _cloudsCutoff; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _cloudsCutoff)) return;
                _cloudsCutoff = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _cloudsColor;

        public int CloudsColor {
            get { return _cloudsColor; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _cloudsColor)) return;
                _cloudsColor = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private double _cloudsWidth;

        public double CloudsWidth {
            get { return _cloudsWidth; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _cloudsWidth)) return;
                _cloudsWidth = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private double _cloudsHeight;

        public double CloudsHeight {
            get { return _cloudsHeight; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _cloudsHeight)) return;
                _cloudsHeight = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private double _cloudsRadius;

        public double CloudsRadius {
            get { return _cloudsRadius; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _cloudsRadius)) return;
                _cloudsRadius = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _cloudsNumber;

        public int CloudsNumber {
            get { return _cloudsNumber; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _cloudsNumber)) return;
                _cloudsNumber = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private double _cloudsSpeedMultipler;

        public double CloudsSpeedMultipler {
            get { return _cloudsSpeedMultipler; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _cloudsSpeedMultipler)) return;
                _cloudsSpeedMultipler = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CloudsSpeedMultiplerRounded));
                    Changed = true;
                }
            }
        }

        public double CloudsSpeedMultiplerRounded {
            get { return CloudsSpeedMultipler;  }
            set { CloudsSpeedMultipler = value.Round(0.01);  }
        }

        private Color _fogColor;

        public Color FogColor {
            get { return _fogColor; }
            set {
                if (Equals(value, _fogColor)) return;
                _fogColor = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _fogColorMultipler;

        public int FogColorMultipler {
            get { return _fogColorMultipler; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _fogColorMultipler)) return;
                _fogColorMultipler = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _fogBlend;

        public int FogBlend {
            get { return _fogBlend; }
            set {
                value = value.Clamp(0, 1000000);
                if (Equals(value, _fogBlend)) return;
                _fogBlend = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private double _fogDistance;

        public double FogDistance {
            get { return _fogDistance; }
            set {
                value = value.Clamp(0, 999999999);
                if (Equals(value, _fogDistance)) return;
                _fogDistance = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _loadedExtended;

        private void LoadExtended(IniFile ini) {
            var clouds = ini["CLOUDS"];
            CloudsCover = clouds.GetDouble("COVER", 0.9).ToIntPercentage();
            CloudsCutoff = clouds.GetDouble("CUTOFF", 0.5).ToIntPercentage();
            CloudsColor = clouds.GetDouble("COLOR", 0.7).ToIntPercentage();
            CloudsWidth = clouds.GetDouble("WIDTH", 9);
            CloudsHeight = clouds.GetDouble("HEIGHT", 4);
            CloudsRadius = clouds.GetDouble("RADIUS", 6);
            CloudsNumber = clouds.GetInt("NUMBER", 40);
            CloudsSpeedMultipler = clouds.GetDouble("BASE_SPEED_MULT", 0.0015) * 100d;

            var fog = ini["FOG"];
            var color = fog.GetStrings("COLOR").Select(x => FlexibleParser.TryParseDouble(x) ?? 1d).ToArray();
            if (color.Length == 3) {
                var maxValue = color.Max();
                if (Equals(maxValue, 0d)) {
                    FogColor = Colors.Black;
                    FogColorMultipler = 100;
                } else {
                    maxValue *= 1.2;
                    if (maxValue >= 0d && maxValue < 1d) {
                        maxValue = 1d;
                    } else if (maxValue < 0d && maxValue > -1d) {
                        maxValue = -1d;
                    }

                    FogColor = Color.FromRgb((255 * color[0] / maxValue).ClampToByte(),
                        (255 * color[1] / maxValue).ClampToByte(),
                        (255 * color[2] / maxValue).ClampToByte());
                    FogColorMultipler = maxValue.ToIntPercentage();
                }
            }

            FogBlend = fog.GetDouble("BLEND", 0.85).ToIntPercentage();
            FogDistance = fog.GetDouble("DISTANCE", 9000);

            ForceCarLights = ini["CAR_LIGHTS"].GetBool("FORCE_ON", false);
        }

        private void SaveExtended(IniFile ini) {
            ini["CAR_LIGHTS"].Set("FORCE_ON", ForceCarLights);
        }
        #endregion

        #region Color curves
        private bool _hasCurvesData;

        public bool HasCurvesData {
            get { return _hasCurvesData; }
            set {
                if (Equals(value, _hasCurvesData)) return;
                _hasCurvesData = value;
                OnPropertyChanged();
            }
        }

        private int _hdrOffMultipler;

        public int HdrOffMultipler {
            get { return _hdrOffMultipler; }
            set {
                if (Equals(value, _hdrOffMultipler)) return;
                _hdrOffMultipler = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _angleGamma;

        public int AngleGamma {
            get { return _angleGamma; }
            set {
                if (Equals(value, _angleGamma)) return;
                _angleGamma = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        public WeatherColorEntry[] ColorCurves { get; private set; }

        private void LoadColorCurvesData(IniFile ini) {
            var header = ini["HEADER"];
            HdrOffMultipler = header.GetDouble("HDR_OFF_MULT", 0.3).ToIntPercentage();
            AngleGamma = header.GetDouble("ANGLE_GAMMA", 3.4).ToIntPercentage();

            foreach (var entry in ColorCurves) {
                double multipler;
                entry.Color = ini[entry.Id].GetColor(entry.Sub, entry.DefaultColor, entry.DefaultMultipler, out multipler);
                entry.Multipler = multipler;
            }
        }

        private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (_loadedExtended) {
                OnPropertyChanged();
                Changed = true;
            }
        }
        #endregion

        private void ReloadColorCurves() {
            string text;
            try {
                text = FileUtils.ReadAllText(ColorCurvesIniFilename);
                RemoveError(AcErrorType.Weather_ColorCurvesIniIsMissing);
            } catch (FileNotFoundException) {
                AddError(AcErrorType.Weather_ColorCurvesIniIsMissing);
                HasCurvesData = false;
                return;
            } catch (DirectoryNotFoundException) {
                AddError(AcErrorType.Weather_ColorCurvesIniIsMissing);
                HasCurvesData = false;
                return;
            }

            ColorCurvesIniObject = IniFile.Parse(text);

            try {
                LoadColorCurvesData(ColorCurvesIniObject);
                HasCurvesData = true;
            } catch (Exception e) {
                Logging.Warning("[WeatherObject] LoadColorCurvesData(): " + e);
            }
        }

        public void EnsureLoadedExtended() {
            if (ColorCurves == null) {
                ColorCurves = new[] {
                    new WeatherColorEntry(@"HORIZON", @"LOW", Resources.Weather_ColorCurves_HorizonLow, Color.FromRgb(255, 138, 34), 1.9, 7d),
                    new WeatherColorEntry(@"HORIZON", @"HIGH", Resources.Weather_ColorCurves_HorizonHigh, Color.FromRgb(150, 170, 220), 3.5, 7d),
                    new WeatherColorEntry(@"SKY", @"LOW", Resources.Weather_ColorCurves_SkyLow, Color.FromRgb(30, 73, 167), 2.8, 5d),
                    new WeatherColorEntry(@"SKY", @"HIGH", Resources.Weather_ColorCurves_SkyHigh, Color.FromRgb(30, 73, 167), 3.0, 5d),
                    new WeatherColorEntry(@"SUN", @"LOW", Resources.Weather_ColorCurves_SunLow, Color.FromRgb(229, 140, 70), 40d, 50d),
                    new WeatherColorEntry(@"SUN", @"HIGH", Resources.Weather_ColorCurves_SunHigh, Color.FromRgb(170, 160, 140), 20d, 50d),
                    new WeatherColorEntry(@"AMBIENT", @"LOW", Resources.Weather_ColorCurves_AmbientLow, Color.FromRgb(124, 124, 124), 18d, 30d),
                    new WeatherColorEntry(@"AMBIENT", @"HIGH", Resources.Weather_ColorCurves_AmbientHigh, Color.FromRgb(105, 105, 105), 11d, 30d),
                };

                foreach (var entry in ColorCurves) {
                    entry.PropertyChanged += Entry_PropertyChanged;
                }
            }

            if (_loadedExtended || IniObject == null) return;

            var changed = Changed;
            try {
                LoadExtended(IniObject);
            } catch (Exception e) {
                Logging.Warning("[WeatherObject] LoadExtended(): " + e);
            }

            try {
                ReloadColorCurves();
            } finally {
                Changed = changed;
                _loadedExtended = true;
            }
        }

        public override bool HandleChangedFile(string filename) {
            if (!FileUtils.IsAffected(filename, ColorCurvesIniFilename) || !_loadedExtended) return base.HandleChangedFile(filename);

            if (!Changed ||
                    ModernDialog.ShowMessage(Resources.AcObject_ReloadAutomatically_Ini, Resources.AcObject_ReloadAutomatically, MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                var c = Changed;
                ReloadColorCurves();
                Changed = c;
            }

            return true;
        }

        public override int CompareTo(AcPlaceholderNew o) {
            return Enabled == o.Enabled ?
                    AlphanumComparatorFast.Compare(Id, o.Id) : Enabled ? -1 : 1;
        }
    }
}
