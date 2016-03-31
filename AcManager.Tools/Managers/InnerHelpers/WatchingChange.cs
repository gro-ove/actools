using System.IO;

namespace AcManager.Tools.Managers.InnerHelpers {
    internal class WatchingChange {
        public WatcherChangeTypes Type;
        public string NewLocation, FullFilename;
    }
}
