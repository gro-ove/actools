using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgNewConnection : PluginMessage {
        public string DriverName { get; set; }
        public string DriverGuid { get; set; }
        public byte CarId { get; set; }
        public string CarModel { get; set; }
        public string CarSkin { get; set; }

        public MsgNewConnection()
                : base(ACSProtocol.MessageType.ACSP_NEW_CONNECTION) { }

        protected internal override void Deserialize(BinaryReader br) {
            DriverName = ReadStringW(br);
            DriverGuid = ReadStringW(br);
            CarId = br.ReadByte();
            CarModel = ReadString(br);
            CarSkin = ReadString(br);
        }

        protected internal override void Serialize(BinaryWriter bw) {
            WriteStringW(bw, DriverName);
            WriteStringW(bw, DriverGuid);
            bw.Write(CarId);
            WriteStringW(bw, CarModel);
            WriteStringW(bw, CarSkin);
        }
    }
}