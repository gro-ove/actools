using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Tools.Managers.Online {
    public abstract partial class BaseOnlineManager {
        private int _pinged;

        public int Pinged {
            get { return _pinged; }
            set {
                if (Equals(value, _pinged)) return;
                _pinged = value;
                OnPropertyChanged();
            }
        }

        private bool _pingingInProcess;

        public bool PingingInProcess {
            get { return _pingingInProcess; }
            private set {
                if (Equals(value, _pingingInProcess)) return;
                _pingingInProcess = value;
                OnPropertyChanged();
            }
        }

        private int _pinging;

        public void StopPinging() {
            if (PingingInProcess) {
                _pinging++;
                PingingInProcess = false;
            }
        }

        public async Task PingEverything([CanBeNull] IFilter<ServerEntry> priorityFilter, CancellationToken cancellation = default(CancellationToken)) {
            if (PingingInProcess) return;

            Pinged = List.Count(x => x.Status != ServerStatus.Unloaded);
            var pinging = ++_pinging;

            try {
                PingingInProcess = true;
                var w = Stopwatch.StartNew();

                await (priorityFilter == null ? List : List.Where(priorityFilter.Test).Concat(List.Where(x => !priorityFilter.Test(x)))).Select(async x => {
                    if (cancellation.IsCancellationRequested || pinging != _pinging) return;
                    if (x.Status == ServerStatus.Unloaded) {
                        await x.Update(ServerEntry.UpdateMode.Lite);
                        Pinged++;
                    }
                }).WhenAll(SettingsHolder.Online.PingConcurrency, cancellation);
                if (cancellation.IsCancellationRequested) return;

                if (Pinged > 0) {
                    Logging.Write($"Pinging {Pinged} servers: {w.Elapsed.TotalMilliseconds:F2} ms");
                }
            } finally {
                if (pinging == _pinging) {
                    PingingInProcess = false;
                }
            }
        }
    }
}