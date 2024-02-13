using System.Collections;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    public interface IAcManagerNew : IEnumerable {
        string Id { get; }
        
        [CanBeNull]
        AcItemWrapper GetWrapperById([NotNull] string id);

        [NotNull]
        IAcObjectList WrappersAsIList { get; }

        [CanBeNull]
        IAcObjectNew GetObjectById([NotNull]string id);

        [ItemCanBeNull]
        Task<IAcObjectNew> GetObjectByIdAsync([NotNull]string id);

        void Rescan();

        Task RescanAsync();

        void Reload(string id);

        void UpdateList(bool force);

        int LoadedCount { get; }

        bool IsLoaded { get; }
    }
}