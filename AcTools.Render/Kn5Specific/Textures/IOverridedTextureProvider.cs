using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public interface IOverridedTextureProvider {
        [ItemCanBeNull]
        Task<byte[]> GetOverridedData(string name);
    }
}