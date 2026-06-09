using System.Runtime.InteropServices;

namespace AcManager.Tools.AcPlugins.CspCommands {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandOverrideBanMessage : ICspCommand, ICspVariableCommand {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string Value;

        ushort ICspCommand.GetMessageType() {
            return 101;
        }
    }
}