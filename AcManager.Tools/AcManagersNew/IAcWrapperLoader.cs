using System.Threading.Tasks;

namespace AcManager.Tools.AcManagersNew {
    public interface IAcWrapperLoader {
        /// <summary>
        /// Not recommended way, but can’t think of something better
        /// at the moment. After all, it’s not that bad.
        /// </summary>
        /// <param name="id"></param>
        void Load(string id);

        Task LoadAsync(string id);
    }
}