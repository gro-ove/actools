namespace AcManager.Tools.AcPlugins.Kunos {
    public class ACSProtocol {
        public enum MessageType : byte {
            // careful, we'll use byte as underlying datatype so we can just write the Type into the BinaryReader
            ERROR_BYTE = 0,
            ACSP_NEW_SESSION = 50,
            ACSP_NEW_CONNECTION = 51,
            ACSP_CONNECTION_CLOSED = 52,
            ACSP_CAR_UPDATE = 53,
            ACSP_CAR_INFO = 54, // Sent as response to ACSP_GET_CAR_INFO command
            ACSP_END_SESSION = 55,
            ACSP_LAP_COMPLETED = 73,

            // EVENTS
            ACSP_CLIENT_EVENT = 130,

            // EVENT TYPES
            ACSP_CE_COLLISION_WITH_CAR = 10,
            ACSP_CE_COLLISION_WITH_ENV = 11,

            // COMMANDS
            ACSP_REALTIMEPOS_INTERVAL = 200,
            ACSP_GET_CAR_INFO = 201,
            ACSP_SEND_CHAT = 202, // Sends chat to one car
            ACSP_BROADCAST_CHAT = 203, // Sends chat to everybody 

            // new in update 1.2.3:
            ACSP_SESSION_INFO = 59,

            /// <summary>
            /// The server fires one up at startup (assuming the plugin is already there), you can also later re request the protocol version with an ACS_VERSION command. However, I've also added the protocol version number to the ACSP_NEW_SESSION message because that used to be the entry point for the plugin.
            /// </summary>
            ACSP_VERSION = 56,

            /// <summary>
            /// Called every time a chat message is received by the server. Useful to interact with the plugins.
            /// </summary>
            ACSP_CHAT = 57,

            /// <summary>
            /// Fired by the server when the first position update arrives from a client, which means, it's done loading
            /// </summary>
            ACSP_CLIENT_LOADED = 58,

            /// <summary>
            /// Use this to request a session info packet to be sent by the server 
            /// </summary>
            ACSP_GET_SESSION_INFO = 204,

            /// <summary>
            /// To change name, laps, time, wait time of a session. In theory this can change also the current session, but the client is not aware of that at the moment. This will open the door to have time limited races, although the support would be much more clearly implemented natively in AC and I have plans to do that.
            /// I might decide to have the server refusing to change the current session info
            /// </summary>
            ACSP_SET_SESSION_INFO = 205,

            /// <summary>
            /// The server will use this sending a stringW with the description of the problem. Used for stuff like out of bound indices and so on.
            /// </summary>
            ACSP_ERROR = 60,

            /// <summary>
            /// Kicks this player
            /// </summary>
            ACSP_KICK_USER = 206,

            ACSP_NEXT_SESSION = 207,
            ACSP_RESTART_SESSION = 208,
            ACSP_ADMIN_COMMAND = 209, // Send message plus a stringW with the command
        }
    }
}