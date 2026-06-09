using AcTools.Render.Base.Materials;
using SlimDX;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class TrackMapMaterialsFactory : IMaterialsFactory {
        public IRenderableMaterial CreateMaterial(object key) {
            if (key as string == "main") {
                return new Kn5MaterialTrackMap(new Vector3(1f, 1f, 1f));
            }
            if (key is Vector3 v) {
                return new Kn5MaterialTrackMap(v);
            }
            if (key as string == "BasicMaterial.DepthOnly") {
                return new Kn5MaterialTrackMap(new Vector3(1f, 1f, 1f));
            }
            return new InvisibleMaterial();
        }
    }
}