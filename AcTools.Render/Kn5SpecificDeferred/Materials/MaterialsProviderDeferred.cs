using AcTools.Kn5File;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificDeferred.Materials {
    public class MaterialsProviderDeferred : Kn5MaterialsProvider {
        public override IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material) {
            if (kn5Material == null) {
                return new InvisibleMaterial();
            }

            switch (kn5Material.ShaderName) {
                case "GL":
                    return new Kn5MaterialGlDeferred();

                case "ksWindscreen":
                    return new InvisibleMaterial();

                default:
                    return new Kn5MaterialDeferred(kn5Filename, kn5Material);
            }
        }

        public override IRenderableMaterial CreateAmbientShadowMaterial(string filename) {
            return new AmbientShadowMaterialDeferred(filename);
        }

        public override IRenderableMaterial CreateSkyMaterial() {
            return new SkyMaterialDeferred();
        }

        public override IRenderableMaterial CreateMirrorMaterial() {
            return new MirrorMaterialDeferred();
        }
    }
}