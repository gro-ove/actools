using System;
using System.Windows;
using System.Windows.Threading;

namespace FirstFloor.ModernUI {
    public static class ActionExtension {
        public static void InvokeInMainThread(this Action action) {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(action);

            /*var app = Application.Current;
            if (app != null) {
                app.Dispatcher.Invoke(action);
            } else {
                action.Invoke();
            }*/
        }

        public static void BeginInvokeInMainThread(this Action action) {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).BeginInvoke(action);

            /*var app = Application.Current;
            if (app != null) {
                app.Dispatcher.BeginInvoke(action);
            } else {
                action.Invoke();
            }*/
        }

        public static T InvokeInMainThread<T>(this Func<T> action) {
            return (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(action);

            /*var app = Application.Current;
            if (app != null) {
                return app.Dispatcher.Invoke(action);
            } else {
                return action.Invoke();
            }*/
        }

        public static void InvokeInMainThreadAsync(this Action action) {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).InvokeAsync(action);

            /*var app = Application.Current;
            if (app != null) {
                app.Dispatcher.InvokeAsync(action);
            } else {
                action.Invoke();
            }*/
        }
    }
}