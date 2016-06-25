using System;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private WeakReference<CarSetupsManager> _setupsManager;

        [CanBeNull]
        public CarSetupsManager GetSetupsManagerIfInitialized() {
            CarSetupsManager result;
            return _setupsManager != null && _setupsManager.TryGetTarget(out result) ? result : null;
        }

        [NotNull]
        public CarSetupsManager SetupsManager {
            get {
                CarSetupsManager result;
                if (_setupsManager != null && _setupsManager.TryGetTarget(out result)) {
                    return result;
                }

                result = CarSetupsManager.Create(this);
                _setupsManager = new WeakReference<CarSetupsManager>(result);
                return result;
            }
        }

        private DataWrapper _data;

        public DataWrapper Data {
            get {
                if (_data != null) return _data;
                _data = DataWrapper.FromFile(Location);
                return _data;
            }
        }
    }
}
