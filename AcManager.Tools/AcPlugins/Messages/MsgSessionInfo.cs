using System;
using AcManager.Tools.AcPlugins.Kunos;
using AcTools.Processes;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgSessionInfo : PluginMessage {
        #region As-binary-members; we should reuse them exactly this way to stay efficient
        public byte Version { get; set; }
        public string ServerName { get; set; }
        public string TrackConfig { get; set; }
        public string Track { get; set; }
        public string Name { get; set; }
        public Game.SessionType SessionType { get; set; }
        public ushort SessionDuration { get; set; }
        public ushort Laps { get; set; }
        public ushort WaitTime { get; set; }
        public byte AmbientTemp { get; set; }
        public byte RoadTemp { get; set; }
        public string Weather { get; set; }

        /// <summary>
        /// Milliseconds from the start (this might be negative for races with WaitTime)
        /// </summary>
        public int ElapsedMS { get; private set; }
        #endregion

        #region wellformed stuff members - offer some more comfortable data conversion
        public TimeSpan SessionDurationTimespan {
            get { return TimeSpan.FromMinutes(SessionDuration); }

            set { SessionDuration = Convert.ToUInt16(Math.Round(value.TotalMinutes, 0)); }
        }

        /// <summary>
        /// The index of the session in the message
        /// </summary>
        public byte SessionIndex { get; private set; }

        /// <summary>
        /// The index of the current session in the server
        /// </summary>
        public byte CurrentSessionIndex { get; private set; }

        /// <summary>
        /// The number of sessions in the server
        /// </summary>
        public byte SessionCount { get; private set; }
        #endregion

        public MsgSessionInfo()
                : base(ACSProtocol.MessageType.ACSP_SESSION_INFO) { }

        public MsgSessionInfo(ACSProtocol.MessageType overridingNewSessionFlag)
                : base(ACSProtocol.MessageType.ACSP_NEW_SESSION) {
            if (overridingNewSessionFlag != ACSProtocol.MessageType.ACSP_NEW_SESSION)
                throw new Exception("MsgSessionInfo's type may only be overriden by ACSP_NEW_SESSION");
        }

        protected internal override void Deserialize(System.IO.BinaryReader br) {
            Version = br.ReadByte();
            SessionIndex = br.ReadByte();
            CurrentSessionIndex = br.ReadByte();
            SessionCount = br.ReadByte();
            ServerName = ReadStringW(br);
            Track = ReadString(br);
            TrackConfig = ReadString(br);
            Name = ReadString(br);
            SessionType = (Game.SessionType)br.ReadByte();
            SessionDuration = br.ReadUInt16();
            Laps = br.ReadUInt16();
            WaitTime = br.ReadUInt16();
            AmbientTemp = br.ReadByte();
            RoadTemp = br.ReadByte();
            Weather = ReadString(br);
            ElapsedMS = br.ReadInt32();
        }

        protected internal override void Serialize(System.IO.BinaryWriter bw) {
            bw.Write(Version);
            bw.Write(SessionIndex);
            bw.Write(CurrentSessionIndex);
            bw.Write(SessionCount);
            WriteStringW(bw, ServerName);
            WriteString(bw, Track);
            WriteString(bw, TrackConfig);
            WriteString(bw, Name);
            bw.Write((byte)SessionType);
            bw.Write(SessionDuration);
            bw.Write(Laps);
            bw.Write(WaitTime);
            bw.Write(AmbientTemp);
            bw.Write(RoadTemp);
            WriteString(bw, Weather);
            bw.Write(ElapsedMS);
        }

        public RequestSetSession CreateSetSessionRequest() {
            return new RequestSetSession() {
                Laps = Laps,
                SessionName = Name,
                SessionIndex = SessionIndex,
                SessionType = SessionType,
                Time = SessionDuration,
                WaitTime = WaitTime,
            };
        }
    }
}