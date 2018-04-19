using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class NonfatalErrorSolution : AsyncCommand {
        public static Uri IconsDictionary { get; set; }

        [CanBeNull]
        private readonly NonfatalErrorEntry _entry;

        [CanBeNull]
        private readonly Func<CancellationToken, Task> _execute;

        [NotNull]
        public string DisplayName { get; }

        [CanBeNull]
        public string IconKey { get; }

        [CanBeNull]
        public Geometry IconData => IconKey == null || IconsDictionary == null ? null :
                new SharedResourceDictionary { Source = IconsDictionary }[IconKey] as Geometry;

        private bool _solved;

        public bool Solved {
            get => _solved;
            set {
                if (value == _solved) return;
                _solved = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public NonfatalErrorSolution([CanBeNull] string displayName, [CanBeNull] NonfatalErrorEntry entry, [CanBeNull] Func<CancellationToken, Task> execute,
                [Localizable(false)] string iconKey = null) : base(() => Task.Delay(0), () => execute != null) {
            DisplayName = displayName ?? "Fix it";
            IconKey = iconKey;
            _entry = entry;
            _execute = execute;
        }

        protected override bool CanExecuteOverride() {
            return base.CanExecuteOverride() && !Solved;
        }

        protected override async Task ExecuteInner() {
            Logging.Debug("Fixing error: " + DisplayName);

            try {
                using (var waiting = new WaitingDialog()) {
                    waiting.Report("Solving the issue…");
                    await Task.Yield();
                    await _execute(CancellationToken.None).ConfigureAwait(false);
                }
            } catch (Exception e) when (e.IsCanceled()) {
                return;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t solve the issue", e);
                return;
            }

            Solved = true;

            if (_entry != null) {
                NonfatalError.Instance.Errors.Remove(_entry);
            }
        }
    }
}