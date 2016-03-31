using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    public interface IAcManagerNew {
        [CanBeNull]
        IAcObjectNew GetObjectById([NotNull]string id);

        void Rescan();

        Task RescanAsync();

        void Reload(string id);

        void UpdateList();
    }
}