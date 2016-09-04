using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public interface INonfatalErrorSolution {
        [CanBeNull]
        string DisplayName { get; }

        bool CanBeApplied { get; }

        Task Apply(CancellationToken cancellationToken);
    }

    public static class NonfatalErrorSolution {
        public static async Task Solve([NotNull] this INonfatalErrorSolution solution, NonfatalErrorEntry entry = null) {
            if (solution == null) throw new ArgumentNullException(nameof(solution));

            try {
                using (var waiting = new WaitingDialog()) {
                    waiting.Report("Solving the issue…");
                    await solution.Apply(waiting.CancellationToken);
                }
            } catch (TaskCanceledException) {
                return;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t solve the issue", e);
                return;
            }

            if (entry != null) {
                NonfatalError.Instance.Errors.Remove(entry);
            }
        }
    }
}