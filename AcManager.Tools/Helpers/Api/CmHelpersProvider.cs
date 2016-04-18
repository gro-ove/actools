namespace AcManager.Tools.Helpers.Api {
    public partial class CmHelpersProvider {
        public static string GetAddress(string path) {
            return ServerAddress + path;
        }
    }
}
