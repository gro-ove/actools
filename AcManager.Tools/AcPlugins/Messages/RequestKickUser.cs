using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class RequestKickUser : PluginMessage {
        public byte CarId { get; set; }

        public RequestKickUser()
                : base(ACSProtocol.MessageType.ACSP_KICK_USER) { }

        protected internal override void Deserialize(BinaryReader br) {
            CarId = br.ReadByte();
        }

        protected internal override void Serialize(BinaryWriter bw) {
            bw.Write(CarId);
        }
    }
}