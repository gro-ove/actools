using System.Collections;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    public interface IAcManagerNew : IEnumerable {
        [NotNull]
        IAcObjectList WrappersAsIList { get; }

        [CanBeNull]
        IAcObjectNew GetObjectById([NotNull]string id);

        void Rescan();

        Task RescanAsync();

        void Reload(string id);

        void UpdateList();
    }
}