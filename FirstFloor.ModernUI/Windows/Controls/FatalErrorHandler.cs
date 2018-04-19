using System;
using System.Linq;
using System.Windows;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class FatalErrorEventArgs : EventArgs {
        public Exception Exception { get; }

        public FatalErrorEventArgs(Exception exception) {
            Exception = exception;
        }
    }

    public static class FatalErrorHandler {
        public static event EventHandler<FatalErrorEventArgs> FatalError;

        public static void OnFatalError(Exception e) {
            FatalError?.Invoke(null, new FatalErrorEventArgs(e));

            var app = Application.Current;
            if (app != null){
                foreach (var result in app.Windows.OfType<DpiAwareWindow>().Where(x => !x.ShownAsDialog)) {
                    result.IsDimmed = true;
                }
            }

            new FatalErrorMessage {
                Message = e.Message,
                StackTrace = e.ToString()
            }.ShowDialog();
        }
    }
}