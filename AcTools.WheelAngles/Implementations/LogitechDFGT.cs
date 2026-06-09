using System;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class LogitechDFGT : LogitechG25 {
        public override string ControllerName => "Logitech DFGT";

        public override IWheelSteerLockSetter Test(string productGuid) {
            return string.Equals(productGuid, "C29A046D-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase) ? this : null;
        }

        protected override string GetRegistryPath() {
            return @"Software\Logitech\Gaming Software\GlobalDeviceSettings\DrivingForceGT";
        }
    }
}