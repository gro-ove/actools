using System;
using System.Linq;
using System.Runtime.InteropServices;
using HidLibrary;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class Fanatec : IWheelSteerLockSetter {
        public virtual string ControllerName => "Fanatec DD2";

        public WheelOptionsBase GetOptions() {
            return null;
        }

        public virtual bool Test(string productGuid) {
            return string.Equals(productGuid, "00070EB7-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        public int MaximumSteerLock => 65535;
        public int MinimumSteerLock => 90;

        private enum ScReportId : byte {
            Out = 0x6B
        }

        private enum ScCommand : byte {
            SetTemporaryVariable = 100
        }

        private enum ScValue1 : ushort {
            TemporarySteeringAngle = 1,
            UnsetTemporarySteeringAngle = 2
        }
        
        public bool Apply(int steerLock, bool isReset, out int appliedValue) {
            appliedValue = Math.Min(Math.Max(steerLock, MinimumSteerLock), MaximumSteerLock);

            var cmd = new byte[] { 0x01, 0xf8, 0x81 }.Concat(BitConverter.GetBytes(appliedValue)).ToArray();

            var result = HidDevices.Enumerate(0x0eb7, 0x0007).Aggregate(false, (a, b) => {
                using (b) {
                    AcToolsLogging.Write($"Set to {steerLock}: " + b.DevicePath);
                    return a | b.Write(cmd);
                }
            });

            return result;
        }
    }
}
