using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace AcTools.WheelAngles {
    public static class WheelSteerLock {
        private static IWheelSteerLockSetter[] _instances;

        private static void Initialize() {
            if (_instances == null) {
                _instances = Assembly.GetExecutingAssembly().GetTypes()
                                     .Where(x => !x.IsAbstract && x.GetInterfaces().Contains(typeof(IWheelSteerLockSetter)))
                                     .Select(x => (IWheelSteerLockSetter)Activator.CreateInstance(x)).ToArray();
            }
        }

        [CanBeNull]
        public static IWheelSteerLockSetter Get([CanBeNull] string productGuid) {
            Initialize();
            return _instances.FirstOrDefault(x => x.Test(productGuid));
        }

        public static bool IsSupported([CanBeNull] string productGuid) {
            Initialize();
            return _instances.Any(x => x.Test(productGuid));
        }

        [NotNull]
        public static IEnumerable<string> GetSupportedNames() {
            Initialize();
            return _instances.Select(x => x.ControllerName);
        }
    }
}
