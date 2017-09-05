using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class Busy {
        private readonly bool _invokeInUiThread;
        private readonly bool _logging;

        public Busy(bool invokeInUiThread = false, bool logging = false) {
            _invokeInUiThread = invokeInUiThread;
            _logging = logging;
            _counter = 0;
        }

        private int _counter;
        public bool Is => _counter > 0;

        public IDisposable Set() {
            _counter++;
            if (_logging) {
                Logging.Debug("Busy: " + _counter);
            }

            return new ActionAsDisposable(SetAction);
        }

        private void SetAction() {
            _counter--;
            if (_logging) {
                Logging.Debug("Ended: " + _counter);
            }
        }

        private class ActionAsDisposable : IDisposable {
            private readonly Action _action;

            public ActionAsDisposable(Action action) {
                _action = action;
            }

            public void Dispose() {
                _action.Invoke();
            }
        }

        private void DoUi(Action a) {
            if (Is) return;
            using (Set()) {
                a();
            }
        }

        public void Do(Action a) {
            if (_invokeInUiThread) {
                var t = this;
                ActionExtension.InvokeInMainThread(() => t.DoUi(a));
            } else {
                DoUi(a);
            }
        }

        private async Task TaskUi(Func<Task> a) {
            if (Is) return;
            using (Set()) {
                await a();
            }
        }

        public Task Task(Func<Task> a) {
            var t = this;
            return _invokeInUiThread ? ActionExtension.InvokeInMainThreadAsync(() => t.TaskUi(a)) : TaskUi(a);
        }

        private async Task DelayUi(TimeSpan delay, bool force) {
            if (!force && Is) return;
            using (Set()) {
                await System.Threading.Tasks.Task.Delay(delay);
            }
        }

        public Task Delay(TimeSpan delay, bool force = false) {
            var t = this;
            return _invokeInUiThread ? ActionExtension.InvokeInMainThreadAsync(() => t.DelayUi(delay, force)) : DelayUi(delay, force);
        }

        private async Task DelayUi(int millisecondsDelay, bool force) {
            if (!force && Is) return;
            using (Set()) {
                await System.Threading.Tasks.Task.Delay(millisecondsDelay);
            }
        }

        public Task Delay(int delay, bool force = false) {
            var t = this;
            return _invokeInUiThread ? ActionExtension.InvokeInMainThreadAsync(() => t.DelayUi(delay, force)) : DelayUi(delay, force);
        }
    }

    public static class BusyExtension {
        public static void Delay([NotNull] this Busy busy, Action a, int millisecondsDelay, bool force = false) {
            using (busy.Set()) {
                a();
            }

            busy.Delay(millisecondsDelay, force).Forget();
        }

        public static void Delay([NotNull] this Busy busy, Action a, TimeSpan delay, bool force = false) {
            using (busy.Set()) {
                a();
            }

            busy.Delay(delay, force).Forget();
        }

        public static async Task Delay([NotNull] this Busy busy, Func<Task> a, int millisecondsDelay, bool force = false) {
            using (busy.Set()) {
                await a();
            }

            busy.Delay(millisecondsDelay, force).Forget();
        }

        public static async Task Delay([NotNull] this Busy busy, Func<Task> a, TimeSpan delay, bool force = false) {
            using (busy.Set()) {
                await a();
            }

            busy.Delay(delay, force).Forget();
        }

        public static Task DoDelay([NotNull] this Busy busy, Action a, int millisecondsDelay) {
            return busy.Task(async () => {
                await Task.Delay(millisecondsDelay);
                a();
            });
        }

        public static Task DoDelay([NotNull] this Busy busy, Action a, TimeSpan delay) {
            return busy.Task(async () => {
                await Task.Delay(delay);
                a();
            });
        }

        public static Task TaskDelay([NotNull] this Busy busy, Func<Task> a, int millisecondsDelay) {
            return busy.Task(async () => {
                await Task.Delay(millisecondsDelay);
                await a();
            });
        }

        public static Task TaskDelay([NotNull] this Busy busy, Func<Task> a, TimeSpan delay) {
            return busy.Task(async () => {
                await Task.Delay(delay);
                await a();
            });
        }

        public static Task DoDelayAfterwards([NotNull] this Busy busy, Action a, int millisecondsDelay) {
            return busy.Task(async () => {
                a();
                await Task.Delay(millisecondsDelay);
            });
        }

        public static Task DoDelayAfterwards([NotNull] this Busy busy, Action a, TimeSpan delay) {
            return busy.Task(async () => {
                a();
                await Task.Delay(delay);
            });
        }

        public static Task TaskDelayAfterwards([NotNull] this Busy busy, Func<Task> a, int millisecondsDelay) {
            return busy.Task(async () => {
                await a();
                await Task.Delay(millisecondsDelay);
            });
        }

        public static Task TaskDelayAfterwards([NotNull] this Busy busy, Func<Task> a, TimeSpan delay) {
            return busy.Task(async () => {
                await a();
                await Task.Delay(delay);
            });
        }
    }
}
