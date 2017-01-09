using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class NonfatalErrorSolution : AsyncCommand {
        private readonly NonfatalErrorEntry _entry;
        private readonly Func<CancellationToken, Task> _execute;

        [NotNull]
        public string DisplayName { get; }

        public NonfatalErrorSolution([CanBeNull] string displayName, NonfatalErrorEntry entry, [NotNull] Func<CancellationToken, Task> execute, Func<bool> canExecute = null)
                : base(() => Task.Delay(0), canExecute) {
            _entry = entry;
            _execute = execute;
            DisplayName = displayName ?? "Fix It";
        }

        protected override async Task ExecuteInner() {
            try {
                using (var waiting = new WaitingDialog()) {
                    waiting.Report("Solving the issue…");
                    await _execute(waiting.CancellationToken);
                }
            } catch (TaskCanceledException) {
                return;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t solve the issue", e);
                return;
            }

            if (_entry != null) {
                NonfatalError.Instance.Errors.Remove(_entry);
            }
        }
    }
}