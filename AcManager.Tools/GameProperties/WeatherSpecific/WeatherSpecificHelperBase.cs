using System;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public abstract class WeatherSpecificHelperBase : Game.RaceIniProperties, IDisposable {
        private bool _requiresDisposal;

        public sealed override void Set(IniFile file) {
            try {
                var remote = file["REMOTE"];
                if (remote.GetBool("ACTIVE", false) && !remote.GetBool("__CM_EXTENDED", false) ||
                        file["BENCHMARK"].GetBool("ACTIVE", false)) return;

                var weatherId = file["WEATHER"].GetNonEmpty("NAME");
                var weather = weatherId == null ? null : WeatherManager.Instance.GetById(weatherId);
                _requiresDisposal = weather != null && SetOverride(weather);
            } catch (Exception e) {
                Logging.Warning($"[{GetType().Name}] Set(): " + e);
                _requiresDisposal = false;
            }
        }

        protected abstract bool SetOverride(WeatherObject weather);

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