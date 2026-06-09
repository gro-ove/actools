using System;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class ThrustmasterF1 : ThrustmasterT500 {
        public override string ControllerName => "Thrustmaster F1";

        public override IWheelSteerLockSetter Test(string productGuid) {
            return string.Equals(productGuid, "B662044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase) ? this : null;
        }

        protected override int ProductId => 0xb662;
    }
}