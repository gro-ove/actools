using System;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class ThrustmasterT300 : ThrustmasterT500 {
        public override string ControllerName => "Thrustmaster T300RS";

        public override IWheelSteerLockSetter Test(string productGuid) {
            return string.Equals(productGuid, "B66E044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase) ? this : null;
        }

        protected override int ProductId => 0xb66e;
    }
}