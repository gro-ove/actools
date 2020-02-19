using System;
using System.IO;
using AcManager.Tools.AcPlugins.Kunos;
using AcManager.Tools.AcPlugins.Messages;

namespace AcManager.Tools.AcPlugins.Helpers {
    public class AcMessageParser {
        public static PluginMessage Parse(TimestampedBytes rawMessage) {
            var rawData = rawMessage.RawData;

            if (rawData == null) {
                throw new ArgumentNullException();
            }

            if (rawData.Length == 0) {
                throw new ArgumentException("rawData is empty");
            }

            ACSProtocol.MessageType msgType;
            try {
                msgType = (ACSProtocol.MessageType)rawData[0];
            } catch (Exception) {
                throw new Exception("Message of not implemented type: " + rawData[0]);
            }

            var newMsg = CreateInstance(msgType);
            newMsg.CreationDate = rawMessage.IncomingDate;
            using (var m = new MemoryStream(rawData))
            using (var br = new BinaryReader(m)) {
                if (br.ReadByte() != (byte)newMsg.Type) {
                    throw new Exception("Can’t parse the message properly: " + newMsg.GetType().Name);
                }
                newMsg.Deserialize(br);
            }

            return newMsg;
        }

        private static PluginMessage CreateInstance(ACSProtocol.MessageType msgType) {
            switch (msgType) {
                case ACSProtocol.MessageType.ACSP_VERSION:
                    return new MsgVersionInfo();
                case ACSProtocol.MessageType.ACSP_SESSION_INFO:
                    return new MsgSessionInfo();
                case ACSProtocol.MessageType.ACSP_NEW_SESSION:
                    return new MsgSessionInfo(ACSProtocol.MessageType.ACSP_NEW_SESSION);
                case ACSProtocol.MessageType.ACSP_NEW_CONNECTION:
                    return new MsgNewConnection();
                case ACSProtocol.MessageType.ACSP_CONNECTION_CLOSED:
                    return new MsgConnectionClosed();
                case ACSProtocol.MessageType.ACSP_CAR_UPDATE:
                    return new MsgCarUpdate();
                case ACSProtocol.MessageType.ACSP_CAR_INFO:
                    return new MsgCarInfo();
                case ACSProtocol.MessageType.ACSP_LAP_COMPLETED:
                    return new MsgLapCompleted();
                case ACSProtocol.MessageType.ACSP_END_SESSION:
                    return new MsgSessionEnded();
                case ACSProtocol.MessageType.ACSP_CLIENT_EVENT:
                    return new MsgClientEvent();
                case ACSProtocol.MessageType.ACSP_REALTIMEPOS_INTERVAL:
                    return new RequestRealtimeInfo();
                case ACSProtocol.MessageType.ACSP_GET_CAR_INFO:
                    return new RequestCarInfo();
                case ACSProtocol.MessageType.ACSP_SEND_CHAT:
                    return new RequestSendChat();
                case ACSProtocol.MessageType.ACSP_BROADCAST_CHAT:
                    return new RequestBroadcastChat();
                case ACSProtocol.MessageType.ACSP_ADMIN_COMMAND:
                    return new RequestAdminCommand();
                case ACSProtocol.MessageType.ACSP_NEXT_SESSION:
                    return new RequestNextSession();
                case ACSProtocol.MessageType.ACSP_RESTART_SESSION:
                    return new RequestRestartSession();
                case ACSProtocol.MessageType.ACSP_CHAT:
                    return new MsgChat();
                case ACSProtocol.MessageType.ACSP_GET_SESSION_INFO:
                    return new RequestSessionInfo();
                case ACSProtocol.MessageType.ACSP_CLIENT_LOADED:
                    return new MsgClientLoaded();
                case ACSProtocol.MessageType.ACSP_SET_SESSION_INFO:
                    return new RequestSetSession();
                case ACSProtocol.MessageType.ACSP_ERROR:
                    return new MsgError();
                case ACSProtocol.MessageType.ACSP_KICK_USER:
                    return new RequestKickUser();
                case ACSProtocol.MessageType.ERROR_BYTE:
                    throw new Exception("CreateInstance: MessageType is not set or wrong (ERROR_BYTE=0)");
                case ACSProtocol.MessageType.ACSP_CE_COLLISION_WITH_CAR:
                case ACSProtocol.MessageType.ACSP_CE_COLLISION_WITH_ENV:
                    throw new Exception("CreateInstance: MessageType " + msgType
                            + " is not meant to be used as MessageType, but as Subtype to ACSP_CLIENT_EVENT");
                default:
                    throw new Exception("MessageType " + msgType + " is not known or implemented");
            }
        }
    }
}