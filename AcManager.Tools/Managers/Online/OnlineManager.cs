using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public enum OnlineManagerStatus {
        Error, Loading, Ready
    }

    public abstract class BaseOnlineManager : NotifyPropertyChanged {
        public ChangeableObservableCollection<ServerEntry> List { get; } = new ChangeableObservableCollection<ServerEntry>();

        private OnlineManagerStatus _status;

        public OnlineManagerStatus Status {
            get { return _status; }
            set {
                if (Equals(value, _status)) return;
                _status = value;
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

        private DelegateCommand _refreshListCommand;

        public DelegateCommand RefreshListCommand => _refreshListCommand ?? (_refreshListCommand = new DelegateCommand(() => {
            ;
        }, () => true));

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
    }

    public class OnlineManager : BaseOnlineManager {
        private static OnlineManager _instance;

        public static OnlineManager Instance => _instance ?? (_instance = new OnlineManager());

        protected override async Task InnerLoadAsync() {
            Status = OnlineManagerStatus.Loading;

            try {
                var data = await Task.Run(() => KunosApiProvider.TryToGetList()?.Select(x => new ServerEntry(x)).ToList());
                if (data != null) {
                    List.ReplaceEverythingBy(data);
                }

                Status = OnlineManagerStatus.Ready;
            } catch (Exception) {
                Status = OnlineManagerStatus.Error;
            }
        }
    }
}