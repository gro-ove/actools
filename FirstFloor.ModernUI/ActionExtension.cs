using System;
using System.Windows;
using System.Windows.Threading;

namespace FirstFloor.ModernUI {
    public static class ActionExtension {
        public static void InvokeInMainThread(this Action action) {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(action);
        }

        public static T InvokeInMainThread<T>(this Func<T> action) {
            return (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(action);
        }

        public static void InvokeInMainThreadAsync(this Action action) {
            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).InvokeAsync(action);
        }
    }
}