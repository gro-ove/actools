using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.Objects;

namespace AcManager.CustomShowroom {
    public interface ILiveryGenerator {
        Task CreateLiveryAsync(CarSkinObject skinDirectory, Color[] colors, string preferredStyle);
    }
}