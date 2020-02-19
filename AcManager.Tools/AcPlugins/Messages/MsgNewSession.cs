using AcManager.Tools.AcPlugins.Kunos;

namespace AcManager.Tools.AcPlugins.Messages {
    public class MsgNewSession : MsgSessionInfo {
        public MsgNewSession() {
            Type = ACSProtocol.MessageType.ACSP_NEW_SESSION;
        }
    }
}