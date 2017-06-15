using System;
using System.Windows.Input;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Windows {
    public class AlwaysExecutable : MarkupExtension, ICommand {
        public AlwaysExecutable(ICommand command) {
            Command = command;
        }

        [ConstructorArgument("command")]
        public ICommand Command { get; set; }

        public bool CanExecute(object parameter) {
            return true;
        }

        public void Execute(object parameter) {
            Command?.Execute(parameter);
        }

        public event EventHandler CanExecuteChanged {
            add { }
            remove { }
        }

        public override object ProvideValue(IServiceProvider serviceProvider) {
            return this;
        }
    }
}