using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Dialogs {
    public class AllowedHelper<T> : IDisposable where T : AcCommonObject {
        private readonly int _hiddenCars;
        private readonly AcManagerNew<T> _manager;

        private static uint GetCrc32(string id) {
            return Crc32Algorithm.Compute(Encoding.UTF8.GetBytes(id));
        }

        public AllowedHelper(AcManagerNew<T> manager, HashSet<uint> allowedCarIds){
            _manager = manager;
            if (allowedCarIds != null) {
                foreach (var c in manager) {
                    c.NotAllowedToSelect = !allowedCarIds.Contains(GetCrc32(c.Id));
                    if (c.NotAllowedToSelect) ++_hiddenCars;
                }
                if (_hiddenCars != 0) {
                    AcPlaceholderNew.AnyNotAllowedToSelect = true;
                    if (_hiddenCars == manager.Count()) {
                        Logging.Warning("No items are available, data could be damaged, disabling the filter");
                        _hiddenCars = 0;
                        foreach (var c in manager) {
                            c.NotAllowedToSelect = false;
                        }
                        AcPlaceholderNew.AnyNotAllowedToSelect = false;
                    }
                }
            }
        }
            
        public int HiddenCount => _hiddenCars;

        public T GetDefaultItem() {
            var def = _manager.GetDefault();
            return def?.NotAllowedToSelect != false ? _manager.FirstOrDefault(x => !x.NotAllowedToSelect) : def;
        }

        public void SetDefaultIfNull(ref T item) {
            if (item == null || item.NotAllowedToSelect) {
                item = GetDefaultItem();
            }
        }
            
        public void Dispose() {
            if (_hiddenCars > 0) {
                AcPlaceholderNew.AnyNotAllowedToSelect = false;
                foreach (var c in _manager) {
                    c.NotAllowedToSelect = false;
                }
            }
        }
    }
}