using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public class ReplayHelper : NotifyPropertyChanged {
        public bool IsReplayAvailable { get; }

        public readonly string OriginalReplayFilename;

        [CanBeNull]
        public readonly string RenamedReplayFilename;

        internal ReplayHelper(Game.StartProperties startProperties, Game.Result result) {
            OriginalReplayFilename = Path.Combine(FileUtils.GetReplaysDirectory(), ReplayObject.PreviousReplayName);

            var replayName = GetReplayName(startProperties, result);
            RenamedReplayFilename = replayName == null ? null : FileUtils.EnsureUnique(Path.Combine(FileUtils.GetReplaysDirectory(), replayName));

            IsReplayAvailable = replayName != null && File.Exists(OriginalReplayFilename);
            if (IsReplayAvailable && SettingsHolder.Drive.AutoSaveReplays) {
                IsReplayRenamed = true;
            }
        }

        private bool _isReplayRenamed;

        public bool IsReplayRenamed {
            get { return _isReplayRenamed; }
            set {
                if (!IsReplayAvailable || RenamedReplayFilename == null || Equals(_isReplayRenamed, value)) return;

                try {
                    if (value) {
                        File.Move(OriginalReplayFilename, RenamedReplayFilename);
                    } else {
                        File.Move(RenamedReplayFilename, OriginalReplayFilename);
                    }

                    _isReplayRenamed = value;
                    OnPropertyChanged();
                } catch (Exception e) {
                    NonfatalError.Notify(value ? @"Can't save replay" : @"Can't unsave replay", e);
                }
            }
        }

        public static string GetType(Game.StartProperties startProperties, Game.Result result) {
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

            return startProperties.ModeProperties is Game.RaceProperties ? "Race" : "Something Unspeakable";
        }

        private static string GetAcDate(DateTime date) {
            return $"{date.Day}-{date.Month}-{date.Year - 1900}-{date.Hour}-{date.Minute}-{date.Second}";
        }

        [CanBeNull]
        private static string GetValue(Game.StartProperties startProperties, Game.Result result, string key) {
            if (startProperties.BasicProperties == null || result == null) return null;

            switch (key) {
                case "type":
                    return GetType(startProperties, result);
                case "car":
                    return CarsManager.Instance.GetById(startProperties.BasicProperties.CarId)?.DisplayName;
                case "car.id":
                    return startProperties.BasicProperties.CarId;
                case "track":
                    var track = TracksManager.Instance.GetById(startProperties.BasicProperties.TrackId);
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

        private static readonly Regex ReplayNameRegex = new Regex(@"\{([\w\.]+)(?::(\w*))?\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        [CanBeNull]
        private static string GetReplayName(Game.StartProperties startProperties, Game.Result result) {
            if (startProperties == null || result == null) return null;

            var s = SettingsHolder.Drive.ReplaysNameFormat;
            if (string.IsNullOrEmpty(s)) {
                s = SettingsHolder.Drive.DefaultReplaysNameFormat;
            }

            s = ReplayNameRegex.Replace(s, match => {
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

            return FileUtils.EnsureFileNameIsValid(s);
        }
    }
}