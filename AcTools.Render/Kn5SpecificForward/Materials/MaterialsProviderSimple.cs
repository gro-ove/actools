using System;
using AcTools.Kn5File;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class MaterialsProviderSimple : IKn5MaterialsProvider {
        public IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material) {
            switch (kn5Material.ShaderName) {
                case "GL":
                    throw new NotImplementedException();

                case "ksWindscreen":
                    return new InvisibleMaterial();

                default:
                    return new Kn5MaterialSimple(kn5Filename, kn5Material);
            }
        }

        public IRenderableMaterial CreateAmbientShadowMaterial(string filename) {
            return new AmbientShadowMaterialSimple(filename);
        }

        public IRenderableMaterial CreateSkyMaterial() {
            throw new NotSupportedException();
        }

        public IRenderableMaterial CreateMirrorMaterial() {
            return new Kn5MaterialSimpleMirror();
        }
    }
}