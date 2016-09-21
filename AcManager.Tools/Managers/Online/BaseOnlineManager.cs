using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using StringBasedFilter;

namespace AcManager.Tools.Managers.Online {
    public interface IOnlineManager : IAcManagerNew { }

    public abstract class BaseOnlineManager : AsyncScanAcManager<ServerEntry>, IOnlineManager {
        public BetterObservableCollection<string> UnavailableList { get; } = new BetterObservableCollection<string>();

        public static BaseOnlineManager ManagerByMode(OnlineManagerType type) {
            switch (type) {
                case OnlineManagerType.Online:
                    return OnlineManager.Instance;

                case OnlineManagerType.Lan:
                    return LanManager.Instance;

                case OnlineManagerType.Recent:
                    return RecentManager.Instance;
            }

            throw new ArgumentOutOfRangeException();
        }
        
        protected override ServerEntry CreateAcObject(string id, bool enabled) {
            throw new NotSupportedException();
        }

        protected ServerEntry CreateAndAddEntry(ServerInformation information, bool withPastLoad = true) {
            var entry = new ServerEntry(this, information);
            if (GetById(entry.Id) != null) throw new Exception(ToolsStrings.Common_IdIsTaken);

            entry.Load();
            if (withPastLoad) {
                entry.PastLoad();
            }
            InnerWrappersList.Add(new AcItemWrapper(this, entry));
            return entry;
        }

        private bool _backgroundLoading;

        public bool BackgroundLoading {
            get { return _backgroundLoading; }
            set {
                if (Equals(value, _backgroundLoading)) return;
                _backgroundLoading = value;
                OnPropertyChanged();
            }
        }

        private bool _errorFatal;

        public bool ErrorFatal {
            get { return _errorFatal; }
            set {
                if (Equals(value, _errorFatal)) return;
                _errorFatal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RefreshListCommand));
            }
        }

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

        public async Task PingEverything(IFilter<ServerEntry> priorityFilter, CancellationToken cancellation = default(CancellationToken)) {
            if (PingingInProcess) return;

            Pinged = LoadedOnly.Count(x => x.Status != ServerStatus.Unloaded);
            var pinging = ++_pinging;

            try {
                PingingInProcess = true;
                var w = Stopwatch.StartNew();

                if (priorityFilter != null) {
                    await LoadedOnly.Where(priorityFilter.Test).Select(async x => {
                        if (cancellation.IsCancellationRequested || pinging != _pinging) return;
                        if (x.Status == ServerStatus.Unloaded) {
                            await x.Update(ServerEntry.UpdateMode.Lite);
                            Pinged++;
                        }
                    }).WhenAll(SettingsHolder.Online.PingConcurrency, cancellation);
                    UpdateList();

                    if (cancellation.IsCancellationRequested) return;
                }

                if (pinging != _pinging) return;
                await LoadedOnly.Select(async x => {
                    if (cancellation.IsCancellationRequested || pinging != _pinging) return;
                    if (x.Status == ServerStatus.Unloaded) {
                        await x.Update(ServerEntry.UpdateMode.Lite);
                        Pinged++;
                    }
                }).WhenAll(SettingsHolder.Online.PingConcurrency, cancellation);

                if (pinging != _pinging) return;
                UpdateList();

                if (Pinged > 0) {
                    Logging.Write($"Pinging {Pinged} servers: {w.Elapsed.TotalMilliseconds:F2} ms");
                }
            } finally {
                if (pinging == _pinging) {
                    PingingInProcess = false;
                }
            }
        }

        private AsyncCommand _refreshCommand;

        public AsyncCommand RefreshListCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand(() => {
            StopPinging();
            InnerWrappersList.Clear();
            return RescanAsync();
        }, () => !ErrorFatal));
    }
}
