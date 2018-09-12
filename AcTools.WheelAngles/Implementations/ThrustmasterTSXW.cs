using System;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class ThrustmasterTSXW : ThrustmasterT500 {
        public override string ControllerName => "Thrustmaster TS-XW Racer";

        public override bool Test(string productGuid) {
            return string.Equals(productGuid, "B692044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        protected override int ProductId => 0xb692;
    }
}