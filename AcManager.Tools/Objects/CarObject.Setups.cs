using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private WeakReference<CarSetupsManager> _setupsManager;

        [CanBeNull]
        public CarSetupsManager GetSetupsManagerIfInitialized() {
            return _setupsManager != null && _setupsManager.TryGetTarget(out var result) ? result : null;
        }

        [NotNull]
        public CarSetupsManager SetupsManager {
            get {
                if (_setupsManager != null && _setupsManager.TryGetTarget(out var result)) {
                    return result;
                }

                result = CarSetupsManager.Create(this);
                _setupsManager = new WeakReference<CarSetupsManager>(result);
                return result;
            }
        }

        public void UpdateAcdData() {
            _acdData = null;
            OnPropertyChanged(nameof(AcdData));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool _acdDataRead;
        private Task<DataWrapper> _acdDataTask;
        private DataWrapper _acdData;

        /// <summary>
        /// Null only if data is damaged so much it’s unreadable.
        /// </summary>
        [CanBeNull]
        public DataWrapper AcdData => _acdDataRead ? _acdData : GetAcdDataAsync().Result;

        [ItemCanBeNull]
        public Task<DataWrapper> GetAcdDataAsync() {
            if (_acdDataRead) return Task.FromResult(_acdData);
            return _acdDataTask ?? (_acdDataTask = LoadAsync());

            async Task<DataWrapper> LoadAsync() {
                try {
                    _acdData = await Task.Run(() => DataWrapper.FromCarDirectory(Location)).ConfigureAwait(false);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t read data", e);
                    _acdDataRead = true;
                    return null;
                }
                _acdDataRead = true;
                _acdDataTask = null;
                return _acdData;
            }
        }
    }
}