using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcPlugins.Messages;
using AcTools.Processes;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.AcPlugins.Extras {
    public sealed class AcLeaderboard : NotifyPropertyChanged {
        private readonly List<AcDriverLeaderboardDetails> _positionHelperList;

        public BetterObservableCollection<AcDriverLeaderboardDetails> Leaderboard { get; }

        public bool ResetBestLapTimeOnNewSession { get; set; } = true;

        private bool _inRaceSession;
        private LapProgressComparer _lapProgressComparer;

        public AcLeaderboard(int capacity) {
            Leaderboard = new BetterObservableCollection<AcDriverLeaderboardDetails>(
                    Enumerable.Range(0, capacity).Select(x => new AcDriverLeaderboardDetails()));
            OnPropertyChanged(nameof(Leaderboard));
            _positionHelperList = Leaderboard.ToList();
            _lapProgressComparer = new LapProgressComparer(false);
        }

        public void OnCarInfo(MsgCarInfo msg) {
            if (msg.CarId >= Leaderboard.Count
                    || Leaderboard[msg.CarId].Driver != null && Leaderboard[msg.CarId].Driver.DriverName == msg.DriverName) return;
            // Logging.Debug("New car: " + msg.CarId + ", car: " + msg.CarModel);
            Leaderboard[msg.CarId].Driver = new AcDriverDetails(msg.DriverGuid, msg.DriverName, msg.CarModel, msg.CarSkin);
            Leaderboard[msg.CarId].Reset(true);
        }

        // Returns true if car just crossed starting line during the race
        public bool OnCarUpdate(MsgCarUpdate msg) {
            // Logging.Debug("Car update: " + msg.CarId + ", pos: " + msg.NormalizedSplinePosition);
            if (msg.CarId >= Leaderboard.Count) return false;
            var item = Leaderboard[msg.CarId];
            item.CurrentLapProgress = msg.NormalizedSplinePosition;
            _positionHelperList.Sort(_lapProgressComparer);
            item.CurrentRacePosition = _positionHelperList.IndexOf(item) + 1;
            item.Location = new AcDriverLocation(msg.WorldPosition.X, msg.WorldPosition.Z);
            // Trying to register first lap
            if (!item.CurrentLapValid && _inRaceSession && msg.NormalizedSplinePosition < 0.1d && msg.Velocity.Length() > 0.5f) {
                item.CurrentLapValid = true;
                item.CurrentLapStart = DateTime.Now;
                return true;
            }
            return false;
        }

        public void OnLapCompleted(MsgLapCompleted msg, int totalLaps) {
            // Logging.Debug("New lap: " + msg.CarId + ", time: " + TimeSpan.FromMilliseconds(msg.LapTime).ToMillisecondsString());
            if (msg.CarId >= Leaderboard.Count) return;
            var item = Leaderboard[msg.CarId];
            var lapTime = TimeSpan.FromMilliseconds(msg.LapTime);
            if ((item.BestLapTime == TimeSpan.Zero || lapTime < item.BestLapTime) && msg.Cuts == 0) {
                item.BestLapTime = lapTime;
            }
            _positionHelperList.Sort(_lapProgressComparer);
            item.CurrentRacePosition = _positionHelperList.IndexOf(item) + 1;
            item.CurrentLapStart = DateTime.Now;
            item.LastLapTime = lapTime;
            item.CurrentLapValid = true;
            item.TotalLaps++;
            if (item.TotalLaps == totalLaps) {
                item.FinishedPosition = Leaderboard.Count(x => x.FinishedPosition.HasValue);
            }
        }

        private class LapProgressComparer : IComparer<AcDriverLeaderboardDetails> {
            private bool _inRaceSession;

            public LapProgressComparer(bool inRaceSession) {
                _inRaceSession = inRaceSession;
            }

            public int Compare(AcDriverLeaderboardDetails x, AcDriverLeaderboardDetails y) {
                if (x == null || y == null) return 0;
                if ((x.Driver == null) != (y.Driver == null)) return y.Driver != null ? 1 : -1;
                if (!_inRaceSession) {
                    return x.BestLapSortingTime.CompareTo(y.BestLapSortingTime);
                }
                if (x.FinishedPosition.HasValue || y.FinishedPosition.HasValue) {
                    return (x.FinishedPosition ?? 99).CompareTo(y.FinishedPosition ?? 99);
                }
                return x.TotalLaps == y.TotalLaps
                        ? -x.CurrentLapProgress.CompareTo(y.CurrentLapProgress)
                        : -x.TotalLaps.CompareTo(y.TotalLaps);
            }
        }

        public void OnConnectionClosed(MsgConnectionClosed msg) {
            if (msg.CarId >= Leaderboard.Count) return;
            Leaderboard[msg.CarId].Driver = null;
            Leaderboard[msg.CarId].Reset(true);
        }

        public void OnSessionInfo(MsgSessionInfo msg) {
            _inRaceSession = msg.SessionType == Game.SessionType.Race;
            _lapProgressComparer = new LapProgressComparer(_inRaceSession);
            foreach (var item in Leaderboard) {
                item.Reset(ResetBestLapTimeOnNewSession);
            }
        }
    }
}