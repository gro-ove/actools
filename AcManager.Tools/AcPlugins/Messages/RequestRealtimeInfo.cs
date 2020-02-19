using System;
using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class RequestRealtimeInfo : PluginMessage {
        public UInt16 Interval { get; set; }

        public RequestRealtimeInfo()
                : base(ACSProtocol.MessageType.ACSP_REALTIMEPOS_INTERVAL) { }

        protected internal override void Deserialize(BinaryReader br) {
            Interval = br.ReadUInt16();
        }

        protected internal override void Serialize(BinaryWriter bw) {
            bw.Write(Interval);
        }
    }
}