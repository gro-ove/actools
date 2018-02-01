using System;
using AcTools.Utils.Helpers;
using AcTools.WheelAngles;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools {
    internal class WheelAnglesStorage : IWheelSteerLockOptionsStorage {
        public void LoadValues(WheelOptionsBase obj) {
            try {
                var j = ValuesStorage.Get<string>("HardwareLockOptions:" + (obj.GetType().FullName ?? @"Unknown"));
                if (j != null) {
                    _busy = true;
                    JObject.Parse(j).Populate(obj);
                }
            } catch (Exception e) {
                Logging.Error(e);
            } finally {
                _busy = false;
            }
        }

        private bool _busy;

        public void StoreValues(WheelOptionsBase obj) {
            if (_busy) return;
            ValuesStorage.Set("HardwareLockOptions:" + (obj.GetType().FullName ?? @"Unknown"), JsonConvert.SerializeObject(obj));
        }
    }
}