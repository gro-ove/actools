using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class Busy {
        private readonly bool _invokeInUiThread;

        public Busy(bool invokeInUiThread = false) {
            _invokeInUiThread = invokeInUiThread;
        }

        public bool Is { get; private set; }

        private class ActionAsDisposable : IDisposable {
            private readonly Action _action;

            public ActionAsDisposable(Action action) {
                _action = action;
            }

            public void Dispose() {
                _action.Invoke();
            }
        }

        public IDisposable Set() {
            Is = true;
            return new ActionAsDisposable(() => { Is = false; });
        }

        public void Do(Action a) {
            if (Is) return;
            using (Set()) {
                if (_invokeInUiThread) {
                    a.InvokeInMainThread();
                } else {
                    a();
                }
            }
        }

        public async void Task(Func<Task> a) {
            if (Is) return;
            using (Set()) {
                if (_invokeInUiThread) {
                    await a.InvokeInMainThreadAsync();
                } else {
                    await a();
                }
            }
        }

        public async Task TaskAsync(Func<Task> a) {
            if (Is) return;
            using (Set()) {
                if (_invokeInUiThread) {
                    await a.InvokeInMainThreadAsync();
                } else {
                    await a();
                }
            }
        }
    }

    public static class BysyExtension {
        public static void DoDelay([NotNull] this Busy busy, Action a, int millisecondsDelay) {
            busy.Task(async () => {
                await Task.Delay(millisecondsDelay);
                a();
            });
        }

        public static void TaskDelay([NotNull] this Busy busy, Func<Task> a, int millisecondsDelay) {
            busy.Task(async () => {
                await Task.Delay(millisecondsDelay);
                await a();
            });
        }

        public static Task DoDelay([NotNull] this Busy busy, Action a, TimeSpan delay) {
            return busy.TaskAsync(async () => {
                await Task.Delay(delay);
                a();
            });
        }

        public static void TaskDelay([NotNull] this Busy busy, Func<Task> a, TimeSpan delay) {
            busy.Task(async () => {
                await Task.Delay(delay);
                await a();
            });
        }

        public static void DoDelayAfterwards([NotNull] this Busy busy, Action a, int millisecondsDelay) {
            busy.Task(async () => {
                a();
                await Task.Delay(millisecondsDelay);
            });
        }

        public static Task DoDelayAfterwards([NotNull] this Busy busy, Action a, TimeSpan delay) {
            return busy.TaskAsync(async () => {
                a();
                await Task.Delay(delay);
            });
        }
    }
}
