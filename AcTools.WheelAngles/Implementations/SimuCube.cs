using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using HidLibrary;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class SimuCube : IWheelSteerLockSetter {
        public virtual string ControllerName => "SimuCUBE";
        private int _lockedPid;

        public WheelOptionsBase GetOptions() {
            return null;
        }

        private static readonly Dictionary<string, int> KnownGuiDs =new Dictionary<string, int> {
            ["0D5A16D0-0000-0000-0000-504944564944"] = 0x0D5A,
            ["0D5F16D0-0000-0000-0000-504944564944"] = 0X0D5F,
            ["0D6016D0-0000-0000-0000-504944564944"] = 0x0D60,
            ["0D6116D0-0000-0000-0000-504944564944"] = 0x0D61,
            ["0D6616D0-0000-0000-0000-504944564944"] = 0x0D66,
        };

        public virtual IWheelSteerLockSetter Test(string productGuid) {
            return KnownGuiDs.Where(p => string.Equals(p.Key, productGuid, StringComparison.CurrentCultureIgnoreCase))
                    .Select(p => new SimuCube { _lockedPid = p.Value })
                    .FirstOrDefault();
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
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

            return HidDevices.Enumerate(0x16d0, _lockedPid != 0 ? new[] { _lockedPid } : KnownGuiDs.Values.ToArray())
                    .Aggregate(false, (a, b) => {
                        using (b) {
                            AcToolsLogging.Write($"Set to {steerLock}: " + b.DevicePath);
                            return a | b.Write(data);
                        }
                    });
        }
    }
}