using System;
using System.Linq;
using System.Runtime.InteropServices;
using HidLibrary;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class FanatecCSLE : FanatecDD2 {
        public override string ControllerName => "Fanatec CSL Elite";

        public override bool Test(string productGuid)
        {
            return string.Equals(productGuid, "00050EB7-0000-0000-0000-504944564944",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(productGuid, "0E030EB7-0000-0000-0000-504944564944",
                    StringComparison.OrdinalIgnoreCase);
        }
        private readonly int[] _productIds = { 0x0005, 0x0E03 };

    }
}
