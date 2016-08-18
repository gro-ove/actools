using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Lists;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using StringBasedFilter;

namespace AcManager.Tools.Managers.Online {
    public interface IOnlineManager : IAcManagerNew { }

    public abstract class BaseOnlineManager : AsyncScanAcManager<ServerEntry>, IOnlineManager {
        public BetterObservableCollection<string> UnavailableList { get; } = new BetterObservableCollection<string>();

        public static BaseOnlineManager ManagerByMode(OnlineManagerType type) {
            switch (type) {
                case OnlineManagerType.Online:
                    if (OnlineManager.Instance == null) {
                        OnlineManager.Initialize();
                    }

                    return OnlineManager.Instance;

                case OnlineManagerType.Lan:
                    if (LanManager.Instance == null) {
                        LanManager.Initialize();
                    }

                    return LanManager.Instance;

                case OnlineManagerType.Recent:
                    if (RecentManager.Instance == null) {
                        RecentManager.Initialize();
                    }

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

        public async Task PingEverything(IFilter<ServerEntry> priorityFilter, CancellationToken cancellation = default(CancellationToken)) {
            if (PingingInProcess) return;
            Pinged = LoadedOnly.Count(x => x.Status != ServerStatus.Unloaded);

            try {
                PingingInProcess = true;
                var w = Stopwatch.StartNew();

                if (priorityFilter != null) {
                    await LoadedOnly.Where(priorityFilter.Test).Select(async x => {
                        if (cancellation.IsCancellationRequested) return;
                        if (x.Status == ServerStatus.Unloaded) {
                            await x.Update(ServerEntry.UpdateMode.Lite);
                            Pinged++;
                        }
                    }).WhenAll(SettingsHolder.Online.PingConcurrency, cancellation);
                    UpdateList();

                    if (cancellation.IsCancellationRequested) return;
                }

                await LoadedOnly.Select(async x => {
                    if (cancellation.IsCancellationRequested) return;
                    if (x.Status == ServerStatus.Unloaded) {
                        await x.Update(ServerEntry.UpdateMode.Lite);
                        Pinged++;
                    }
                }).WhenAll(SettingsHolder.Online.PingConcurrency, cancellation);
                UpdateList();

                if (Pinged > 0) {
                    Logging.Write($"[OnlineManager] Pinging {Pinged} servers: {w.Elapsed.TotalMilliseconds:F2} ms");
                }
            } finally {
                PingingInProcess = false;
            }
        }

        private ICommand _refreshCommand;

        public ICommand RefreshListCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand(o => {
            InnerWrappersList.Clear();
            return RescanAsync();
        }, o => !ErrorFatal));
    }
}
