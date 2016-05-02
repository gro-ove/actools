using System;
using AcTools.Kn5File;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificForward.Materials {
    public class MaterialsProviderSimple : IKn5MaterialsProvider {
        public IRenderableMaterial CreateMaterial(string kn5Filename, Kn5Material kn5Material) {
            switch (kn5Material.ShaderName) {
                case "ksBrokenGlass":
                    return new InvisibleMaterial();

                case "GL":
                    return new Kn5MaterialSimpleGl(kn5Filename, kn5Material);

                case "ksTyres":
                case "ksBrakeDisc":
                    return new Kn5MaterialSimpleDiffMaps(kn5Filename, kn5Material);

                case "ksWindscreen":
                    return new Kn5MaterialSimple(kn5Filename, kn5Material);

                case "ksPerPixel":
                case "ksPerPixelAT":
                case "ksPerPixelAT_NS":
                case "ksTree":
                    return new Kn5MaterialSimple(kn5Filename, kn5Material);

                case "ksPerPixelAT_NM":
                    return new Kn5MaterialSimpleAtNm(kn5Filename, kn5Material);

                case "ksPerPixelReflection":
                case "ksPerPixelSimpleRefl":
                    return new Kn5MaterialSimpleReflective(kn5Filename, kn5Material);

                case "ksPerPixelNM":
                case "ksPerPixelNM_UV2":
                    return new Kn5MaterialSimpleNm(kn5Filename, kn5Material);

                case "ksPerPixelNM_UVMult":
                    return new Kn5MaterialSimpleNmMult(kn5Filename, kn5Material);

                case "ksPerPixelMultiMap":
                case "ksPerPixelMultiMap_AT":
                case "ksPerPixelMultiMap_AT_NMDetail":
                case "ksPerPixelMultiMap_damage":
                case "ksPerPixelMultiMap_damage_dirt":
                case "ksPerPixelMultiMap_damage_dirt_sunspot":
                case "ksPerPixelMultiMap_NMDetail":
                case "ksPerPixelMultiMapSimpleRefl":
                    return new Kn5MaterialSimpleMaps(kn5Filename, kn5Material);

                case "ksPerPixelAlpha":
                    return new Kn5MaterialSimpleAlpha(kn5Filename, kn5Material);

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