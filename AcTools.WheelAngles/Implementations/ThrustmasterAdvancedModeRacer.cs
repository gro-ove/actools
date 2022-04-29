using System;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class ThrustmasterAdvancedModeRacer : ThrustmasterT500 {
        public override string ControllerName => "Thrustmaster Advanced Mode Racer";

        public override bool Test(string productGuid) {
            return string.Equals(productGuid, "B696044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        protected override int ProductId => 0xB696;
    }
}