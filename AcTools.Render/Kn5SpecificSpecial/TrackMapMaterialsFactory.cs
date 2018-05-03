using AcTools.Render.Base.Materials;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class TrackMapMaterialsFactory : IMaterialsFactory {
        public IRenderableMaterial CreateMaterial(object key) {
            if (BasicMaterials.DepthOnlyKey.Equals(key)) {
                return new Kn5MaterialTrackMap();
            }

            return new InvisibleMaterial();
        }
    }
}