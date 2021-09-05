namespace AcManager.Tools.AcPlugins {
    public class ExternalPluginInfo {
        public readonly int ListeningPort;
        public readonly string RemoteHostname;
        public readonly int RemotePort;

        public ExternalPluginInfo(int listeningPort, string remoteHostname, int remotePort) {
            ListeningPort = listeningPort;
            RemoteHostname = remoteHostname;
            RemotePort = remotePort;
        }
    }
}