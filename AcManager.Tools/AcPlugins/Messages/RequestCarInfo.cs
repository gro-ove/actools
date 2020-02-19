using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class RequestCarInfo : PluginMessage {
        public byte CarId { get; set; }

        public RequestCarInfo()
                : base(ACSProtocol.MessageType.ACSP_GET_CAR_INFO) { }

        protected internal override void Deserialize(BinaryReader br) {
            CarId = br.ReadByte();
        }

        protected internal override void Serialize(BinaryWriter bw) {
            bw.Write(CarId);
        }
    }
}