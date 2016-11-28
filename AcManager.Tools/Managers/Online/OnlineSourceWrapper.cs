using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Managers.Online {
    public class OnlineSourceWrapper : NotifyPropertyChanged, IProgress<AsyncProgressEntry> {
        private readonly IList<ServerEntry> _list;
        private readonly IOnlineSource _source;

        public OnlineSourceWrapper(IList<ServerEntry> list, IOnlineSource source) {
            _list = list;
            _source = source;
        }

        private OnlineManagerStatus _status = OnlineManagerStatus.Waiting;

        public OnlineManagerStatus Status {
            get { return _status; }
            set {
                if (Equals(value, _status)) return;
                _status = value;
                LoadingProgress = AsyncProgressEntry.Ready;
                OnPropertyChanged();
            }
        }

        private AsyncProgressEntry _loadingProgress;

        public AsyncProgressEntry LoadingProgress {
            get { return _loadingProgress; }
            set {
                if (Equals(value, _loadingProgress)) return;
                _loadingProgress = value;
                OnPropertyChanged();
            }
        }

        public bool IsBackgroundLoadable => _source.IsBackgroundLoadable;

        public string Key => _source.Key;

        private Task _loadingTask;
        private CancellationToken _cancellation;

        private Task LoadAsync(CancellationToken cancellation) {
            _cancellation = cancellation;
            return _loadingTask ?? (_loadingTask = EnsureLoadedInner());
        }

        public Task EnsureLoadedAsync(CancellationToken cancellation = default(CancellationToken)) {
            return Status == OnlineManagerStatus.Ready || Status == OnlineManagerStatus.Error ? Task.Delay(0, cancellation) :
                    LoadAsync(cancellation);
        }

        public async Task<bool> ReloadAsync(CancellationToken cancellation = default(CancellationToken)) {
            if (Status == OnlineManagerStatus.Loading) {
                return false;
            }

            CleanUp();
            await LoadAsync(cancellation);
            return true;
        }

        private void CleanUp() {
            var count = _list.Count;
            for (var i = count - 1; i >= 0; i--) {
                var entry = _list[i];
                if (entry.RemoveOrigin(_source.Key)) {
                    _list.RemoveAt(i);
                }
            }
        }

        public void Report(AsyncProgressEntry value) {
            LoadingProgress = value;
        }

        private void Add(ServerInformation information) {
            var id = information.GetUniqueId();
            var existing = _list.GetByIdOrDefault(id);
            if (existing == null) {
                var entry = new ServerEntry(information);
                entry.SetOrigin(Key);
                _list.Add(entry);
            } else {
                existing.SetOrigin(Key);
                existing.Update(information);
            }
        }

        private async Task EnsureLoadedInner() {
            Status = OnlineManagerStatus.Loading;
            try {
                CleanUp();
                await _source.LoadAsync(Add, this, _cancellation);
                Status = OnlineManagerStatus.Ready;
            } catch (Exception e) {
                Logging.Warning(e);
                Status = OnlineManagerStatus.Error;
            } finally {
                _loadingTask = null;
                _cancellation = default(CancellationToken);
            }
        }
    }
}