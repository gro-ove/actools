using AcManager.Tools.AcPlugins.Helpers;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgClientEvent : PluginMessage {
        public byte Subtype { get; set; }
        public byte CarId { get; set; }
        public byte OtherCarId { get; set; }

        public float RelativeVelocity { get; set; }
        public Vector3F WorldPosition { get; set; }
        public Vector3F RelativePosition { get; set; }

        public MsgClientEvent()
                : base(ACSProtocol.MessageType.ACSP_CLIENT_EVENT) { }

        public MsgClientEvent(MsgClientEvent copy)
                : base(ACSProtocol.MessageType.ACSP_CLIENT_EVENT) {
            Subtype = copy.Subtype;
            CarId = copy.CarId;
            OtherCarId = copy.OtherCarId;
            RelativeVelocity = copy.RelativeVelocity;
            WorldPosition = copy.WorldPosition; // wrong, should really copy this
            RelativePosition = copy.RelativePosition; // wrong, should really copy this
        }

        protected internal override void Serialize(System.IO.BinaryWriter bw) {
            bw.Write(Subtype);
            bw.Write(CarId);
            if (Subtype == (byte)ACSProtocol.MessageType.ACSP_CE_COLLISION_WITH_CAR)
                bw.Write(OtherCarId);

            bw.Write(RelativeVelocity);

            WriteVector3F(bw, WorldPosition);
            WriteVector3F(bw, RelativePosition);
        }

        protected internal override void Deserialize(System.IO.BinaryReader br) {
            Subtype = br.ReadByte();
            CarId = br.ReadByte();
            if (Subtype == (byte)ACSProtocol.MessageType.ACSP_CE_COLLISION_WITH_CAR)
                OtherCarId = br.ReadByte();
            RelativeVelocity = br.ReadSingle();
            WorldPosition = ReadVector3F(br);
            RelativePosition = ReadVector3F(br);
        }
    }
}