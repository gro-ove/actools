using System;
using System.Linq;
using System.Windows;

namespace FirstFloor.ModernUI.Windows.Controls {
    public static class FatalErrorHandler {
        public static void OnFatalError(Exception e) {
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