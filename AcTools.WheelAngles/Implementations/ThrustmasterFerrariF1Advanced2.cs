using System;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class ThrustmasterFerrariF1Advanced2 : ThrustmasterT500 {
        public override string ControllerName => "Thrustmaster Ferrari F1 Advanced";

        public override bool Test(string productGuid) {
            return string.Equals(productGuid, "B66F044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        protected override int ProductId => 0xb66f;
    }
}