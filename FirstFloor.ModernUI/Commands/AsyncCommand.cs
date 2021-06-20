using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Commands {
    public static class CancellationTokenStraighten {
        public static CancellationToken Straighten(this CancellationToken? c) {
            return c ?? default;
        }
    }

    public class AsyncCommand : CommandExt, IAsyncCommand {
        [NotNull]
        private readonly Func<Task> _execute;

        [CanBeNull]
        private readonly Func<bool> _canExecute;

        private bool _inProcess;
        public bool IsInProcess => _inProcess;

        public Task ExecuteAsync(object parameter) {
            return ExecuteAsync();
        }

        private readonly TimeSpan _additionalDelay;

        public AsyncCommand([NotNull] Func<Task> execute, Func<bool> canExecute, TimeSpan additionalDelay = default, bool isAutomaticRequeryDisabled = false)
                : base(false, isAutomaticRequeryDisabled) {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _additionalDelay = additionalDelay;
        }

        public AsyncCommand([NotNull] Func<Task> execute, TimeSpan additionalDelay = default, bool isAutomaticRequeryDisabled = false)
                : this(execute, null, additionalDelay, isAutomaticRequeryDisabled) {}

        protected override bool CanExecuteOverride() {
            return !_inProcess && (_canExecute == null || _canExecute());
        }

        protected override void ExecuteOverride() {
            ExecuteAsync().Ignore();
        }

        protected virtual Task ExecuteInner() {
            return _execute() ?? Task.Delay(0);
        }

        [Obsolete]
        public new void Execute() {
            base.Execute();
        }

        public async Task ExecuteAsync() {
            try {
                _inProcess = true;
                RaiseCanExecuteChanged();

                await ExecuteInner();

                if (_additionalDelay != TimeSpan.Zero) {
                    await Task.Delay(_additionalDelay);
                } else {
                    await Task.Yield();
                }
            } catch(Exception e) {
                Logging.Error(e);
                throw;
            } finally {
                _inProcess = false;
                RaiseCanExecuteChanged();
            }
        }
    }

    public class AsyncCommand<T> : CommandExt<T>, IAsyncCommand {
        [NotNull]
        private readonly Func<T, Task> _execute;

        [CanBeNull]
        private readonly Func<T, bool> _canExecute;

        private bool _inProcess;
        public bool IsInProcess => _inProcess;

        public Task ExecuteAsync(object parameter) {
            if (parameter == null) return ExecuteAsync(default);
            return ConvertXamlCompatible(parameter, out T value) ? ExecuteAsync(value) : Task.Delay(0);
        }

        private readonly int _additionalDelay;

        public AsyncCommand([NotNull] Func<T, Task> execute, Func<T, bool> canExecute = null, int additionalDelay = 0, bool isAutomaticRequeryDisabled = false)
                : base(false, isAutomaticRequeryDisabled) {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _additionalDelay = additionalDelay;
        }

        protected override bool CanExecuteOverride(T parameter) {
            return !_inProcess && (_canExecute == null || _canExecute(parameter));
        }

        [Obsolete]
        public new void Execute(T parameter) {
            base.Execute(parameter);
        }

        public async Task ExecuteAsync(T parameter) {
            try {
                _inProcess = true;
                RaiseCanExecuteChanged();

                await (_execute(parameter) ?? Task.FromResult(default(T)));

                if (_additionalDelay != 0) {
                    await Task.Delay(_additionalDelay);
                } else {
                    await Task.Yield();
                }
            } catch (Exception e) {
                Logging.Error(e);
                throw;
            } finally {
                _inProcess = false;
                RaiseCanExecuteChanged();
            }
        }

        protected override void ExecuteOverride(T parameter) {
            ExecuteAsync(parameter).Ignore();
        }
    }
}