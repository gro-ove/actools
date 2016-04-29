using AcTools.Kn5File;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificDeferred.Materials {
    public class MaterialsProviderDeferred : IKn5MaterialsProvider {
        public IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material) {
            switch (kn5Material.ShaderName) {
                case "GL":
                    return new Kn5MaterialGlDeferred();

                case "ksWindscreen":
                    return new InvisibleMaterial();

                default:
                    return new Kn5MaterialDeferred(kn5Filename, kn5Material);
            }
        }

        public IRenderableMaterial CreateAmbientShadowMaterial(string filename) {
            return new AmbientShadowMaterialDeferred(filename);
        }

        public IRenderableMaterial CreateSkyMaterial() {
            return new SkyMaterialDeferred();
        }

        public IRenderableMaterial CreateMirrorMaterial() {
            return new MirrorMaterialDeferred();
        }
    }
}