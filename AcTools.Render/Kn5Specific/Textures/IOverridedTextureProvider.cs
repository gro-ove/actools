using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public interface IOverridedTextureProvider {
        [CanBeNull]
        byte[] GetOverridedData(string name);

        [ItemCanBeNull]
        Task<byte[]> GetOverridedDataAsync(string name);
    }
}