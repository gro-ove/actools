using System.IO;
using AcManager.Tools.AcPlugins.Kunos;
using AcTools.Processes;

namespace AcManager.Tools.AcPlugins.Messages {
    public class RequestSetSession : PluginMessage {
        public RequestSetSession()
                : base(ACSProtocol.MessageType.ACSP_SET_SESSION_INFO) { }

        public byte SessionIndex { get; set; }
        public string SessionName { get; set; }
        public Game.SessionType SessionType { get; set; }
        public uint Laps { get; set; }

        /// <summary>
        /// Time (of day?) in seconds
        /// </summary>
        public uint Time { get; set; }

        /// <summary>
        /// Wait time (before race/lock pits in qualifying) in seconds
        /// </summary>
        public uint WaitTime { get; set; }

        protected internal override void Deserialize(BinaryReader br) {
            SessionIndex = br.ReadByte();
            SessionName = ReadStringW(br);
            SessionType = (Game.SessionType)br.ReadByte();
            Laps = br.ReadUInt32();
            Time = br.ReadUInt32();
            WaitTime = br.ReadUInt32();
        }

        protected internal override void Serialize(BinaryWriter bw) {
            // Session Index we want to change, be very careful with changing the current session tho, some stuff might not work as expected
            bw.Write(SessionIndex);

            // Session name
            WriteStringW(bw, SessionName); // Careful here, the server is still broadcasting ASCII strings to the clients for this

            // Session type
            bw.Write((byte)SessionType);

            // Laps
            bw.Write(Laps);

            // Time (in seconds)
            bw.Write(Time);

            // Wait time (in seconds)
            bw.Write(WaitTime);
        }
    }
}