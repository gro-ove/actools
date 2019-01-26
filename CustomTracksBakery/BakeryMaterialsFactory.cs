using System;
using AcTools.Kn5File;
using AcTools.Render.Base.Materials;
using AcTools.Render.Kn5Specific.Materials;

namespace CustomTracksBakery {
    public class BakeryMaterialsFactory : IMaterialsFactory {
        private Kn5 _kn5;

        public BakeryMaterialsFactory(Kn5 kn5) {
            _kn5 = kn5;
        }

        public IRenderableMaterial CreateMaterial(object key) {
            if (key is uint id) {
                return new Kn5MaterialToBake(_kn5.GetMaterial(id));
            }

            if (key is Kn5MaterialDescription description) {
                return new Kn5MaterialToBake(description.Material);
            }

            if (key is Kn5AmbientShadowMaterialDescription) {
                return new Kn5MaterialToBake(null);
            }

            throw new NotSupportedException("Type: " + key?.GetType());
        }
    }
}