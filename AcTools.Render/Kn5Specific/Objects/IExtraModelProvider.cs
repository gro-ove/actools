using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Objects {
    public interface IExtraModelProvider {
        [ItemCanBeNull]
        Task<byte[]> GetModel([CanBeNull] string key);
    }
}