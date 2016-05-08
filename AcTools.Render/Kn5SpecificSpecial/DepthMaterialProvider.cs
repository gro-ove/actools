using AcTools.Kn5File;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class DepthMaterialProvider : Kn5MaterialsProvider {
        public override IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material) {
            return new Kn5MaterialDepth();
        }

        public override IRenderableMaterial CreateAmbientShadowMaterial(string filename) {
            return new Kn5MaterialDepth();
        }

        public override IRenderableMaterial CreateSkyMaterial() {
            return new InvisibleMaterial();
        }

        public override IRenderableMaterial CreateMirrorMaterial() {
            return new InvisibleMaterial();
        }
    }
}