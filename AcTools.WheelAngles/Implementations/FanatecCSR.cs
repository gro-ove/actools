using System;
using System.Linq;
using System.Runtime.InteropServices;
using HidLibrary;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class FanatecCSR : FanatecDD2 {
        public override string ControllerName => "Fanatec Forza Motorsport CSR (Elite)";

        public new int MaximumSteerLock => 900;

        public override bool Test(string productGuid)
        {
            return string.Equals(productGuid, "00110EB7-0000-0000-0000-504944564944",
                    StringComparison.OrdinalIgnoreCase);
        }

        private readonly int[] _productIds = { 0x0011 };
    }
}