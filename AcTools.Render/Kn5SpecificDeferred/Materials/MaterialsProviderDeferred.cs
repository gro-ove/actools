using System;
using AcTools.Render.Base.Materials;
using AcTools.Render.Kn5Specific.Materials;

namespace AcTools.Render.Kn5SpecificDeferred.Materials {
    public class MaterialsProviderDeferred : IMaterialsFactory {
        public IRenderableMaterial CreateMaterial(object key) {
            var kn5 = key as Kn5MaterialDescription;
            if (kn5 != null) {
                return CreateMaterial(kn5);
            }

            var shadow = key as Kn5AmbientShadowMaterialDescription;
            if (shadow != null) {
                return new AmbientShadowMaterialDeferred(shadow.Filename);
            }

            switch (key as string) {
                case BasicMaterials.MirrorKey:
                    return new MirrorMaterialDeferred();
                case BasicMaterials.SkyKey:
                    return new SkyMaterialDeferred();
            }

            throw new NotSupportedException($@"Key not supported: {key}");
        }

        private IRenderableMaterial CreateMaterial(Kn5MaterialDescription description) {
            if (description?.Material == null) {
                return new InvisibleMaterial();
            }

            switch (description.Material.ShaderName) {
                case "GL":
                    return new Kn5MaterialGlDeferred();

                case "ksWindscreen":
                    return new InvisibleMaterial();

                default:
                    return new Kn5MaterialDeferred(description);
            }
        }
    }
}