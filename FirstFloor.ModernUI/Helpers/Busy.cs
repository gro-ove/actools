using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FirstFloor.ModernUI.Helpers {
    public class Busy {
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
                a();
            }
        }

        public async void Task(Func<Task> a) {
            if (Is) return;
            using (Set()) {
                await a();
            }
        }

        public async Task TaskAsync(Func<Task> a) {
            if (Is) return;
            using (Set()) {
                await a();
            }
        }

        public void DoDelay(Action a, int millisecondsDelay) {
            Task(async () => {
                await System.Threading.Tasks.Task.Delay(millisecondsDelay);
                a();
            });
        }

        public void TaskDelay(Func<Task> a, int millisecondsDelay) {
            Task(async () => {
                await System.Threading.Tasks.Task.Delay(millisecondsDelay);
                await a();
            });
        }

        public Task DoDelay(Action a, TimeSpan delay) {
            return TaskAsync(async () => {
                await System.Threading.Tasks.Task.Delay(delay);
                a();
            });
        }

        public void TaskDelay(Func<Task> a, TimeSpan delay) {
            Task(async () => {
                await System.Threading.Tasks.Task.Delay(delay);
                await a();
            });
        }

        public void DoDelayAfterwards(Action a, int millisecondsDelay) {
            Task(async () => {
                a();
                await System.Threading.Tasks.Task.Delay(millisecondsDelay);
            });
        }

        public Task DoDelayAfterwards(Action a, TimeSpan delay) {
            return TaskAsync(async () => {
                a();
                await System.Threading.Tasks.Task.Delay(delay);
            });
        }
    }
}
