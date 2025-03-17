using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Tools.Managers.Online {
    public partial class OnlineManager {
        private int _pinged;

        public int Pinged {
            get => _pinged;
            set => Apply(value, ref _pinged);
        }

        public bool PingingInProcess => _currentPinging != null;

        public void StopPinging() {
            _currentPinging?.Cancel();
            _currentPinging = null;
            OnPropertyChanged(nameof(PingingInProcess));
        }

        private CancellationTokenSource _currentPinging;

        public async Task PingEverything([CanBeNull] IFilter<ServerEntry> priorityFilter, CancellationToken cancellationToken = default) {
            StopPinging();

            var cancellation = new CancellationTokenSource();
            var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellation.Token);

            using (var pinging = TaskbarService.Create("Pinging online servers", 10)) {
                void SetPinged(int value) {
                    Pinged = value;
                    pinging.Set(TaskbarState.Normal, (double)value / List.Count);
                }

                try {
                    _currentPinging = cancellation;
                    OnPropertyChanged(nameof(PingingInProcess));
                    SetPinged(List.Count(x => x.Status != ServerStatus.Unloaded));

                    var w = Stopwatch.StartNew();
                    var pingedNow = 0;

                    for (var i = 0; Pinged < List.Count && i < 10 && !linked.IsCancellationRequested; i++) {
                        if (i > 0) {
                            Logging.Write("Not everying was pinged in the previous iteration, let’s try again");
                        }

                        while (GameWrapper.IsInGame && SettingsHolder.Online.PausePingingInRace) {
                            await Task.Delay(TimeSpan.FromSeconds(1d));
                        }

                        var ordered = List.OrderByDescending(x => (priorityFilter?.Test(x) == true ? 1000 : 0) + x.ConnectedDrivers).ToList();
                        await ordered.Select
                                (async x => {
                                    // ReSharper disable once AccessToDisposedClosure
                                    if (linked.IsCancellationRequested) return;
                                    if (GameWrapper.IsInGame && SettingsHolder.Online.PausePingingInRace) return;
                                    if (x.Status == ServerStatus.Unloaded) {
                                        await x.Update(ServerEntry.UpdateMode.Lite);
                                        SetPinged(Pinged + 1);
                                        pingedNow++;
                                    }
                                }).WhenAll(SettingsHolder.Online.PingingSingleSocket ? 25 : SettingsHolder.Online.PingConcurrency, linked.Token);
                        SetPinged(List.Count(x => x.Status != ServerStatus.Unloaded));
                    }

                    if (!linked.IsCancellationRequested && pingedNow > 0) {
                        Logging.Write($"Pinging {pingedNow} servers: {w.Elapsed.TotalMilliseconds:F2} ms");
                    }
                } finally {
                    if (ReferenceEquals(_currentPinging, cancellation)) {
                        _currentPinging = null;
                        OnPropertyChanged(nameof(PingingInProcess));
                    }

                    linked.Dispose();
                    cancellation.Dispose();
                }
            }
        }
    }
}