using System.Runtime.InteropServices;

namespace AcManager.Tools.AcPlugins.CspCommands {
    // Command to test signature.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandSignatureIn : ICspCommand, ICspVariableCommand {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 160)]
        public string Value;

        ushort ICspCommand.GetMessageType() {
            return 2;
        }
    }
}