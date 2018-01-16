using System;

namespace AcTools.WheelAngles.Implementations {
    internal class LogitechG29 : LogitechG25 {
        public override string ControllerName => "Logitech G29";

        public override bool Test(string productGuid) {
            return string.Equals(productGuid, "C24F046D-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }
    }
}