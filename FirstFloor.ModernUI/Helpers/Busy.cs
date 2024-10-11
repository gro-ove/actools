using System;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class Busy : NotifyPropertyChanged {
        private readonly bool _invokeInUiThread;
        private readonly bool _logging;

        public Busy(bool invokeInUiThread = false, bool logging = false) {
            _invokeInUiThread = invokeInUiThread;
            _logging = logging;
            _counter = 0;
        }

        private int _counter;

        private bool _is;

        public bool Is {
            get => _is;
            private set => Apply(value, ref _is);
        }

        public IDisposable Set() {
            _counter++;
            Is = true;
            if (_logging) {
                Logging.Debug("Busy: " + _counter);
            }

            return new ActionAsDisposable(SetAction);
        }

        private void SetAction() {
            Is = --_counter > 0;
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
            if (Is) return System.Threading.Tasks.Task.FromResult(false);
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

            busy.Delay(millisecondsDelay, force).Ignore();
        }

        public static void Delay([NotNull] this Busy busy, Action a, TimeSpan delay, bool force = false) {
            using (busy.Set()) {
                a();
            }

            busy.Delay(delay, force).Ignore();
        }

        public static async Task Delay([NotNull] this Busy busy, Func<Task> a, int millisecondsDelay, bool force = false) {
            using (busy.Set()) {
                await a();
            }

            busy.Delay(millisecondsDelay, force).Ignore();
        }

        public static async Task Delay([NotNull] this Busy busy, Func<Task> a, TimeSpan delay, bool force = false) {
            using (busy.Set()) {
                await a();
            }

            busy.Delay(delay, force).Ignore();
        }

        public static Task Yield([NotNull] this Busy busy, Action a) {
            return busy.Task(async () => {
                await Task.Yield();
                a();
            });
        }

        public static Task TaskYield([NotNull] this Busy busy, Func<Task> a) {
            return busy.Task(async () => {
                await Task.Yield();
                await a();
            });
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