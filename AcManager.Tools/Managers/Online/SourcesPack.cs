using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class SourcesPack : NotifyPropertyChanged, IDisposable {
        public static int OptionConcurrency = 4;

        private readonly OnlineSourceWrapper[] _sources;

        internal SourcesPack(IEnumerable<OnlineSourceWrapper> sources) {
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

                if (!value && Status == OnlineManagerStatus.Ready) {
                    Ready?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool LoadingComplete => Status == OnlineManagerStatus.Ready && !BackgroundLoading;

        private ErrorInformation _error;

        [CanBeNull]
        public ErrorInformation Error {
            get { return _error; }
            set => Apply(value, ref _error);
        }

        private void UpdateStatus() {
            ErrorInformation error = null;
            var waiting = false;
            var loading = false;
            var background = false;
            var errorless = 0;

            foreach (var source in _sources) {
                switch (source.Status) {
                    case OnlineManagerStatus.Loading:
                        ++errorless;
                        if (source.BackgroundLoading) {
                            background = true;
                        } else {
                            loading = true;
                        }
                        break;
                    case OnlineManagerStatus.Error:
                        error = source.Error;
                        break;
                    case OnlineManagerStatus.Waiting:
                        ++errorless;
                        waiting = true;
                        break;
                    case OnlineManagerStatus.Ready:
                        ++errorless;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (errorless > 0) {
                error = null;
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

        public Task EnsureLoadedAsync(CancellationToken cancellation = default) {
            return LoadingComplete || Status == OnlineManagerStatus.Error ? Task.Delay(0, cancellation) :
                    _sources.Select(x => x.EnsureLoadedAsync(cancellation)).WhenAll(OptionConcurrency, cancellation);
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
        public async Task ReloadAsync(bool nonReadyOnly = false, CancellationToken cancellation = default) {
            if (Status == OnlineManagerStatus.Loading) return;

            var filter = nonReadyOnly ? (Func<OnlineSourceWrapper, bool>)(x => x.Status != OnlineManagerStatus.Ready) : x => true;
            foreach (var source in _sources.Where(x => x.IsBackgroundLoadable).Where(filter)) {
                source.ReloadAsync(false, cancellation).Ignore();
            }

            await _sources.Where(x => !x.IsBackgroundLoadable).Where(filter).Select(x => x.ReloadAsync(false, cancellation))
                                  .WhenAll(OptionConcurrency, cancellation);
        }

        public void Dispose() {
            foreach (var source in _sources) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(source, nameof(INotifyPropertyChanged.PropertyChanged),
                        SourceWrapper_PropertyChanged);
            }
        }
    }
}