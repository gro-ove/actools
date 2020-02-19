using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgSessionEnded : PluginMessage {
        #region As-binary-members; we should reuse them exactly this way to stay efficient
        public string ReportFileName { get; set; }
        #endregion

        public MsgSessionEnded()
                : base(ACSProtocol.MessageType.ACSP_END_SESSION) { }

        protected internal override void Serialize(System.IO.BinaryWriter bw) {
            WriteStringW(bw, ReportFileName);
        }

        protected internal override void Deserialize(System.IO.BinaryReader br) {
            ReportFileName = ReadStringW(br);
        }
    }
}