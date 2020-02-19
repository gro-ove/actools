using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class RequestRestartSession : PluginMessage {
        public RequestRestartSession()
                : base(ACSProtocol.MessageType.ACSP_RESTART_SESSION) { }

        protected internal override void Deserialize(BinaryReader br) { }

        protected internal override void Serialize(BinaryWriter bw) { }
    }
}