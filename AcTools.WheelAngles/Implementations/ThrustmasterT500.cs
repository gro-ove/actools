using System;
using System.Linq;
using HidLibrary;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class ThrustmasterT500 : IWheelSteerLockSetter {
        public virtual string ControllerName => "Thrustmaster T500RS";

        public WheelOptionsBase GetOptions() {
            return null;
        }

        public virtual IWheelSteerLockSetter Test(string productGuid) {
            return string.Equals(productGuid, "B65E044F-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase) ? this : null;
        }

        protected virtual int ProductId => 0xb65e;

        public int MaximumSteerLock => 1080;
        public int MinimumSteerLock => 40;

        public bool Apply(int steerLock, bool isReset, out int appliedValue) {
            appliedValue = Math.Min(Math.Max(steerLock, MinimumSteerLock), MaximumSteerLock);
            var cmd = new byte[] { 0x40, 0x11 }.Concat(BitConverter.GetBytes((ushort)(65535d / 1080d * (appliedValue - 1)))).ToArray();
            return HidDevices.Enumerate(0x44f, ProductId).Aggregate(false, (a, b) => {
                using (b) {
                    AcToolsLogging.Write($"Set to {steerLock}: " + b.DevicePath);
                    return a | b.Write(cmd);
                }
            });
        }
    }
}