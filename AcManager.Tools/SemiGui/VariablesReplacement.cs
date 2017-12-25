using System;
using System.Globalization;
using System.Text.RegularExpressions;
using AcManager.Tools.Managers;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public static class VariablesReplacement {

        private static readonly Regex ReplayNameRegex = new Regex(@"\{([\w\.]+)(?::(\w*))?\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string GetAcDate(DateTime date) {
            return $"{date.Day}-{date.Month}-{date.Year - 1900}-{date.Hour}-{date.Minute}-{date.Second}";
        }

        public static string GetType(Game.StartProperties startProperties, [CanBeNull] Game.Result result) {
            // TODO: drag mode
            if (startProperties.ModeProperties is Game.DriftProperties) {
                return "Drift";
            }

            if (startProperties.ModeProperties is Game.TimeAttackProperties) {
                return "Time Attack";
            }

            if (startProperties.ModeProperties is Game.HotlapProperties) {
                return "Hotlap";
            }

            if (startProperties.ModeProperties is Game.OnlineProperties) {
                return "Online";
            }

            if (startProperties.ModeProperties is Game.PracticeProperties) {
                return "Practice";
            }

            if (result?.Sessions?.Length == 1) {
                return "Race";
            }

            if (result?.Sessions?.Length > 1) {
                return "Weekend";
            }

            return startProperties.ModeProperties is Game.RaceProperties ? "Race" : "Something unspeakable";
        }

        [CanBeNull]
        private static string GetValue(Game.StartProperties startProperties, [CanBeNull] Game.Result result, string key) {
            if (startProperties.BasicProperties == null) return null;

            switch (key) {
                case "type":
                    return GetType(startProperties, result);
                case "car":
                    return CarsManager.Instance.GetById(startProperties.BasicProperties.CarId ?? "")?.DisplayName;
                case "car.id":
                    return startProperties.BasicProperties.CarId;
                case "track":
                    var track = TracksManager.Instance.GetById(startProperties.BasicProperties.TrackId ?? "");
                    var config = startProperties.BasicProperties.TrackConfigurationId != null
                            ? track?.GetLayoutByLayoutId(startProperties.BasicProperties.TrackConfigurationId) : track;
                    return config?.Name;
                case "track.id":
                    return startProperties.BasicProperties.TrackId;
                case "date":
                    return startProperties.StartTime.ToString(CultureInfo.CurrentCulture);
                case "date_ac":
                    return GetAcDate(startProperties.StartTime);
                default:
                    return null;
            }
        }

        public static string Process([NotNull] string str, [NotNull] Game.StartProperties startProperties, [CanBeNull] Game.Result result) {
            if (startProperties == null) throw new ArgumentNullException(nameof(startProperties));
            if (str == null) throw new ArgumentNullException(nameof(str));

            return ReplayNameRegex.Replace(str, match => {
                var value = GetValue(startProperties, result, match.Groups[1].Value)?.Trim();
                if (string.IsNullOrEmpty(value)) return "-";

                foreach (var c in match.Groups[2].Success ? match.Groups[2].Value.ToLowerInvariant() : string.Empty) {
                    switch (c) {
                        case 'l':
                            value = value.ToLowerInvariant();
                            break;

                        case 'u':
                            value = value.ToUpperInvariant();
                            break;

                        case '0':
                            value = value.Substring(0, 1);
                            break;

                        default:
                            Logging.Warning("Unsupported modifier: " + c);
                            break;
                    }
                }

                return value;
            });
        }
    }
}