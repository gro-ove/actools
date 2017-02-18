using System;
using System.ComponentModel;
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
using JetBrains.Annotations;

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

        private string _timeDiapason;

        [CanBeNull]
        public string TimeDiapason {
            get { return _timeDiapason; }
            set {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                if (Equals(value, _timeDiapason)) return;
                _timeDiapason = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _temperatureDiapason;

        [CanBeNull]
        public string TemperatureDiapason {
            get { return _temperatureDiapason; }
            set {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                if (Equals(value, _temperatureDiapason)) return;
                _temperatureDiapason = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _disableShadows;

        public bool DisableShadows {
            get { return _disableShadows; }
            set {
                if (Equals(value, _disableShadows)) return;
                _disableShadows = value;
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
                    Changed = true;
                }
            }
        }

        private static WeatherType TryToDetectWeatherTypeById(string id) {
            var l = id.ToLower();

            if (l.Contains(@"fog")) {
                if (l.Contains(@"light")) return WeatherType.Mist;
                return WeatherType.Fog;
            }

            if (l.Contains(@"drizzle")) {
                if (l.Contains(@"light")) return WeatherType.LightDrizzle;
                if (l.Contains(@"heavy")) return WeatherType.HeavyDrizzle;
                return WeatherType.Drizzle;
            }

            if (l.Contains(@"rain")) {
                if (l.Contains(@"light")) return WeatherType.LightRain;
                if (l.Contains(@"heavy")) return WeatherType.HeavyRain;
                return WeatherType.Rain;
            }

            if (l.Contains(@"snow")) {
                if (l.Contains(@"light")) return WeatherType.LightSnow;
                if (l.Contains(@"heavy")) return WeatherType.HeavySnow;
                return WeatherType.Snow;
            }

            if (l.Contains(@"clouds")) {
                if (l.Contains(@"light")) return WeatherType.ScatteredClouds;
                if (l.Contains(@"heavy")) return WeatherType.OvercastClouds;
                if (l.Contains(@"mid") || l.Contains(@"few")) return WeatherType.FewClouds;
                return WeatherType.BrokenClouds;
            }

            if (l.Contains(@"few")) return WeatherType.FewClouds;
            if (l.Contains(@"overcast")) return WeatherType.OvercastClouds;
            if (l.Contains(@"scattered")) return WeatherType.ScatteredClouds;
            if (l.Contains(@"cold")) return WeatherType.Cold;
            if (l.Contains(@"hot")) return WeatherType.Hot;

            if (l.Contains(@"clear")) {
                if (l.Contains(@"mid")) return WeatherType.FewClouds;
                return WeatherType.Clear;
            }
            
            return WeatherType.None;
        }

        protected override void InitializeLocations() {
            base.InitializeLocations();
            IniFilename = Path.Combine(Location, "weather.ini");
            PreviewImage = Path.Combine(Location, "preview.jpg");
            ColorCurvesIniFilename = Path.Combine(Location, "colorCurves.ini");
        }

        public string PreviewImage { get; private set; }

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
            Name = ini["LAUNCHER"].GetPossiblyEmpty("NAME");
            TemperatureCoefficient = ini["LAUNCHER"].GetDouble("TEMPERATURE_COEFF", 0d);

            WeatherType? type;
            try {
                 type = ini["__LAUNCHER_CM"].GetEnumNullable<WeatherType>("WEATHER_TYPE");
            } catch (Exception) {
                 type = null;
            }

            TemperatureDiapason = ini["__LAUNCHER_CM"].GetNonEmpty("TEMPERATURE_DIAPASON");
            TimeDiapason = ini["__LAUNCHER_CM"].GetNonEmpty("TIME_DIAPASON");
            DisableShadows = ini["__LAUNCHER_CM"].GetBool("DISABLE_SHADOWS", false);

            Type = type ?? TryToDetectWeatherTypeById(Id);

            if (_loadedExtended) {
                LoadExtended(ini);
            }
        }

        public override void SaveData(IniFile ini) {
            ini["LAUNCHER"].Set("NAME", Name);
            ini["LAUNCHER"].Set("TEMPERATURE_COEFF", TemperatureCoefficient);

            ini["__LAUNCHER_CM"].Set("WEATHER_TYPE", Type);
            ini["__LAUNCHER_CM"].SetOrRemove("TEMPERATURE_DIAPASON", TemperatureDiapason);
            ini["__LAUNCHER_CM"].SetOrRemove("TIME_DIAPASON", TimeDiapason);
            ini["__LAUNCHER_CM"].SetOrRemove("DISABLE_SHADOWS", DisableShadows);

            if (_loadedExtended) {
                SaveExtended(ini);
                SaveColorCurves();
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

        private double _cloudsCover;

        public double CloudsCover {
            get { return _cloudsCover; }
            set {
                if (Equals(value, _cloudsCover)) return;
                _cloudsCover = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private double _cloudsCutoff;

        public double CloudsCutoff {
            get { return _cloudsCutoff; }
            set {
                if (Equals(value, _cloudsCutoff)) return;
                _cloudsCutoff = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private double _cloudsColor;

        public double CloudsColor {
            get { return _cloudsColor; }
            set {
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

        private double _fogColorMultipler;

        public double FogColorMultipler {
            get { return _fogColorMultipler; }
            set {
                if (Equals(value, _fogColorMultipler)) return;
                _fogColorMultipler = value;
                if (_loadedExtended) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private double _fogBlend;

        public double FogBlend {
            get { return _fogBlend; }
            set {
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
            CloudsCover = clouds.GetDouble("COVER", 0.9);
            CloudsCutoff = clouds.GetDouble("CUTOFF", 0.5);
            CloudsColor = clouds.GetDouble("COLOR", 0.7);
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
                    FogColorMultipler = maxValue;
                }
            }

            FogBlend = fog.GetDouble("BLEND", 0.85);
            FogDistance = fog.GetDouble("DISTANCE", 9000);

            ForceCarLights = ini["CAR_LIGHTS"].GetBool("FORCE_ON", false);
        }

        [Localizable(false)]
        private static readonly IniCommentariesScheme IniCommentaries = new IniCommentariesScheme {
            ["CLOUDS"] = {
                ["COVER"] = "regulates clouds transparency: 0-1",
                ["CUTOFF"] =
                    "regulates other colors influencies on the cloud: 0-1; if 1, just the color of the cloud is considered; if 0, just the color effects on the clouds are considered; if 0.5, 50% of cloud color and 50% of effects",
                ["COLOR"] = "base color, the sunlight, light color and ambient will be added to this: 0-1",
                ["WIDTH"] = "width of the quad",
                ["HEIGHT"] = "height of the quad",
                ["RADIUS"] = "distance from the center of the skybox to the quad",
                ["NUMBER"] = "number of clouds",
            },
            ["LAUNCHER"] = {
                ["NAME"] = "name of the weather as it will appear in the launcher menus",
                ["TEMPERATURE_COEFF"] =
                    "creates a variation of the asphalt temperature relative to the weather, ambient temperature and time: −1-1; see the readme_weather.txt for explanation",
            }
        };

        private void SaveExtended(IniFile ini) {
            ini.SetCommentaries(IniCommentaries);

            var clouds = ini["CLOUDS"];
            clouds.Set("COVER", CloudsCover);
            clouds.Set("CUTOFF", CloudsCutoff);
            clouds.Set("COLOR", CloudsColor);
            clouds.Set("WIDTH", CloudsWidth);
            clouds.Set("HEIGHT", CloudsHeight);
            clouds.Set("RADIUS", CloudsRadius);
            clouds.Set("NUMBER", CloudsNumber);
            clouds.Set("BASE_SPEED_MULT", CloudsSpeedMultipler * 0.01);

            var fog = ini["FOG"];
            fog.Set("COLOR", new [] {
                FogColor.R, FogColor.G, FogColor.B
            }.Select(x => (x * FogColorMultipler / 255d).Round(0.001)));
            fog.Set("BLEND", FogBlend);
            fog.Set("DISTANCE", FogDistance);

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

        private double _hdrOffMultipler;

        public double HdrOffMultipler {
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

        private double _angleGamma;

        public double AngleGamma {
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
            HdrOffMultipler = header.GetDouble("HDR_OFF_MULT", 0.3);
            AngleGamma = header.GetDouble("ANGLE_GAMMA", 3.4);

            foreach (var entry in ColorCurves) {
                double multipler;
                entry.Color = ini[entry.Id].GetColor(entry.Sub, entry.DefaultColor, entry.DefaultMultipler, out multipler);
                entry.Multipler = multipler;
            }
        }

        private void SaveColorCurvesData(IniFile ini) {
            var header = ini["HEADER"];
            header.Set("VERSION", 3);
            header.Set("HDR_OFF_MULT", HdrOffMultipler);
            header.Set("ANGLE_GAMMA", AngleGamma);

            foreach (var entry in ColorCurves) {
                ini[entry.Id].Set(entry.Sub, new[] {
                    entry.Color.R, entry.Color.G, entry.Color.B, entry.Multipler
                });
            }
        }

        private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (_loadedExtended) {
                OnPropertyChanged();
                Changed = true;
            }
        }

        private void SaveColorCurves() {
            var ini = ColorCurvesIniObject ?? new IniFile();
            SaveColorCurvesData(ini);

            using ((FileAcManager as IIgnorer)?.IgnoreChanges()) {
                File.WriteAllText(ColorCurvesIniFilename, ini.ToString());
            }

            Changed = false;
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
                Logging.Warning("LoadColorCurvesData(): " + e);
            }
        }

        public void EnsureLoadedExtended() {
            if (ColorCurves == null) {
                ColorCurves = new[] {
                    new WeatherColorEntry(@"HORIZON", @"LOW", ToolsStrings.Weather_ColorCurves_HorizonLow, Color.FromRgb(255, 138, 34), 1.9, 7d),
                    new WeatherColorEntry(@"HORIZON", @"HIGH", ToolsStrings.Weather_ColorCurves_HorizonHigh, Color.FromRgb(150, 170, 220), 3.5, 7d),
                    new WeatherColorEntry(@"SKY", @"LOW", ToolsStrings.Weather_ColorCurves_SkyLow, Color.FromRgb(30, 73, 167), 2.8, 5d),
                    new WeatherColorEntry(@"SKY", @"HIGH", ToolsStrings.Weather_ColorCurves_SkyHigh, Color.FromRgb(30, 73, 167), 3.0, 5d),
                    new WeatherColorEntry(@"SUN", @"LOW", ToolsStrings.Weather_ColorCurves_SunLow, Color.FromRgb(229, 140, 70), 40d, 50d),
                    new WeatherColorEntry(@"SUN", @"HIGH", ToolsStrings.Weather_ColorCurves_SunHigh, Color.FromRgb(170, 160, 140), 20d, 50d),
                    new WeatherColorEntry(@"AMBIENT", @"LOW", ToolsStrings.Weather_ColorCurves_AmbientLow, Color.FromRgb(124, 124, 124), 18d, 30d),
                    new WeatherColorEntry(@"AMBIENT", @"HIGH", ToolsStrings.Weather_ColorCurves_AmbientHigh, Color.FromRgb(105, 105, 105), 11d, 30d),
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
                Logging.Warning("LoadExtended(): " + e);
            }

            try {
                ReloadColorCurves();
            } finally {
                Changed = changed;
                _loadedExtended = true;
            }
        }

        public override void Reload() {
            OnImageChangedValue(PreviewImage);
            base.Reload();
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) return true;

            if (FileUtils.IsAffected(filename, PreviewImage)) {
                OnImageChangedValue(PreviewImage);
                return true;
            }

            if (_loadedExtended && FileUtils.IsAffected(filename, ColorCurvesIniFilename)) {
                if (!Changed ||
                        ModernDialog.ShowMessage(ToolsStrings.AcObject_ReloadAutomatically_Ini, ToolsStrings.AcObject_ReloadAutomatically, MessageBoxButton.YesNo) ==
                                MessageBoxResult.Yes) {
                    var c = Changed;
                    ReloadColorCurves();
                    Changed = c;
                }

                return true;
            }

            return false;
        }

        public override int CompareTo(AcPlaceholderNew o) {
            return Enabled == o.Enabled ?
                    AlphanumComparatorFast.Compare(Id, o.Id) : Enabled ? -1 : 1;
        }
    }
}
