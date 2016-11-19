using System;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public abstract partial class BaseOnlineManager : NotifyPropertyChanged {
        public ChangeableObservableCollection<ServerEntry> List { get; } = new ChangeableObservableCollection<ServerEntry>();

        private OnlineManagerStatus _status;

        public OnlineManagerStatus Status {
            get { return _status; }
            set {
                if (Equals(value, _status)) return;
                _status = value;
                LoadingState = null;
                OnPropertyChanged();
                _reloadCommand?.RaiseCanExecuteChanged();
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

        private string _loadingState;

        public string LoadingState {
            get { return _loadingState; }
            set {
                if (Equals(value, _loadingState)) return;
                _loadingState = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoaded;

        public bool IsLoaded {
            get { return _isLoaded; }
            set {
                if (Equals(value, _isLoaded)) return;
                _isLoaded = value;
                OnPropertyChanged();
            }
        }

        private Task _loadingTask;

        public async Task EnsureLoadedAsync() {
            if (IsLoaded) return;

            if (_loadingTask != null) {
                await _loadingTask;
            } else {
                try {
                    _loadingTask = InnerLoadAsync();
                    await _loadingTask;

                    IsLoaded = true;
                } finally {
                    _loadingTask = null;
                }
            }
        }

        protected Task ReloadAsync() {
            StopPinging();
            List.Clear();
            return InnerLoadAsync();
        }

        protected abstract Task InnerLoadAsync();

        public static BaseOnlineManager ManagerByMode(OnlineManagerType type) {
            switch (type) {
                case OnlineManagerType.Online:
                    return OnlineManager.Instance;

                case OnlineManagerType.Lan:
                    return OnlineManager.Instance; // TODO

                case OnlineManagerType.Recent:
                    return OnlineManager.Instance; // TODO
            }

            throw new ArgumentOutOfRangeException();
        }

        [CanBeNull]
        public ServerEntry GetById(string id) {
            return List.GetByIdOrDefault(id);
        }

        private AsyncCommand _reloadCommand;

        public AsyncCommand ReloadCommand => _reloadCommand ?? (_reloadCommand = new AsyncCommand(ReloadAsync, () => Status != OnlineManagerStatus.Loading));
    }
}