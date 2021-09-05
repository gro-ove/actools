using System.Runtime.InteropServices;

namespace AcManager.Tools.AcPlugins.CspCommands {
    // Command sent from server to client, if values are not fitting, AC will close with a message.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandHandshakeIn : ICspCommand {
        public uint MinVersion; // build code
        public bool RequiresWeatherFX;

        ushort ICspCommand.GetMessageType() {
            return 0;
        }
    };
}