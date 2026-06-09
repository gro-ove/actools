using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AcTools.Utils.Helpers;
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
            return _instances.Select(x => x.Test(productGuid)).NonNull().FirstOrDefault();
        }

        public static bool IsSupported([CanBeNull] string productGuid) {
            Initialize();
            return _instances.Any(x => x.Test(productGuid) != null);
        }

        public static bool IsSupported([CanBeNull] string productGuid, [CanBeNull] out WheelOptionsBase options) {
            Initialize();
            var setter = Get(productGuid);
            options = setter?.GetOptions();
            options?.EnsureLoaded();
            return setter != null;
        }

        [NotNull]
        public static IEnumerable<string> GetSupportedNames() {
            Initialize();
            return _instances.Select(x => x.ControllerName);
        }
    }
}
