namespace AcManager.Tools.Managers.InnerHelpers {
    public interface IWatchingChangeApplier {
        void ApplyChange(string location, WatchingChange change);
    }
}
