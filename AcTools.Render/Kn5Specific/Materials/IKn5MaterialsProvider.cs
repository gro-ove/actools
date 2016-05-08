using AcTools.Kn5File;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Materials {
    public interface IKn5MaterialsProvider {
        [NotNull]
        IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material);

        [NotNull]
        IRenderableMaterial CreateAmbientShadowMaterial(string filename);

        [NotNull]
        IRenderableMaterial CreateSkyMaterial();

        [NotNull]
        IRenderableMaterial GetMirrorMaterial();
    }
}