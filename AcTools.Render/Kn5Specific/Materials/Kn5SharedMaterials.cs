using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Materials;

namespace AcTools.Render.Kn5Specific.Materials {
    public class Kn5SharedMaterials : SharedMaterials {
        private readonly IKn5 _kn5;

        public Kn5SharedMaterials(IDeviceContextHolder holder, IKn5 kn5) : base(holder.Get<IMaterialsFactory>()) {
            _kn5 = kn5;
        }

        protected override IRenderableMaterial CreateMaterial(object key) {
            switch (key) {
                case uint id:
                    return base.CreateMaterial(new Kn5MaterialDescription(_kn5.GetMaterial(id)));
                case Tuple<object, uint> special:
                    return base.CreateMaterial(new Kn5MaterialDescription(special.Item1, _kn5.GetMaterial(special.Item2)));
                default:
                    return base.CreateMaterial(key);
            }
        }
    }
}