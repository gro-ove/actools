using System;
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

        public async void Do(Func<Task> a) {
            if (Is) return;
            using (Set()) {
                await a();
            }
        }

        public void DoDelay(Action a, int millisecondsDelay) {
            Do(async () => {
                await Task.Delay(millisecondsDelay);
                a();
            });
        }

        public Task DoDelay(Action a, TimeSpan delay) {
            return DoAsync(async () => {
                await Task.Delay(delay);
                a();
            });
        }

        public async Task DoAsync(Func<Task> a) {
            if (Is) return;
            using (Set()) {
                await a();
            }
        }
    }
}
