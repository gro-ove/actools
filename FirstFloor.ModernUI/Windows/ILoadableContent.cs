using System.Threading;
using System.Threading.Tasks;

namespace FirstFloor.ModernUI.Windows {
    public interface ILoadableContent {
        Task LoadAsync(CancellationToken cancellationToken);

        void Load();

        void Initialize();
    }
}
