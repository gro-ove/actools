using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.AcManagersNew {
    public abstract partial class AcManagerNew<T> : FileAcManager<T> where T : AcCommonObject {
        private bool _subscribed;

        public override void ActualScan() {
            base.ActualScan();

            if (_subscribed || !IsScanned) return;
            _subscribed = true;
            Directories.Subscribe(this);
        }
    }
}
