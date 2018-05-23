using System;
using System.Linq;
using System.Windows;
using FirstFloor.ModernUI.Dialogs;

namespace FirstFloor.ModernUI.Windows.Controls {
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