using System.IO;
using AcManager.Tools.AcPlugins.Helpers;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgCarUpdate : PluginMessage {
        #region As-binary-members; we should reuse them exactly this way to stay efficient
        public byte CarId { get; set; }
        public Vector3F WorldPosition { get; set; }
        public Vector3F Velocity { get; set; }
        public byte Gear { get; set; }
        public ushort EngineRpm { get; set; }
        public float NormalizedSplinePosition { get; set; }
        #endregion

        public MsgCarUpdate()
                : base(ACSProtocol.MessageType.ACSP_CAR_UPDATE) { }

        protected internal override void Deserialize(BinaryReader br) {
            CarId = br.ReadByte();
            WorldPosition = ReadVector3F(br);
            Velocity = ReadVector3F(br);
            Gear = br.ReadByte();
            EngineRpm = br.ReadUInt16();
            NormalizedSplinePosition = br.ReadSingle();
        }

        protected internal override void Serialize(BinaryWriter bw) {
            bw.Write(CarId);
            WriteVector3F(bw, WorldPosition);
            WriteVector3F(bw, Velocity);
            bw.Write(Gear);
            bw.Write(EngineRpm);
            bw.Write(NormalizedSplinePosition);
        }
    }
}