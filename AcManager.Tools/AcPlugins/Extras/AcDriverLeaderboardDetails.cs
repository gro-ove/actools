using System;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.AcPlugins.Extras {
    [JsonObject(MemberSerialization.OptIn)]
    public class AcDriverLeaderboardDetails : NotifyPropertyChanged {
        [JsonConstructor]
        public AcDriverLeaderboardDetails() { }

        [CanBeNull, JsonProperty("driver")]
        public AcDriverDetails Driver { get; set; }

        [CanBeNull, JsonProperty("location")]
        public AcDriverLocation Location { get; set; }

        [JsonProperty("bestLap")]
        private TimeSpan _bestLapTime;

        public TimeSpan BestLapTime {
            get => _bestLapTime;
            set => Apply(value, ref _bestLapTime);
        }

        public TimeSpan BestLapSortingTime => _bestLapTime == TimeSpan.Zero ? TimeSpan.MaxValue : _bestLapTime;

        [JsonProperty("lastLap")]
        private TimeSpan _lastLapTime;

        public TimeSpan LastLapTime {
            get => _lastLapTime;
            set => Apply(value, ref _lastLapTime);
        }

        [JsonProperty("totalLaps")]
        private int _totalLaps;

        public int TotalLaps {
            get => _totalLaps;
            set => Apply(value, ref _totalLaps);
        }

        [JsonProperty("currentLapValid")]
        private bool _currentLapValid;

        public bool CurrentLapValid {
            get => _currentLapValid;
            set => Apply(value, ref _currentLapValid);
        }

        [JsonProperty("lapStart")]
        private DateTime _currentLapStart;

        public DateTime CurrentLapStart {
            get => _currentLapStart;
            set => Apply(value, ref _currentLapStart);
        }

        [JsonProperty("lapProgress")]
        private double _currentLapProgress;

        public double CurrentLapProgress {
            get => _currentLapProgress;
            set => Apply(value, ref _currentLapProgress);
        }

        [JsonProperty("lapSpoiled")]
        private bool _currentLapSpoiled;

        public bool CurrentLapSpoiled {
            get => _currentLapSpoiled;
            set => Apply(value, ref _currentLapSpoiled);
        }

        [JsonProperty("racePos")]
        private int _currentRacePosition;

        public int CurrentRacePosition {
            get => _currentRacePosition;
            set => Apply(value, ref _currentRacePosition);
        }

        [JsonProperty("finishedPosition")]
        private int? _finishedPosition;

        public int? FinishedPosition {
            get => _finishedPosition;
            set => Apply(value, ref _finishedPosition);
        }

        public string DisplayBestLapTime => BestLapTime == TimeSpan.Zero ? "–" : BestLapTime.ToMillisecondsString();

        public string DisplayLastLapTime => LastLapTime == TimeSpan.Zero ? "–" : LastLapTime.ToMillisecondsString();

        public string DisplayCurrentLapTime => (DateTime.Now - CurrentLapStart).ToMillisecondsString();

        public string DisplayCurrentLapProgress => $"{CurrentLapProgress * 100:F1}%";

        public string DisplayCurrentLapTimeAndProgress => CurrentLapValid && CurrentLapProgress > 0.05f
                ? $"{DisplayCurrentLapTime} ({CurrentLapProgress * 100:F0}%)" : "–";

        public void Reset(bool resetBestLapTime) {
            if (resetBestLapTime) {
                BestLapTime = TimeSpan.Zero;
            }
            LastLapTime = TimeSpan.Zero;
            CurrentLapStart = DateTime.Now;
            CurrentLapProgress = 0d;
            CurrentLapSpoiled = false;
            CurrentLapValid = false;
            CurrentRacePosition = 0;
            TotalLaps = 0;
        }
    }
}