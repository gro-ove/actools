using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgChat : PluginMessage {
        public MsgChat()
                : base(ACSProtocol.MessageType.ACSP_CHAT) { }

        #region Members as binary
        public byte CarId { get; private set; }

        public string Message { get; private set; }
        #endregion

        public bool IsCommand {
            get {
                if (string.IsNullOrWhiteSpace(Message))
                    return false;
                return Message.StartsWith("/");
            }
        }

        protected internal override void Deserialize(BinaryReader br) {
            CarId = br.ReadByte();
            Message = ReadStringW(br);
        }

        protected internal override void Serialize(BinaryWriter bw) {
            bw.Write(CarId);
            bw.Write(Message);
        }
    }
}