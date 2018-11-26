using System;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Profile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using OverallStats = AcManager.Tools.Profile.PlayerStatsManager.OverallStats;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private static Storage StatsStorage
            => _statsStorageInner ?? (_statsStorageInner = new Storage(FilesStorage.Instance.GetFilename("Progress", "Online Stats.data")));

        private static Storage _statsStorageInner;

        private OverallStats _stats;

        [NotNull]
        public OverallStats Stats {
            get {
                if (_stats == null) {
                    _stats = StatsStorage.GetOrCreateObject<OverallStats>(Id);
                    _stats.Storage = new Substorage(StatsStorage, Id);
                }

                return _stats;
            }
        }

        public int SessionsCount => Stats.SessionsCount;

        private void UpdateStats() {
            if (SettingsHolder.Drive.WatchForSharedMemory) {
                var last = PlayerStatsManager.Instance.Last;
                if (last != null && !last.IsSpoiled) {
                    if (!CountingStatsFrom.HasValue) {
                        CountingStatsFrom = DateTime.Now;
                    }

                    Stats.Extend(last);
                    StatsStorage.SetObject(Id, Stats);
                    OnPropertyChanged(nameof(SessionsCount));
                }
            }
        }

        private DelegateCommand _resetStatsCommand;

        public DelegateCommand ResetStatsCommand => _resetStatsCommand ?? (_resetStatsCommand = new DelegateCommand(() => {
            if (MessageDialog.Show("Are you sure you want to reset server stats? Can’t be undone, you know.",
                    "Careful now", MessageDialogButton.YesNo)
                    != MessageBoxResult.Yes) {
                return;
            }

            Stats.Reset();
            StatsStorage.SetObject(Id, Stats);
            LastConnected = null;
            OnPropertyChanged(nameof(SessionsCount));
        }, () => Stats.SessionsCount != 0 || LastConnected != null));
    }
}