using System.Runtime.InteropServices;

namespace AcManager.Tools.AcPlugins.CspCommands {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandSignatureVerificationIn : ICspCommand, ICspVariableCommand {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 160)]
        public string Value;

        ushort ICspCommand.GetMessageType() {
            return 4;
        }
    }
}