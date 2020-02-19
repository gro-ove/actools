using System.Collections.Generic;
using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgLapCompleted : PluginMessage {
        public byte CarId { get; set; }
        public uint LapTime { get; set; }
        public byte Cuts { get; set; }

        public byte LeaderboardSize => (byte)Leaderboard.Count;

        public List<MsgLapCompletedLeaderboardEnty> Leaderboard { get; set; }
        public float GripLevel { get; set; }

        public MsgLapCompleted()
                : base(ACSProtocol.MessageType.ACSP_LAP_COMPLETED) {
            Leaderboard = new List<MsgLapCompletedLeaderboardEnty>();
        }

        protected internal override void Deserialize(BinaryReader br) {
            CarId = br.ReadByte();
            LapTime = br.ReadUInt32();
            Cuts = br.ReadByte();

            var leaderboardCount = br.ReadByte();
            Leaderboard.Clear();
            for (int i = 0; i < leaderboardCount; i++) {
                Leaderboard.Add(MsgLapCompletedLeaderboardEnty.FromBinaryReader(br));
            }

            GripLevel = br.ReadSingle();
        }

        protected internal override void Serialize(BinaryWriter bw) {
            bw.Write(CarId);
            bw.Write(LapTime);
            bw.Write(Cuts);
            bw.Write(LeaderboardSize);

            foreach (var entry in Leaderboard) {
                entry.Serialize(bw);
            }

            bw.Write(GripLevel);
        }
    }
}