using System.Windows.Input;

namespace FirstFloor.ModernUI.Commands {
    public interface ICommandExt : ICommand {
        void RaiseCanExecuteChanged();
    }
}