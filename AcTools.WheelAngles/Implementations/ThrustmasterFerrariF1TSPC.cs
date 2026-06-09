using System;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class ThrustmasterFerrariF1TSPC : ThrustmasterT500 {
        public override string ControllerName => "Thrustmaster Ferrari F1 TS-PC Racer";

        public override IWheelSteerLockSetter Test(string productGuid) {
            return string.Equals(productGuid, "B68A044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase) ? this : null;
        }

        protected override int ProductId => 0xb689;
    }
}