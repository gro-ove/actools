using System;
using System.Linq;
using System.Runtime.InteropServices;
using HidLibrary;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class SimuCube : IWheelSteerLockSetter {
        public virtual string ControllerName => "SimuCUBE";

        public WheelOptionsBase GetOptions() {
            return null;
        }

        public virtual bool Test(string productGuid) {
            return string.Equals(productGuid, "0D5A16D0-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        public int MaximumSteerLock => 65535;
        public int MinimumSteerLock => 40;

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

        [StructLayout(LayoutKind.Sequential, Pack = 1),]
        struct CommandPacket {
            public ScReportId ReportId;
            public ScCommand Command;
            public ScValue1 Value1;
            public ushort Value2;

            public static readonly int Size = Marshal.SizeOf(typeof(CommandPacket));
        }

        public bool Apply(int steerLock, bool isReset, out int appliedValue) {
            appliedValue = Math.Min(Math.Max(steerLock, MinimumSteerLock), MaximumSteerLock);

            var packet = new CommandPacket {
                ReportId = ScReportId.Out,
                Command = ScCommand.SetTemporaryVariable,
                Value1 = isReset ? ScValue1.UnsetTemporarySteeringAngle : ScValue1.TemporarySteeringAngle,
                Value2 = (ushort)appliedValue
            };

            var ptr = Marshal.AllocHGlobal(CommandPacket.Size);
            var data = new byte[60];
            Marshal.StructureToPtr(packet, ptr, false);
            Marshal.Copy(ptr, data, 0, CommandPacket.Size);
            Marshal.FreeHGlobal(ptr);

            var result = HidDevices.Enumerate(0x16d0, 0x0d5a).Aggregate(false, (a, b) => {
                using (b) {
                    AcToolsLogging.Write($"Set to {steerLock}: " + b.DevicePath);
                    return a | b.Write(data);
                }
            });

            return result;
        }
    }
}