using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgCarInfo : PluginMessage {
        public byte CarId { get; set; }
        public bool IsConnected { get; set; }
        public string CarModel { get; set; }
        public string CarSkin { get; set; }
        public string DriverName { get; set; }
        public string DriverTeam { get; set; }
        public string DriverGuid { get; set; }

        public MsgCarInfo()
                : base(ACSProtocol.MessageType.ACSP_CAR_INFO) { }

        protected internal override void Serialize(System.IO.BinaryWriter bw) {
            bw.Write(CarId);
            bw.Write(IsConnected);
            WriteStringW(bw, CarModel);
            WriteStringW(bw, CarSkin);
            WriteStringW(bw, DriverName);
            WriteStringW(bw, DriverTeam);
            WriteStringW(bw, DriverGuid);
        }

        protected internal override void Deserialize(System.IO.BinaryReader br) {
            CarId = br.ReadByte();
            IsConnected = br.ReadBoolean();

            CarModel = ReadStringW(br);
            CarSkin = ReadStringW(br);
            DriverName = ReadStringW(br);
            DriverTeam = ReadStringW(br);
            DriverGuid = ReadStringW(br);
        }
    }
}