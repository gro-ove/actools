using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils;
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

                    if (value == OnlineManagerStatus.Loading) {
                        LoadingProgress = AsyncProgressEntry.Indetermitate;
                    }
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

        private void OnSourceObsolete(object sender, EventArgs e) {
            ReloadAsync().Forget();
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

        public Task EnsureLoadedAsync(CancellationToken cancellation = default(CancellationToken)) {
            Logging.Write($"({_source.Id}) customerId: {cancellation.GetHashCode()}");
            return Status == OnlineManagerStatus.Ready || Status == OnlineManagerStatus.Error ? Task.Delay(0, cancellation) :
                    LoadAsync(cancellation);
        }

        public Task ReloadAsync(CancellationToken cancellation = default(CancellationToken)) {
            Logging.Write($"({_source.Id}) customerId: {cancellation.GetHashCode()}");
            return Status == OnlineManagerStatus.Loading ? ReloadLater(cancellation) : LoadAsync(cancellation);
        }
        
        private Task _loadingTask;
        private bool _reloadAfterwards;

        private Task ReloadLater(CancellationToken cancellation) {
            Logging.Debug(_source.Id);

            if (_loadingTask == null) {
                Logging.Unexpected();
                return Task.Delay(0, cancellation);
            }

            RegisterCustomer(cancellation);
            _reloadAfterwards = true;
            return _loadingTask;
        }

        // During loading, sudden cancellation might occur, but then immediately loading might
        // be started again. To avoid unnecessary lost of almost loaded data, we’ll put all
        // those requests in a special list and will use a local CancellationTokenSource instead.
        // When request will get cancelled, we’ll remove it from that list, wait for a while (for
        // new requests) and then check if the list is empty. If it is, cancellation will be
        // cancelled properly.

        private Task LoadAsync(CancellationToken cancellation) {
            Logging.Write($"({_source.Id}) customerId: {cancellation.GetHashCode()}");
            RegisterCustomer(cancellation);
            return _cancellationSource?.IsCancellationRequested == false ? _loadingTask : (_loadingTask = LoadAsyncInner());
        }

        private readonly List<int> _customers = new List<int>();
        private CancellationTokenSource _cancellationSource;

        private void RegisterCustomer(CancellationToken cancellation) {
            var customerId = cancellation.GetHashCode();
            Logging.Write($"({_source.Id}) customerId: {customerId}; {_customers.Count}");
            _customers.Add(customerId);
            cancellation.Register(() => {
                OnCancelled(customerId).Forget();
            });
        }

        private async Task OnCancelled(int customerId) {
            Logging.Write($"({_source.Id}) customerId: {customerId}; {_customers.Count}");
            if (!_customers.Remove(customerId) || _customers.Count != 0) return;

            await Task.Delay(200);
            if (_customers.Count == 0) {
                Logging.Warning(_cancellationSource.GetHashCode());
                _cancellationSource?.Cancel();
            }
        }

        private async Task LoadAsyncInner() {
            _reloadAfterwards = false;
            Status = OnlineManagerStatus.Loading;

            var cancellationSource = new CancellationTokenSource();
            _cancellationSource = cancellationSource;
            Logging.Warning(_cancellationSource.GetHashCode());

            try {
                CleanUp();
                var ready = await GetLoadInnerTask(cancellationSource.Token);

                while (_reloadAfterwards && !cancellationSource.IsCancellationRequested) {
                    CleanUp();
                    ready = await GetLoadInnerTask(cancellationSource.Token);
                }

                // new LoadAsyncInner() might be started, if this one is cancelled
                if (_cancellationSource == cancellationSource) {
                    // GetLoadInnerTask() returns “false”, if loading was cancelled before all servers
                    // were loaded and ready to be added to the list. In this case, we need to revert state
                    // to Waiting so source will be properly loaded later.
                    Status = ready ? OnlineManagerStatus.Ready : OnlineManagerStatus.Waiting;
                }
            } catch (InformativeException e) {
                Error = new ErrorInformation(e);
            } catch (Exception e) {
                Error = new ErrorInformation(e);
            } finally {
                if (_cancellationSource == cancellationSource) {
                    _cancellationSource = null;
                    _loadingTask = null;
                    _customers.Clear();
                }

                cancellationSource.Dispose();
            }
        }

        private Task<bool> GetLoadInnerTask(CancellationToken cancellation) {
            var list = _source as IOnlineListSource;
            if (list != null) {
                return list.LoadAsync(Add, this, cancellation);
            }

            var background = _source as IOnlineBackgroundSource;
            if (background != null) {
                return background.LoadAsync(Add, this, cancellation);
            }

            throw new NotSupportedException($@"Not supported type: {_source.GetType().Name}");
        }
    }
}
 
 
 
 
 
 