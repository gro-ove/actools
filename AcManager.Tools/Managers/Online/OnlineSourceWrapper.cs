using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class ErrorInformation : NotifyPropertyChanged {
        public string Message { get; }

        public string Commentary { get; }

        public bool IsFatal { get; }

        public ErrorInformation(Exception e) {
            var informative = e as InformativeException;
            if (informative != null) {
                Message = informative.Message;
                Commentary = informative.SolutionCommentary;
            } else {
                Message = e.Message;
                IsFatal = true;
            }
        }

        protected bool Equals(ErrorInformation other) {
            return string.Equals(Message, other.Message) && string.Equals(Commentary, other.Commentary) && IsFatal == other.IsFatal;
        }

        public override bool Equals(object obj) {
            return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((ErrorInformation)obj));
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = Message?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (Commentary?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ IsFatal.GetHashCode();
                return hashCode;
            }
        }
    }

    public class OnlineSourceWrapper : NotifyPropertyChanged, IProgress<AsyncProgressEntry>, IWithId {
        public string Id => _source.Id;

        public static bool OptionWeakListening = true;

        private readonly IList<ServerEntry> _list;
        private readonly IOnlineSource _source;

        public OnlineSourceWrapper([NotNull] IList<ServerEntry> list, [NotNull] IOnlineSource source) {
            _list = list;
            _source = source;

            if (OptionWeakListening) {
                WeakEventManager<IOnlineSource, EventArgs>.AddHandler(_source, nameof(IOnlineSource.Obsolete), OnSourceObsolete);
            } else {
                _source.Obsolete += OnSourceObsolete;
            }
        }

        private OnlineManagerStatus _status = OnlineManagerStatus.Waiting;

        public OnlineManagerStatus Status {
            get { return _status; }
            set {
                if (!Equals(value, _status)) {
                    _status = value;
                    LoadingProgress = AsyncProgressEntry.Ready;
                    OnPropertyChanged();
                }

                if (value != OnlineManagerStatus.Error) {
                    Error = null;
                }
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

        private ErrorInformation _error;

        [CanBeNull]
        public ErrorInformation Error {
            get { return _error; }
            set {
                if (!Equals(value, _error)) {
                    _error = value;
                    OnPropertyChanged();
                }

                if (value != null) {
                    Status = OnlineManagerStatus.Error;
                }
            }
        }

        public bool IsBackgroundLoadable => _source is IOnlineBackgroundSource;

        public string Key => _source.Id;

        public string DisplayName => _source.DisplayName;

        private Task _loadingTask;
        private bool _reloadAfterwards;

        private void OnSourceObsolete(object sender, EventArgs e) {
            if (Status == OnlineManagerStatus.Loading) {
                _reloadAfterwards = true;
            } else {
                ReloadAsync().Forget();
            }
        }

        public Task EnsureLoadedAsync(CancellationToken cancellation = default(CancellationToken)) {
            return Status == OnlineManagerStatus.Ready || Status == OnlineManagerStatus.Error ? Task.Delay(0, cancellation) :
                    LoadAsync(cancellation);
        }

        public async Task<bool> ReloadAsync(CancellationToken cancellation = default(CancellationToken)) {
            if (Status == OnlineManagerStatus.Loading) {
                _reloadAfterwards = true;
                if (_loadingTask == null) {
                    Logging.Unexpected();
                    return false;
                }

                await _loadingTask;
            } else {
                await LoadAsync(cancellation);
            }

            return true;
        }

        private Task LoadAsync(CancellationToken cancellation) {
            _cancellation = cancellation;
            return _loadingTask ?? (_loadingTask = LoadAsyncInner());
        }

        private void CleanUp() {
            var count = _list.Count;
            for (var i = count - 1; i >= 0; i--) {
                var entry = _list[i];
                if (entry.RemoveOrigin(_source.Id)) {
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
                existing.UpdateValues(information);
            }
        }

        private void Add(IEnumerable<ServerInformation> informations) {
            var newEntries = new List<ServerEntry>(300);
            foreach (var information in informations) {
                var id = information.GetUniqueId();
                var existing = _list.GetByIdOrDefault(id);
                if (existing == null) {
                    var entry = new ServerEntry(information);
                    entry.SetOrigin(Key);
                    newEntries.Add(entry);
                } else {
                    existing.SetOrigin(Key);
                    existing.UpdateValues(information);
                }
            }
            
            var target = _list as ChangeableObservableCollection<ServerEntry>;
            if (target == null || newEntries.Count < 10) {
                foreach (var entry in newEntries) {
                    _list.Add(entry);
                }
                return;
            }
            
            target._AddRangeDirect(newEntries);
        }

        private Task GetLoadInnerTask(CancellationToken cancellation) {
            var list = _source as IOnlineListSource;
            if (list != null) {
                return list.LoadAsync(x => {
                    if (cancellation.IsCancellationRequested) return;
                    Add(x);
                }, this, cancellation);
            }

            var background = _source as IOnlineBackgroundSource;
            if (background != null) {
                return background.LoadAsync(x => {
                    if (cancellation.IsCancellationRequested) return;
                    Add(x);
                }, this, cancellation);
            }

            throw new NotSupportedException($@"Not supported type: {_source.GetType().Name}");
        }

        private CancellationToken? _cancellation;

        private async Task LoadAsyncInner() {
            _reloadAfterwards = false;
            Status = OnlineManagerStatus.Loading;

            try {
                CleanUp();
                await GetLoadInnerTask(_cancellation ?? default(CancellationToken));

                while (_reloadAfterwards) {
                    CleanUp();
                    await GetLoadInnerTask(_cancellation ?? default(CancellationToken));
                }

                Status = OnlineManagerStatus.Ready;
            } catch (InformativeException e) {
                Error = new ErrorInformation(e);
            } catch (Exception e) {
                Error = new ErrorInformation(e);
            } finally {
                _loadingTask = null;
                _cancellation = null;
                _cancellation = default(CancellationToken);
            }
        }
    }
}