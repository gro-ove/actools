using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgClientLoaded : PluginMessage {
        public MsgClientLoaded()
                : base(ACSProtocol.MessageType.ACSP_CLIENT_LOADED) { }

        #region members as binary
        public byte CarId { get; private set; }
        #endregion

        protected internal override void Deserialize(BinaryReader br) {
            CarId = br.ReadByte();
        }

        protected internal override void Serialize(BinaryWriter bw) {
            bw.Write(CarId);
        }
    }
}