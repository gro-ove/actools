using System;
using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Commands {
    public class CombinedCommand : CommandBase {
        private readonly ICommand _first;
        private readonly ICommand _second;

        public CombinedCommand(ICommand first, ICommand second) : base(false, false) {
            _first = first;
            _second = second;
            WeakEventManager<ICommand, EventArgs>.AddHandler(first, nameof(ICommand.CanExecuteChanged), Handler);
            WeakEventManager<ICommand, EventArgs>.AddHandler(second, nameof(ICommand.CanExecuteChanged), Handler);
        }

        protected override bool CanExecuteOverride(object parameter) {
            return _first.CanExecute(parameter) && _second.CanExecute(parameter);
        }

        protected override void ExecuteOverride(object parameter) {
            _first.Execute(parameter);
            _second.Execute(parameter);
        }

        private void Handler(object sender, EventArgs eventArgs) {
            RaiseCanExecuteChanged();
        }
    }
}