using System;
using System.Threading.Tasks;

namespace FirstFloor.ModernUI.Presentation {
    public class AsyncCommand : CommandBase {
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _inProcess;
        private readonly int _additionalDelay;
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="execute"></param>
        /// <param name="canExecute"></param>
        /// <param name="additionalDelay">In milliseconds, optional</param>
        public AsyncCommand(Func<object, Task> execute, Func<object, bool> canExecute = null, int additionalDelay = 0) {
            if (execute == null) {
                throw new ArgumentNullException(nameof(execute));
            }

            _execute = execute;
            _additionalDelay = additionalDelay;
            _canExecute = canExecute ?? (o => true);
        }

        public AsyncCommand(Func<object, Task> execute, int additionalDelay) : this(execute, null, additionalDelay) {}

        public override bool CanExecute(object parameter) {
            return !_inProcess && _canExecute(parameter);
        }
        
        protected override async void OnExecute(object parameter) {
            _inProcess = true;
            OnCanExecuteChanged();

            await _execute(parameter);

            if (_additionalDelay != 0) {
                await Task.Delay(_additionalDelay);
            }

            _inProcess = false;
            OnCanExecuteChanged();
        }
    }
}