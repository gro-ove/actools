using AcTools.Kn5File;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class DepthMaterialProvider : IKn5MaterialsProvider {
        public IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material) {
            return new Kn5MaterialDepth();
        }

        public IRenderableMaterial CreateAmbientShadowMaterial(string filename) {
            return new InvisibleMaterial();
        }

        public IRenderableMaterial CreateSkyMaterial() {
            return new InvisibleMaterial();
        }

        public IRenderableMaterial CreateMirrorMaterial() {
            return new InvisibleMaterial();
        }
    }
}