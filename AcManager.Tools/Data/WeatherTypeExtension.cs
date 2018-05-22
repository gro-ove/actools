using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public class WeatherTypeConverterState {
        private string _footprint;

        [CanBeNull]
        public WeatherObject TryToGetWeather(WeatherType type, int time, double temperature) {
            return type.TryToGetWeather(time, temperature, ref _footprint);
        }

        public void Reset() {
            _footprint = null;
        }
    }

    public static class WeatherTypeExtension {
        [CanBeNull]
        private static IReadOnlyDictionary<WeatherType, double> GetFallbacks(this WeatherType value) {
            var name = Enum.GetName(typeof(WeatherType), value);
            if (name == null) return null;
            var field = typeof(WeatherType).GetField(name);
            return field == null ? null
                    : (Attribute.GetCustomAttribute(field, typeof(WeatherFallbackAttribute)) as WeatherFallbackAttribute)?.Distances;
        }

        private static readonly List<KeyValuePair<WeatherType, IReadOnlyDictionary<WeatherType, double>>> WeatherTypesWithFallbacks
                = Enum.GetValues(typeof(WeatherType)).OfType<WeatherType>().ToDictionary(x => x, GetFallbacks).Where(x => x.Value != null).ToList();

        public static WeatherType? FindClosestWeather(this WeatherType type, IEnumerable<WeatherType> list) {
            if (type == WeatherType.None) return null;

            var weatherTypes = list.Select(x => (WeatherType?)x).ToList();
            if (weatherTypes.Contains(type)) return type;

            var minimalDistance = double.MaxValue;
            WeatherType? result = null;
            var distances = new Dictionary<WeatherType, double>();
            AddType(type, 0);
            return result;

            void AddType(WeatherType t, double baseDistance) {
                if (baseDistance > minimalDistance || distances.TryGetValue(t, out var d) && d <= baseDistance) return;

                distances[t] = baseDistance;
                if (weatherTypes.Contains(t) && baseDistance < minimalDistance) {
                    minimalDistance = baseDistance;
                    result = t;
                }

                foreach (var a in WeatherTypesWithFallbacks) {
                    if (a.Key == t) {
                        foreach (var f in a.Value) {
                            AddType(f.Key, f.Value + baseDistance);
                        }
                    } else if (a.Value.ContainsKey(t)) {
                        var ct = t;
                        AddType(a.Key, a.Value.First(x => x.Key == ct).Value + baseDistance);
                    }
                }
            }
        }

        private static bool FitsTime(WeatherObject weatherObject, int time) {
            return weatherObject.GetTimeDiapason()?.Contains(time)
                    ?? time >= CommonAcConsts.TimeMinimum && time <= CommonAcConsts.TimeMaximum;
        }

        public static bool Fits(this WeatherObject weatherObject, int? time, double? temperature) {
            return weatherObject.Enabled
                    && (time == null || FitsTime(weatherObject, time.Value))
                    && (temperature == null || weatherObject.GetTemperatureDiapason()?.Contains(temperature.Value) != false);
        }

        public static bool Fits(this WeatherObject weatherObject, WeatherType type, int? time, double? temperature) {
            return weatherObject.Fits(time, temperature) && weatherObject.Type == type;
        }

        [CanBeNull]
        public static WeatherObject TryToGetWeather(this WeatherType type, int time, double temperature, ref string currentFootprint) {
            if (type == WeatherType.None) return null;

            try {
                var candidates = WeatherManager.Instance.Loaded.Where(x => x.Fits(time, temperature)).ToList();
                var closest = type.FindClosestWeather(from w in candidates select w.Type);
                if (closest == null) {
                    return time < CommonAcConsts.TimeMinimum || time > CommonAcConsts.TimeMaximum
                            ? candidates.RandomElementOrDefault()
                            : null;
                }

                candidates = candidates.Where(x => x.Type == closest).ToList();
                var footprint = candidates.Select(x => x.Id).JoinToString(';');
                if (footprint == currentFootprint) return null;

                currentFootprint = footprint;
                return candidates.RandomElementOrDefault();
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t find weather", e);
                return null;
            }
        }

        [CanBeNull]
        public static WeatherObject TryToGetWeather(this WeatherType type, int time, double temperature) {
            string footprint = null;
            return type.TryToGetWeather(time, temperature, ref footprint);
        }

        [CanBeNull]
        public static WeatherObject TryToGetWeather(this WeatherDescription description, int time) {
            return TryToGetWeather(description.Type, time, description.Temperature);
        }
    }
}