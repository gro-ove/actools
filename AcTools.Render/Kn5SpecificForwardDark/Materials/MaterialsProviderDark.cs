using System;
using System.Linq;
using AcTools.Render.Base.Materials;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class MaterialsProviderDark : IMaterialsFactory {
        public IRenderableMaterial CreateMaterial(object key) {
            if (key is Kn5MaterialDescription kn5) {
                switch (kn5.SpecialKey as string) {
                    case BasicMaterials.DebugKey:
                        return new DebugMaterial(kn5);
                    case null:
                        return CreateMaterial(kn5);
                    default:
                        throw new NotSupportedException($@"Key not supported: {kn5.SpecialKey}");
                }
            }

            if (key is Kn5AmbientShadowMaterialDescription shadow) {
                return new AmbientShadowMaterialSimple(shadow);
            }

            switch (key as string) {
                case BasicMaterials.MirrorKey:
                    return new Kn5MaterialDarkMirror();
                case BasicMaterials.DebugLinesKey:
                    return new DebugLinesMaterial();
                case BasicMaterials.DebugColliderKey:
                    return new DebugColliderMaterial();
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
                    return new Kn5MaterialDarkGl(description);

                case "ksTyres":
                case "ksBrakeDisc":
                    return new Kn5MaterialDarkTyres(description);

                //case "ksBrakeDisc":
                //    return new Kn5MaterialSimpleDiffMaps(description);

                case "ksWindscreen":
                    return new Kn5MaterialDarkWindscreen(description);

                case "ksPerPixel":
                case "ksPerPixelAT":
                case "ksPerPixelAT_NS":
                case "ksTree":
                    return new Kn5MaterialDark(description);

                case "ksPerPixelAT_NM":
                    return new Kn5MaterialDarkAtNm(description);

                case "ksPerPixelReflection":
                case "ksPerPixelSimpleRefl":
                    return new Kn5MaterialDarkReflective(description);

                case "ksPerPixelNM":
                case "ksPerPixelNM_UV2":
                    return new Kn5MaterialDarkNm(description);

                case "ksPerPixelNM_UVMult":
                    return new Kn5MaterialDarkNmMult(description);

                case "ksPerPixelMultiMap":
                case "ksPerPixelMultiMap_AT":
                case "ksPerPixelMultiMap_AT_NMDetail":
                case "ksPerPixelMultiMap_damage":
                case "ksPerPixelMultiMap_damage_dirt":
                case "ksPerPixelMultiMap_damage_dirt_sunspot":
                case "ksPerPixelMultiMap_NMDetail":
                case "ksPerPixelMultiMap_emissive":
                case "ksPerPixelMultiMapSimpleRefl":
                    return new Kn5MaterialDarkMaps(description);

                case "ksPerPixelAlpha":
                    return new Kn5MaterialDarkAlpha(description);

                case "ksSkinnedMesh":
                case "ksSkinnedMesh_NMDetaill":
                    return new Kn5MaterialDarkSkinnedMaps(description);

                case "ksSky":
                case "ksSkyBox":
                    return new Kn5MaterialDarkSky(description);

                default:
                    if (shader.IndexOf("skinned", StringComparison.OrdinalIgnoreCase) != -1) {
                        return new Kn5MaterialDarkSkinnedGl(description);
                    }

                    if (description.Material != null) {
                        if (description.Material.TextureMappings.Any(x => x.Name == "txMaps")) {
                            return new Kn5MaterialDarkMaps(description);
                        }

                        if (description.Material.TextureMappings.Any(x => x.Name == "txNormal")) {
                            return new Kn5MaterialDarkNm(description);
                        }
                    }

                    return new Kn5MaterialDark(description);
            }
        }
    }
}