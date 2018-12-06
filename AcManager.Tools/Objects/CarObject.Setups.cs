using System;
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

        private bool _acdDataRead ;
        private DataWrapper _acdData;
        private readonly object _acdDataSync = new object();

        /// <summary>
        /// Null only if data is damaged so much it’s unreadable.
        /// </summary>
        [CanBeNull]
        public DataWrapper AcdData {
            get {
                if (_acdDataRead) return _acdData;
                lock (_acdDataSync) {
                    try {
                        _acdData = DataWrapper.FromCarDirectory(Location);
                        _acdDataRead = true;
                        return _acdData;
                    } catch (Exception e) {
                        NonfatalError.NotifyBackground("Can’t read data", e);
                        _acdDataRead = true;
                        return null;
                    }
                }
            }
        }
    }
}
