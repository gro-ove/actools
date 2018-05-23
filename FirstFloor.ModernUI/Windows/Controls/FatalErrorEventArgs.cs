using System;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class FatalErrorEventArgs : EventArgs {
        public Exception Exception { get; }

        public FatalErrorEventArgs(Exception exception) {
            Exception = exception;
        }
    }
}