using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Tools.Managers.Online {
    public partial class OnlineManager {
        private int _pinged;

        public int Pinged {
            get { return _pinged; }
            set {
                if (Equals(value, _pinged)) return;
                _pinged = value;
                OnPropertyChanged();
            }
        }

        public bool PingingInProcess => _currentPinging != null;

        public void StopPinging() {
            _currentPinging?.Cancel();
            _currentPinging = null;
            OnPropertyChanged(nameof(PingingInProcess));
        }

        private CancellationTokenSource _currentPinging;

        public async Task PingEverything([CanBeNull] IFilter<ServerEntry> priorityFilter, CancellationToken cancellationToken = default(CancellationToken)) {
            StopPinging();

            using (var cancellation = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellation.Token)) {
                _currentPinging = cancellation;
                OnPropertyChanged(nameof(PingingInProcess));

                Pinged = List.Count(x => x.Status != ServerStatus.Unloaded);

                var w = Stopwatch.StartNew();
                await (priorityFilter == null ? List : List.Where(priorityFilter.Test).Concat(List.Where(x => !priorityFilter.Test(x)))).Select(async x => {
                    // ReSharper disable once AccessToDisposedClosure
                    if (linked.IsCancellationRequested) return;

                    if (x.Status == ServerStatus.Unloaded) {
                        await x.Update(ServerEntry.UpdateMode.Lite);
                        Pinged++;
                    }
                }).WhenAll(SettingsHolder.Online.PingConcurrency, linked.Token);
                if (linked.IsCancellationRequested) return;

                if (Pinged > 0) {
                    Logging.Write($"Pinging {Pinged} servers: {w.Elapsed.TotalMilliseconds:F2} ms");
                }

                if (ReferenceEquals(_currentPinging, cancellation)) {
                    _currentPinging = null;
                    OnPropertyChanged(nameof(PingingInProcess));
                }
            }
        }
    }
}