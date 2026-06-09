using System;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class ThrustmasterT150 : ThrustmasterT500 {
        public override string ControllerName => "Thrustmaster T150";

        public override IWheelSteerLockSetter Test(string productGuid) {
            return string.Equals(productGuid, "B677044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase) ? this : null;
        }

        protected override int ProductId => 0xb66e;
    }
}