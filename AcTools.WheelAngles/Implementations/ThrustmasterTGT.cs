using System;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class ThrustmasterTGT : ThrustmasterT500 {
        public override string ControllerName => "Thrustmaster T-GT";

        public override IWheelSteerLockSetter Test(string productGuid) {
            return string.Equals(productGuid, "B684044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase) ? this : null;
        }

        protected override int ProductId => 0xb684;
    }
}