using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace FirstFloor.ModernUI.Commands {
    /// <summary>
    ///     This class contains methods for the CommandManager that help avoid memory leaks by
    ///     using weak references.
    /// </summary>
    internal static class CommandManagerHelper {
        internal static void CallWeakReferenceHandlers(List<WeakReference> handlers) {
            if (handlers == null) return;

            var app = Application.Current;
            if (app?.Dispatcher.CheckAccess() != false) {
                // Take a snapshot of the handlers before we call out to them since the handlers
                // could cause the array to me modified while we are reading it.
                var callees = new EventHandler[handlers.Count];
                var count = 0;

                for (var i = handlers.Count - 1; i >= 0; i--) {
                    var reference = handlers[i];
                    var handler = reference.Target as EventHandler;
                    if (handler == null) {
                        // Clean up old handlers that have been collected
                        handlers.RemoveAt(i);
                    } else {
                        callees[count] = handler;
                        count++;
                    }
                }

                // Call the handlers that we snapshotted
                for (var i = 0; i < count; i++) {
                    var handler = callees[i];
                    handler(null, EventArgs.Empty);
                }
            } else {
                app.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => CallWeakReferenceHandlers(handlers)));
            }
        }

        internal static void AddWeakReferenceHandler(ref List<WeakReference> handlers, EventHandler handler, int defaultListSize) {
            if (handlers == null) {
                handlers = defaultListSize > 0 ? new List<WeakReference>(defaultListSize) : new List<WeakReference>();
            }

            handlers.Add(new WeakReference(handler));
        }

        internal static void RemoveWeakReferenceHandler(List<WeakReference> handlers, EventHandler handler) {
            if (handlers == null) return;

            for (var i = handlers.Count - 1; i >= 0; i--) {
                var reference = handlers[i];
                var existingHandler = reference.Target as EventHandler;
                if (existingHandler == null || existingHandler == handler) {
                    // Clean up old handlers that have been collected
                    // in addition to the handler that is to be removed.
                    handlers.RemoveAt(i);
                }
            }
        }
    }
}