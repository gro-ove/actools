using System;
using System.IO;
using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class RequestSessionInfo : PluginMessage {
        /// <summary>
        /// ACSP_GET_SESSION_INFO gets a session index you want to get or a -1 for the current session 
        /// </summary>
        public Int16 SessionIndex { get; set; }

        public RequestSessionInfo()
                : base(ACSProtocol.MessageType.ACSP_GET_SESSION_INFO) { }

        protected internal override void Deserialize(BinaryReader br) {
            SessionIndex = br.ReadInt16();
        }

        protected internal override void Serialize(BinaryWriter bw) {
            bw.Write(SessionIndex);
        }
    }
}