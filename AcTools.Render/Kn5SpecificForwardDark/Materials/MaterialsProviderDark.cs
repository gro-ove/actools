using System;
using AcTools.Render.Base.Materials;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class MaterialsProviderDark : IMaterialsFactory {
        public IRenderableMaterial CreateMaterial(object key) {
            var kn5 = key as Kn5MaterialDescription;
            if (kn5 != null) {
                return CreateMaterial(kn5);
            }

            var shadow = key as Kn5AmbientShadowMaterialDescription;
            if (shadow != null) {
                return new AmbientShadowMaterialSimple(shadow);
            }

            switch (key as string) {
                case BasicMaterials.MirrorKey:
                    return new Kn5MaterialSimpleMirror();
                case BasicMaterials.FlatMirrorKey:
                    return new FlatMirrorMaterialSimple(false);
                case BasicMaterials.FlatGroundKey:
                    return new FlatMirrorMaterialSimple(true);
                case BasicMaterials.DebugKey:
                    return new DebugMaterial();
                case BasicMaterials.DebugLinesKey:
                    return new DebugLinesMaterial();
            }

            throw new NotSupportedException($@"Key not supported: {key}");
        }

        private IRenderableMaterial CreateMaterial(Kn5MaterialDescription description) {
            if (description?.Material == null) {
                return new InvisibleMaterial();
            }

            var shader = description.Material?.ShaderName;
            if (shader == null) {
                return new InvisibleMaterial();
            }

            switch (description.Material?.ShaderName) {
                case "ksBrokenGlass":
                    return new InvisibleMaterial();

                case "GL":
                    return new Kn5MaterialSimpleGl(description);

                case "ksTyres":
                case "ksBrakeDisc":
                    return new Kn5MaterialSimpleDiffMaps(description);

                case "ksWindscreen":
                    return new Kn5MaterialSimple(description);

                case "ksPerPixel":
                case "ksPerPixelAT":
                case "ksPerPixelAT_NS":
                case "ksTree":
                    return new Kn5MaterialSimple(description);

                case "ksPerPixelAT_NM":
                    return new Kn5MaterialSimpleAtNm(description);

                case "ksPerPixelReflection":
                case "ksPerPixelSimpleRefl":
                    return new Kn5MaterialSimpleReflective(description);

                case "ksPerPixelNM":
                case "ksPerPixelNM_UV2":
                    return new Kn5MaterialSimpleNm(description);

                case "ksPerPixelNM_UVMult":
                    return new Kn5MaterialSimpleNmMult(description);

                case "ksPerPixelMultiMap":
                case "ksPerPixelMultiMap_AT":
                case "ksPerPixelMultiMap_AT_NMDetail":
                case "ksPerPixelMultiMap_damage":
                case "ksPerPixelMultiMap_damage_dirt":
                case "ksPerPixelMultiMap_damage_dirt_sunspot":
                case "ksPerPixelMultiMap_NMDetail":
                case "ksPerPixelMultiMapSimpleRefl":
                    return new Kn5MaterialSimpleMaps(description);

                case "ksPerPixelAlpha":
                    return new Kn5MaterialSimpleAlpha(description);

                case "ksSkinnedMesh":
                case "ksSkinnedMesh_NMDetaill":
                    return new Kn5MaterialSimpleSkinnedMaps(description);

                default:
                    if (shader.IndexOf("skinned", StringComparison.OrdinalIgnoreCase) != -1) {
                        return new Kn5MaterialSimpleSkinnedGl(description);
                    }

                    return new Kn5MaterialSimple(description);
            }
        }
    }
}