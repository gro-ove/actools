using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Commands {
    public class CombinedCommand : AsyncCommand<object> {
        public CombinedCommand(ICommand first, ICommand second) : base(
                GetExecute(first, second),
                o => first.CanExecute(o) && second.CanExecute(o)) {
            WeakEventManager<ICommand, EventArgs>.AddHandler(first, nameof(ICommand.CanExecuteChanged), Handler);
            WeakEventManager<ICommand, EventArgs>.AddHandler(second, nameof(ICommand.CanExecuteChanged), Handler);
        }

        private static Func<object, Task> GetExecute(ICommand first, ICommand second) {
            return async o => {
                if (first is IAsyncCommand firstAsync) {
                    await firstAsync.ExecuteAsync(o);
                } else {
                    first.Execute(o);
                }

                if (second is IAsyncCommand secondAsync) {
                    await secondAsync.ExecuteAsync(o);
                } else {
                    second.Execute(o);
                }
            };
        }

        private void Handler(object sender, EventArgs eventArgs) {
            RaiseCanExecuteChanged();
        }
    }
}