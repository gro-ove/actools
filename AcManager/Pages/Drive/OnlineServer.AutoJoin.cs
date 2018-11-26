using System.ComponentModel;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Drive {
    public partial class OnlineServer {
        public partial class ViewModel {
            private bool _autoJoinActive;

            public bool AutoJoinActive {
                get => _autoJoinActive;
                set {
                    if (Equals(value, _autoJoinActive)) return;
                    _autoJoinActive = value;
                    OnPropertyChanged();
                    _startAutoJoinCommand?.RaiseCanExecuteChanged();
                    _stopAutoJoinCommand?.RaiseCanExecuteChanged();
                }
            }

            private bool _autoJoinAnyCar;

            public bool AutoJoinAnyCar {
                get => _autoJoinAnyCar;
                set {
                    if (Equals(value, _autoJoinAnyCar)) return;
                    _autoJoinAnyCar = value;
                    OnPropertyChanged();
                    _startAutoJoinCommand?.RaiseCanExecuteChanged();
                    _stopAutoJoinCommand?.RaiseCanExecuteChanged();
                }
            }

            private DelegateCommand<bool> _startAutoJoinCommand;

            public DelegateCommand<bool> StartAutoJoinCommand => _startAutoJoinCommand ?? (_startAutoJoinCommand = new DelegateCommand<bool>(anyCar => {
                AutoJoinActive = true;
                AutoJoinAnyCar = anyCar;
            }, anyCar => (!AutoJoinActive || AutoJoinAnyCar != anyCar) && (anyCar ? Entry.AutoJoinAnyCarAvailable : Entry.AutoJoinAvailable))
                    .ListenOnWeak(Entry, nameof(Entry.AutoJoinAvailable))
                    .ListenOnWeak(Entry, nameof(Entry.AutoJoinAnyCarAvailable)));

            private DelegateCommand _stopAutoJoinCommand;

            public DelegateCommand StopAutoJoinCommand => _stopAutoJoinCommand ?? (_stopAutoJoinCommand = new DelegateCommand(() => {
                AutoJoinActive = false;
            }, () => AutoJoinActive));

            public void OnPropertyChanged(PropertyChangedEventArgs a) {
                if (!AutoJoinActive) return;

                if (Entry.IsAutoJoinReady(AutoJoinAnyCar)) {
                    Logging.Here();
                    AutoJoinActive = false;

                    if (AutoJoinAnyCar && !Entry.FixedCar) {
                        var av = Entry.Cars?.FirstOrDefault(x => x.IsAvailable);
                        if (av != null) {
                            Logging.Write($"Available car: {av.DisplayName} (IsAvailable={Entry.IsAvailable})");
                            Entry.SetSelectedCarEntry(av);
                            Entry.AvailableUpdate();
                            Logging.Write($"IsAvailable={Entry.IsAvailable}");
                        }
                    }

                    Entry.JoinCommand.ExecuteAsync(null).Ignore();
                    // Entry.JoinCommand.Execute(ServerEntry.ForceJoin);
                } else if (AutoJoinAnyCar) {
                    if (a.PropertyName == nameof(Entry.AutoJoinAnyCarAvailable) && !Entry.AutoJoinAnyCarAvailable) {
                        AutoJoinActive = false;
                    }
                } else {
                    if (a.PropertyName == nameof(Entry.AutoJoinAvailable) && !Entry.AutoJoinAvailable) {
                        AutoJoinActive = false;
                    }
                }
            }
        }
    }
}