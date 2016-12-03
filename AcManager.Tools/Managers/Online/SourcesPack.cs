using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class SourcesPack : NotifyPropertyChanged, IDisposable {
        public static int OptionConcurrency = 4;

        private readonly OnlineSourceWrapper[] _sources;

        public SourcesPack(IEnumerable<OnlineSourceWrapper> sources) {
            _sources = sources.ToArray();
            UpdateStatus();

            foreach (var source in _sources) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(source, nameof(INotifyPropertyChanged.PropertyChanged),
                        SourceWrapper_PropertyChanged);
            }
        }

        public IReadOnlyList<OnlineSourceWrapper> SourceWrappers => _sources;

        public event EventHandler Ready;

        private OnlineManagerStatus _status;

        public OnlineManagerStatus Status {
            get { return _status; }
            private set {
                if (Equals(value, _status)) return;
                _status = value;
                OnPropertyChanged();

                if (value == OnlineManagerStatus.Ready) {
                    Ready?.Invoke(this, EventArgs.Empty);
                }
            }
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

        private ErrorInformation _error;

        [CanBeNull]
        public ErrorInformation Error {
            get { return _error; }
            set {
                if (Equals(value, _error)) return;
                _error = value;
                OnPropertyChanged();
            }
        }

        private void UpdateStatus() {
            ErrorInformation error = null;
            var waiting = false;
            var loading = false;
            var background = false;

            foreach (var source in _sources) {
                switch (source.Status) {
                    case OnlineManagerStatus.Loading:
                        if (source.IsBackgroundLoadable) {
                            background = true;
                        } else {
                            loading = true;
                        }
                        break;
                    case OnlineManagerStatus.Error:
                        error = source.Error;
                        break;
                    case OnlineManagerStatus.Waiting:
                        waiting = true;
                        break;
                }
            }

            if (loading) {
                Status = OnlineManagerStatus.Loading;
                BackgroundLoading = false;
            } else {
                Status = error != null ? OnlineManagerStatus.Error : waiting ? OnlineManagerStatus.Waiting : OnlineManagerStatus.Ready;
                BackgroundLoading = background;
            }

            Error = error;
        }

        private static readonly string[] ForceSources = {
            FileBasedOnlineSources.FavoritesKey,
            FileBasedOnlineSources.RecentKey
        };

        public Task EnsureLoadedAsync(CancellationToken cancellation = default(CancellationToken)) {
            if (Status == OnlineManagerStatus.Ready || Status == OnlineManagerStatus.Error) {
                return Task.Delay(0, cancellation);
            }

            foreach (var source in _sources.Where(x => x.IsBackgroundLoadable)) {
                source.EnsureLoadedAsync(cancellation).Forget();
            }

            return _sources.Where(x => !x.IsBackgroundLoadable).Select(x => x.EnsureLoadedAsync(cancellation))
                           .Concat(ForceSources.Select(x => _sources.GetByIdOrDefault(x) != null
                                   ? null : OnlineManager.Instance.GetWrappedSource(x)?.EnsureLoadedAsync(cancellation)).NonNull())
                           .WhenAll(OptionConcurrency, cancellation);
        }

        private void SourceWrapper_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(OnlineSourceWrapper.Status):
                    UpdateStatus();
                    break;
            }
        }

        /// <summary>
        /// Reloads data from associated sources.
        /// </summary>
        /// <returns>True if data was reloaded, false if reloading isn’t possible at the moment (for example,
        /// when loading is still in process)</returns>
        public async Task<bool> ReloadAsync(bool nonReadyOnly = false, CancellationToken cancellation = default(CancellationToken)) {
            if (Status == OnlineManagerStatus.Loading) {
                return false;
            }

            var filter = nonReadyOnly ? (Func<OnlineSourceWrapper, bool>)(x => x.Status != OnlineManagerStatus.Ready) : x => true;
            foreach (var source in _sources.Where(x => x.IsBackgroundLoadable).Where(filter)) {
                source.ReloadAsync(cancellation).Forget();
            }

            return (await _sources.Where(x => !x.IsBackgroundLoadable).Where(filter).Select(x => x.ReloadAsync(cancellation))
                                  .WhenAll(OptionConcurrency, cancellation)).All(x => x);
        }

        // private AsyncCommand _reloadCommand;

        // public AsyncCommand ReloadCommand => _reloadCommand ?? (_reloadCommand = new AsyncCommand(ReloadAsync, () => Status != OnlineManagerStatus.Loading));

        public void Dispose() {
            foreach (var source in _sources) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(source, nameof(INotifyPropertyChanged.PropertyChanged),
                        SourceWrapper_PropertyChanged);
            }
        }
    }
}