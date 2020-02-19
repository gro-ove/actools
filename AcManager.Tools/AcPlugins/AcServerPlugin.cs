using AcManager.Tools.AcPlugins.Info;
using AcManager.Tools.AcPlugins.Messages;

namespace AcManager.Tools.AcPlugins {
    public interface IAcServerPlugin {
        string GetName();

        void OnInit(AcServerPluginManager manager);

        void OnConnected();

        void OnDisconnected();

        /// <summary>
        /// Handler for commands.
        /// </summary>
        /// <returns>Returns true if command was handled.</returns>
        bool OnCommandEntered(string cmd);

        #region Handlers for raw acServer messages
        void OnSessionInfo(MsgSessionInfo msg);

        void OnNewSession(MsgSessionInfo msg);

        void OnSessionEnded(MsgSessionEnded msg);

        void OnNewConnection(MsgNewConnection msg);

        void OnConnectionClosed(MsgConnectionClosed msg);

        void OnCarInfo(MsgCarInfo msg);

        void OnCarUpdate(MsgCarUpdate msg);

        void OnCollision(MsgClientEvent msg);

        void OnLapCompleted(MsgLapCompleted msg);

        void OnClientLoaded(MsgClientLoaded msg);

        void OnChatMessage(MsgChat msg);

        void OnProtocolVersion(MsgVersionInfo msg);

        void OnServerError(MsgError msg);
        #endregion Event handlers for raw acServer messages

        #region Handlers for events refined by PluginManager
        /// <summary>
        /// This is triggered after all realtime reports per interval have arrived - they are now
        /// up-to-date and can be accessed via the DriverInfo mechanics
        /// </summary>
        void OnBulkCarUpdateFinished();

        void OnLapCompleted(LapInfo msg);

        void OnCollision(IncidentInfo msg);

        void OnCarUpdate(DriverInfo driverInfo);

        void OnAcServerTimeout();

        void OnAcServerAlive();
        #endregion Handlers for events refined by PluginManager
    }

    public class AcServerPlugin : IAcServerPlugin {
        protected AcServerPluginManager PluginManager { get; private set; }

        public virtual string GetName() {
            return GetType().Name;
        }

        void IAcServerPlugin.OnInit(AcServerPluginManager manager) {
            PluginManager = manager;
            OnInit();
        }

        public virtual void OnInit() { }

        public virtual void OnConnected() { }

        public virtual void OnDisconnected() { }

        public virtual bool OnCommandEntered(string cmd) {
            return false;
        }

        public virtual void OnSessionInfo(MsgSessionInfo msg) { }

        public virtual void OnNewSession(MsgSessionInfo msg) { }

        public virtual void OnSessionEnded(MsgSessionEnded msg) { }

        public virtual void OnNewConnection(MsgNewConnection msg) { }

        public virtual void OnConnectionClosed(MsgConnectionClosed msg) { }

        public virtual void OnCarInfo(MsgCarInfo msg) { }

        public virtual void OnCarUpdate(MsgCarUpdate msg) { }

        public virtual void OnCollision(MsgClientEvent msg) { }

        public virtual void OnLapCompleted(MsgLapCompleted msg) { }

        public virtual void OnClientLoaded(MsgClientLoaded msg) { }

        public virtual void OnChatMessage(MsgChat msg) { }

        public virtual void OnProtocolVersion(MsgVersionInfo msg) { }

        public virtual void OnServerError(MsgError msg) { }

        public virtual void OnBulkCarUpdateFinished() { }

        public virtual void OnLapCompleted(LapInfo msg) { }

        public virtual void OnCollision(IncidentInfo msg) { }

        public virtual void OnCarUpdate(DriverInfo driverInfo) { }

        public virtual void OnAcServerTimeout() { }

        public virtual void OnAcServerAlive() { }
    }
}