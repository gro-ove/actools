using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcPlugins.Messages;
using AcTools.Processes;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.AcPlugins.Extras {
    public sealed class AcLeaderboard : NotifyPropertyChanged {
        public class ConnectedDriversCollection : WrappedFilteredCollection<AcDriverLeaderboardDetails, AcDriverLeaderboardDetails> {
            public ConnectedDriversCollection([NotNull] IReadOnlyList<AcDriverLeaderboardDetails> collection) : base(collection) { }

            protected override AcDriverLeaderboardDetails Wrap(AcDriverLeaderboardDetails source) {
                return source;
            }

            protected override bool Test(AcDriverLeaderboardDetails source) {
                return source.Driver != null;
            }
        }

        private readonly List<AcDriverLeaderboardDetails> _positionHelperList;

        public BetterObservableCollection<AcDriverLeaderboardDetails> Leaderboard { get; }

        public ConnectedDriversCollection ConnectedOnly { get; }

        public bool ResetBestLapTimeOnNewSession { get; set; } = true;

        private bool _inRaceSession;
        private LapProgressComparer _lapProgressComparer;

        public AcLeaderboard(int capacity, [CanBeNull] IAcLeaderboardCommandHelper commandHelper) {
            Leaderboard = new BetterObservableCollection<AcDriverLeaderboardDetails>(
                    Enumerable.Range(0, capacity).Select((x, i) => new AcDriverLeaderboardDetails(i, commandHelper)));
            ConnectedOnly = new ConnectedDriversCollection(Leaderboard);
            _positionHelperList = Leaderboard.ToList();
            _lapProgressComparer = new LapProgressComparer(false);
        }

        [CanBeNull]
        public AcDriverLeaderboardDetails GetDetails(int carId) {
            return carId < 0 || carId >= Leaderboard.Count ? null : Leaderboard[carId];
        }

        public void OnCarInfo(MsgCarInfo msg) {
            if (msg.CarId >= Leaderboard.Count) return;
            var item = Leaderboard[msg.CarId];
            if (item.Driver != null && item.Driver.DriverName == msg.DriverName) return;
            // Logging.Debug("New car: " + msg.CarId + ", car: " + msg.CarModel);
            item.SilentFor = 0;
            item.Driver = new AcDriverDetails(msg.DriverGuid, msg.DriverName, msg.CarModel, msg.CarSkin);
            item.Reset(true);
            ConnectedOnly.Refresh(item);
        }

        // Returns true if car just crossed starting line during the race
        public bool OnCarUpdate(MsgCarUpdate msg) {
            // Logging.Debug("Car update: " + msg.CarId + ", pos: " + msg.NormalizedSplinePosition);
            if (msg.CarId >= Leaderboard.Count) return false;
            var item = Leaderboard[msg.CarId];
            item.CurrentLapProgress = msg.NormalizedSplinePosition;
            _positionHelperList.Sort(_lapProgressComparer);
            item.CurrentRacePosition = _positionHelperList.IndexOf(item) + 1;
            item.Location.PositionX = msg.WorldPosition.X;
            item.Location.PositionZ = msg.WorldPosition.Z;
            item.SilentFor = 0;
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
            item.SilentFor = 0;
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
            var item = Leaderboard[msg.CarId];
            item.Driver = null;
            item.Reset(true);
            ConnectedOnly.Refresh(item);
        }

        public void OnSessionInfo(MsgSessionInfo msg) {
            _inRaceSession = msg.SessionType == Game.SessionType.Race;
            _lapProgressComparer = new LapProgressComparer(_inRaceSession);
            foreach (var item in Leaderboard) {
                item.Reset(ResetBestLapTimeOnNewSession);
            }
        }

        public void OnCollision(MsgClientEvent msg) {
            if (msg.RelativeVelocity > 1 && msg.CarId != msg.OtherCarId) {
                if (msg.CarId < Leaderboard.Count) ++Leaderboard[msg.CarId].Collisions;
                if (msg.OtherCarId < Leaderboard.Count) ++Leaderboard[msg.OtherCarId].Collisions;
            }
        }

        public void CheckDisconnected() {
            foreach (var item in Leaderboard) {
                if (item.Driver != null && ++item.SilentFor > 5) {
                    // Logging.Debug("item.SilentFor=" + item.SilentFor);
                    ActionExtension.InvokeInMainThread(() => {
                        item.Driver = null;
                        item.Reset(true);
                        ConnectedOnly.Refresh(item);
                    });
                }
            }
        }
    }
}