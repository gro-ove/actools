using System;
using System.Linq;
using System.Runtime.InteropServices;
using HidLibrary;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class FanatecCSW : FanatecDD2 {
        public override string ControllerName => "Fanatec ClubSport";

        public new int MaximumSteerLock => 900;

        public override bool Test(string productGuid)
        {
            return string.Equals(productGuid, "038E0EB7-0000-0000-0000-504944564944",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(productGuid, "00040EB7-0000-0000-0000-504944564944",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(productGuid, "00010EB7-0000-0000-0000-504944564944",
                    StringComparison.OrdinalIgnoreCase);
        }

        private readonly int[] _productIds = { 0x038E, 0x0004, 0x0001 };
    }
}