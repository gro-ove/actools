using System;
using System.Windows.Input;

namespace FirstFloor.ModernUI.Commands {
    public class UnavailableCommand : ICommand {
        public static readonly ICommand Instance = new UnavailableCommand();

        private UnavailableCommand() {}

        bool ICommand.CanExecute(object parameter) {
            return false;
        }

        void ICommand.Execute(object parameter) {}

        event EventHandler ICommand.CanExecuteChanged {
            add { }
            remove { }
        }
    }
}