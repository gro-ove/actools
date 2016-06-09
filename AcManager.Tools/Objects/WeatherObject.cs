using System;
using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public class WeatherObject : AcIniObject {
        public WeatherObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        private WeatherType? _type;

        public WeatherType? Type {
            get { return _type; }
            set { 
                if (Equals(_type, value)) return;
                _type = value; 
                OnPropertyChanged();
                Changed = true;

                if (_preparedForEditing) {
                    SaveData(IniObject);
                    Content = IniObject.Stringify();
                }
            }
        }

        private double _temperatureCoefficient;

        public double TemperatureCoefficient {
            get { return _temperatureCoefficient; }
            set { 
                if (Equals(_temperatureCoefficient, value)) return;
                _temperatureCoefficient = value;
                OnPropertyChanged();
                Changed = true;

                if (_preparedForEditing) {
                    SaveData(IniObject);
                    Content = IniObject.Stringify();
                }
            }
        }

        private bool _forceCarLights;

        public bool ForceCarLights {
            get { return _forceCarLights; }
            set {
                if (Equals(value, _forceCarLights)) return;
                _forceCarLights = value;
                OnPropertyChanged();
                Changed = true;

                if (_preparedForEditing) {
                    SaveData(IniObject);
                    Content = IniObject.Stringify();
                }
            }
        }

        private static WeatherType? TryToDetectWeatherTypeById(string id) {
            var lid = id.ToLower();

            if (lid.Contains("mid_clear")) return WeatherType.FewClouds;
            if (lid.Contains("light_clouds")) return WeatherType.ScatteredClouds;
            if (lid.Contains("heavy_clouds")) return WeatherType.OvercastClouds;

            if (lid.Contains("clouds")) return WeatherType.BrokenClouds;
            if (lid.Contains("clear")) return WeatherType.Clear;
            
            if (lid.Contains("light_fog")) return WeatherType.Mist;
            if (lid.Contains("fog")) return WeatherType.Fog;

            return null;
        }

        public string ColorCurvesIniFilename => Path.Combine(Location, "colorCurves.ini");

        public override string IniFilename => Path.Combine(Location, "weather.ini");

        protected override void LoadData(IniFile ini) {
            Name = ini["LAUNCHER"].Get("NAME");
            TemperatureCoefficient = ini["LAUNCHER"].GetDouble("TEMPERATURE_COEFF", 0d);
            ForceCarLights = ini["CAR_LIGHTS"].GetBool("FORCE_ON", false);

            try {
                Type = ini["__LAUNCHER_CM"].GetEnumNullable<WeatherType>("WEATHER_TYPE");
            } catch (Exception) {
                Type = null;
            }

            if (!Type.HasValue) {
                Type = TryToDetectWeatherTypeById(Id);
            }
        }

        public override void Save() {
            var ini = _preparedForEditing ? IniFile.Parse(Content) : IniObject;
            SaveData(ini);

            using (WeatherManager.Instance.IgnoreChanges()) {
                File.WriteAllText(IniFilename, ini.ToString());
            }

            Changed = false;
        }

        public override void SaveData(IniFile ini) {
            ini["LAUNCHER"].Set("NAME", Name);
            ini["LAUNCHER"].Set("TEMPERATURE_COEFF", TemperatureCoefficient);
            ini["CAR_LIGHTS"].Set("FORCE_ON", ForceCarLights);
            ini["__LAUNCHER_CM"].Set("WEATHER_TYPE", Type);
        }

        public override bool HandleChangedFile(string filename) {
            if (string.Equals(filename, IniFilename, StringComparison.OrdinalIgnoreCase)) {
                if (_preparedForEditing) {
                    Content = File.ReadAllText(Location);
                } else {
                    LoadOrThrow();
                }
            }

            return true;
        }

        private string _content;

        public string Content {
            get { return _content; }
            set {
                if (Equals(value, _content)) return;
                _content = value;
                OnPropertyChanged();
                Changed = true;

                if (_preparedForEditing && _content != null) {
                    LoadData(IniFile.Parse(_content));
                }
            }
        }

        private bool _preparedForEditing;

        public void PrepareForEditing() {
            if (_preparedForEditing) return;

            var changed = Changed;
            try {
                Content = File.ReadAllText(IniFilename);
                RemoveError(AcErrorType.Data_IniIsMissing);
            } catch (Exception e) {
                Logging.Write("[WeatherObject] Can’t load: " + e);
                AddError(AcErrorType.Data_IniIsMissing, Id);
            } finally {
                Changed = changed;
                _preparedForEditing = true;
            }
        }
    }
}
