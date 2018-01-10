using System;
using System.Linq;
using HidLibrary;

namespace AcTools.WheelAngles.Implementations {
    internal class ThrustmasterT300 : ThrustmasterT500 {
        public override string ControllerName => "Thrustmaster T300RS";

        public override bool Test(string productGuid) {
            return string.Equals(productGuid, "B66E044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        protected override int ProductId => 0xb66e;
    }
}