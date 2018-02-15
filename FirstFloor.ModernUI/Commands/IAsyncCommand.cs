using System.Threading.Tasks;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Commands {
    public interface IAsyncCommand {
        bool IsInProcess { get; }
        Task ExecuteAsync([CanBeNull] object parameter);
    }
}