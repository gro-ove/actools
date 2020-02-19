using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class RequestBroadcastChat : PluginMessage {
        public string ChatMessage { get; set; }

        public RequestBroadcastChat()
                : base(ACSProtocol.MessageType.ACSP_BROADCAST_CHAT) { }

        protected internal override void Deserialize(BinaryReader br) {
            ChatMessage = ReadStringW(br);
        }

        protected internal override void Serialize(BinaryWriter bw) {
            WriteStringW(bw, ChatMessage);
        }
    }
}