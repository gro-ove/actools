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
            Out = 0xA1
        }
        private enum OFFBClsId : UInt16
        {
            
            AxisCls = 0xA01,
            FxCls = 0xA02
        }

        private enum OFFBCmdId : UInt32
        {
            PowerCmd = 0x00,
            AngleCmd = 0x01
        }

        

        private enum OFFBHidCmdType : byte
        {
            write = 0, request = 1, info = 2, writeAddr = 3, requestAddr = 4
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1),]
        struct CommandPacket
        {
            public OFFBReportId ReportId;
            public OFFBHidCmdType type;
            public OFFBClsId cls;
            public byte inst;
            public OFFBCmdId Command;
            public UInt64 value;
            public UInt64 addr;

            public static readonly int Size = Marshal.SizeOf(typeof(CommandPacket));
        }

        public bool Apply(int steerLock, bool isReset, out int appliedValue)
        {

            appliedValue = Math.Min(Math.Max(steerLock, MinimumSteerLock), MaximumSteerLock);
    
            var packet = new CommandPacket
            {
                ReportId    = OFFBReportId.Out,
                type        = OFFBHidCmdType.write,
                cls         = OFFBClsId.AxisCls,
                inst        = 0, // First axis
                Command     = OFFBCmdId.AngleCmd, // set degrees
                value       = (UInt64)appliedValue,
                addr        = 0
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