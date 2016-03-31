using System;

namespace AcManager.Tools.Miscellaneous {
    public class ActionAsDisposable : IDisposable {
        private readonly Action _action;

        public ActionAsDisposable(Action action) {
            _action = action;
        }

        public void Dispose() {
            _action.Invoke();
        }
    }
}