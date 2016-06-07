using System;

namespace AcManager.Tools.AcManagersNew {
    public class IgnoringHolder : IDisposable {
        internal event EventHandler Disposed;

        public void Dispose() {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}