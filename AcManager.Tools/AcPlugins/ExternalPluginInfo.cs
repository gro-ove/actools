namespace AcManager.Tools.AcPlugins {
    public class ExternalPluginInfo {
        public readonly string PluginName;
        public readonly int ListeningPort;
        public readonly string RemoteHostname;
        public readonly int RemotePort;

        public ExternalPluginInfo(string pluginName, int listeningPort, string remoteHostname, int remotePort) {
            PluginName = pluginName;
            ListeningPort = listeningPort;
            RemoteHostname = remoteHostname;
            RemotePort = remotePort;
        }
    }
}