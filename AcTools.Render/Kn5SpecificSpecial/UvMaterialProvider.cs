using AcTools.Kn5File;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class UvMaterialProvider : Kn5MaterialsProvider {
        public override IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material) {
            return new Kn5MaterialUv(kn5Material);
        }

        public override IRenderableMaterial CreateAmbientShadowMaterial(string filename) {
            return new InvisibleMaterial();
        }

        public override IRenderableMaterial CreateSkyMaterial() {
            return new InvisibleMaterial();
        }

        public override IRenderableMaterial CreateMirrorMaterial() {
            return new InvisibleMaterial();
        }
    }
}