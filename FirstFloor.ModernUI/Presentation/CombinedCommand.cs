using System;
using System.Windows;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Presentation {
    public class CombinedCommand : CommandBase {
        private readonly ICommand _first;
        private readonly ICommand _second;

        public CombinedCommand(ICommand first, ICommand second) {
            _first = first;
            _second = second;
            WeakEventManager<ICommand, EventArgs>.AddHandler(first, nameof(ICommand.CanExecuteChanged), Handler);
            WeakEventManager<ICommand, EventArgs>.AddHandler(second, nameof(ICommand.CanExecuteChanged), Handler);
        }

        private void Handler(object sender, EventArgs eventArgs) {
            OnCanExecuteChanged();
        }

        protected override void OnExecute(object parameter) {
            _first.Execute(parameter);
            _second.Execute(parameter);
        }

        public override bool CanExecute(object parameter) {
            return _first.CanExecute(parameter) && _second.CanExecute(parameter);
        }
    }
}