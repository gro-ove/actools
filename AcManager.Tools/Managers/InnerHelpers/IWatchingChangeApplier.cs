using System.Collections.Generic;

namespace AcManager.Tools.Managers.InnerHelpers {
    internal interface IWatchingChangeApplier {
        void ApplyChange(string location, WatchingChange change);
    }
}
