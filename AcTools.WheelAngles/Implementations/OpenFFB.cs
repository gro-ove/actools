using System;
using System.Linq;
using System.Runtime.InteropServices;
using HidLibrary;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class OpenFFB : IWheelSteerLockSetter {
        public virtual string ControllerName => "Open FFBoard";

        public WheelOptionsBase GetOptions() 
        {
            return null;
        }

        public virtual bool Test(string productGuid) 
        {
            return string.Equals(productGuid, "FFB01209-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        public int MaximumSteerLock => 32767;
        public int MinimumSteerLock => 40;

        private enum OFFBReportId : byte
        {
            Out = 0xAF,
            Angle = 0x21
        }

        private enum OFFBHidCmdType : byte
        {
            write = 0, request = 1, err = 2
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1),]
        struct CommandPacket
        {
            public OFFBReportId ReportId;
            public OFFBHidCmdType type;
            public UInt32 Command;
            public UInt32 addr;
            public UInt64 value;

            public static readonly int Size = Marshal.SizeOf(typeof(CommandPacket));
        }

        public bool Apply(int steerLock, bool isReset, out int appliedValue)
        {

            appliedValue = Math.Min(Math.Max(steerLock, MinimumSteerLock), MaximumSteerLock);
    
            var packet = new CommandPacket
            {
                ReportId    = OFFBReportId.Out,
                type        = OFFBHidCmdType.write,
                Command     = (UInt32)OFFBReportId.Angle, // set degrees
                addr        = 0,    // Axis = 0
                value       = (UInt64)appliedValue
            };

            var ptr = Marshal.AllocHGlobal(CommandPacket.Size);
            var data = new byte[64];
            Marshal.StructureToPtr(packet, ptr, false);
            Marshal.Copy(ptr, data, 0, CommandPacket.Size);
            Marshal.FreeHGlobal(ptr);

            var result = HidDevices.Enumerate(0x1209, 0xFFB0).Aggregate(false, (a, b) => {
                using (b)
                {
                    AcToolsLogging.Write($"Set to {steerLock}: " + b.DevicePath);
                    return a | b.Write(data);
                }
            });

            return result;
        }
    }
}