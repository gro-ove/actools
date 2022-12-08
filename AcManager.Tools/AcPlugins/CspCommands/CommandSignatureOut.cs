using System.Runtime.InteropServices;

namespace AcManager.Tools.AcPlugins.CspCommands {
    // Response testing signature.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandSignatureOut : ICspCommand, ICspVariableCommand {
        public ulong UniqueKey;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 160)]
        public byte[] Value;

        ushort ICspCommand.GetMessageType() {
            return 3;
        }
    }
}