using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgVersionInfo : PluginMessage {
        #region As-binary-members; we should reuse them exactly this way to stay efficient
        public byte Version { get; set; }
        #endregion

        public MsgVersionInfo()
                : base(ACSProtocol.MessageType.ACSP_VERSION) { }

        protected internal override void Deserialize(BinaryReader br) {
            Version = br.ReadByte();
        }

        protected internal override void Serialize(BinaryWriter bw) {
            bw.Write(Version);
        }
    }
}