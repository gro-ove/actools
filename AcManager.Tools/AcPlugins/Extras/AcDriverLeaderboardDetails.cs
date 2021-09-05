using System;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.AcPlugins.Extras {
    [JsonObject(MemberSerialization.OptIn)]
    public class AcDriverLeaderboardDetails : NotifyPropertyChanged {
        private readonly int _id;

        [CanBeNull]
        private readonly IAcLeaderboardCommandHelper _commandHelper;

        [JsonConstructor]
        public AcDriverLeaderboardDetails() {
            _id = -1;
        }

        public AcDriverLeaderboardDetails(int id, [CanBeNull] IAcLeaderboardCommandHelper commandHelper) {
            _id = id;
            _commandHelper = commandHelper;
        }

        internal int SilentFor;

        [CanBeNull]
        private AcDriverDetails _driver;

        [CanBeNull, JsonProperty("driver")]
        public AcDriverDetails Driver {
            get => _driver;
            set {
                if (Equals(value, _driver)) return;
                _driver = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CopyGuidCommand));
            }
        }

        [NotNull, JsonProperty("location")]
        public AcDriverLocation Location { get; set; } = new AcDriverLocation(0f, 0f);

        [JsonProperty("bestLap")]
        private TimeSpan _bestLapTime;

        public TimeSpan BestLapTime {
            get => _bestLapTime;
            set => Apply(value, ref _bestLapTime, () => OnPropertyChanged(nameof(DisplayBestLapTime)));
        }

        public TimeSpan BestLapSortingTime => _bestLapTime == TimeSpan.Zero ? TimeSpan.MaxValue : _bestLapTime;

        [JsonProperty("lastLap")]
        private TimeSpan _lastLapTime;

        public TimeSpan LastLapTime {
            get => _lastLapTime;
            set => Apply(value, ref _lastLapTime, () => OnPropertyChanged(nameof(DisplayLastLapTime)));
        }

        [JsonProperty("totalLaps")]
        private int _totalLaps;

        public int TotalLaps {
            get => _totalLaps;
            set => Apply(value, ref _totalLaps);
        }

        private int _collisions;

        [JsonProperty("collisions")]
        public int Collisions {
            get => _collisions;
            set => Apply(value, ref _collisions);
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
            set => Apply(value, ref _currentLapProgress, () => {
                OnPropertyChanged(nameof(CurrentLapTime));
                OnPropertyChanged(nameof(DisplayCurrentLapTime));
                OnPropertyChanged(nameof(DisplayCurrentLapProgress));
                OnPropertyChanged(nameof(DisplayCurrentLapTimeAndProgress));
            });
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

        public TimeSpan CurrentLapTime => DateTime.Now - CurrentLapStart;

        public string DisplayCurrentLapTime => CurrentLapTime.ToMillisecondsString();

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

        private DelegateCommand _copyGuidCommand;

        public DelegateCommand CopyGuidCommand => _copyGuidCommand ?? (_copyGuidCommand = new DelegateCommand(
                () => ClipboardHelper.SetText(Driver?.Guid), () => Driver != null));

        private DelegateCommand _SendMessageCommand;

        public DelegateCommand SendMessageCommand => _SendMessageCommand ?? (_SendMessageCommand = new DelegateCommand(
                () => _commandHelper?.SendMessageDirectly(_id), () => _id >= 0 && _commandHelper != null));

        private DelegateCommand _MentionCommand;

        public DelegateCommand MentionCommand => _MentionCommand ?? (_MentionCommand = new DelegateCommand(
                () => _commandHelper?.MentionInChat(_id), () => _id >= 0 && _commandHelper != null));

        private DelegateCommand _kickCommand;

        public DelegateCommand KickCommand => _kickCommand ?? (_kickCommand = new DelegateCommand(
                () => _commandHelper?.KickPlayer(_id), () => _id >= 0 && _commandHelper != null));

        private DelegateCommand _BanCommand;

        public DelegateCommand BanCommand => _BanCommand ?? (_BanCommand = new DelegateCommand(
                () => _commandHelper?.BanPlayer(_id), () => _id >= 0 && _commandHelper != null));
    }
}