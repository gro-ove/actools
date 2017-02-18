using System;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public abstract class CarSpecificHelperBase : Game.RaceIniProperties, IDisposable {
        private bool _requiresDisposal;

        public sealed override void Set(IniFile file) {
            try {
                var carId = file["RACE"].GetNonEmpty("MODEL");
                var car = carId == null ? null : CarsManager.Instance.GetById(carId);
                _requiresDisposal = car != null && SetOverride(car);
            } catch (Exception e) {
                Logging.Warning($"[{GetType().Name}] Set(): " + e);
                _requiresDisposal = false;
            }
        }

        protected abstract bool SetOverride(CarObject car);

        public void Dispose() {
            if (_requiresDisposal) {
                try {
                    DisposeOverride();
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }

        protected abstract void DisposeOverride();
    }
}