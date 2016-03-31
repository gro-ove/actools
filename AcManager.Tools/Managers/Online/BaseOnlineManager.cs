using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Lists;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
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

        public static int OptionConcurrentThreadsNumber = 30;

        protected BaseOnlineManager() {
        }
        
        protected override ServerEntry CreateAcObject(string id, bool enabled) {
            throw new NotSupportedException();
        }

        protected ServerEntry CreateAndAddEntry(ServerInformation information, bool withPastLoad = true) {
            var entry = new ServerEntry(this, information);
            if (GetById(entry.Id) != null) throw new Exception("ID is taken");

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

        private bool _pingEverythingInProcess;

        public async Task PingEverything(IFilter<ServerEntry> priorityFilter, CancellationToken cancellation = default(CancellationToken)) {
            Pinged = 0;
            if (_pingEverythingInProcess) {
                return;
            }
            _pingEverythingInProcess = true;

            if (priorityFilter != null) {
                await TaskExtension.WhenAll(LoadedOnly.Where(priorityFilter.Test).Select(async x => {
                    if (x.Status == ServerStatus.Unloaded) {
                        await x.Update(ServerEntry.UpdateMode.Lite);
                    }
                    Pinged++;
                }), OptionConcurrentThreadsNumber, cancellation);
            }

            await TaskExtension.WhenAll(LoadedOnly.Select(async x => {
                if (x.Status == ServerStatus.Unloaded) {
                    await x.Update(ServerEntry.UpdateMode.Lite);
                }
                Pinged++;
            }), OptionConcurrentThreadsNumber, cancellation);

            _pingEverythingInProcess = false;
        }

        private ICommand _refreshCommand;

        public ICommand RefreshListCommand => _refreshCommand ?? (_refreshCommand = new RelayCommand(o => {
            // RefreshList().Forget();
            // TODO
        }, o => !ErrorFatal));
    }
}
