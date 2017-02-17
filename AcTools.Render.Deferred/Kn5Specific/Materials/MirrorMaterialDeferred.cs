using AcTools.Render.Deferred.Shaders;
using AcTools.Render.Shaders;

namespace AcTools.Render.Deferred.Kn5Specific.Materials {
    public class MirrorMaterialDeferred : Kn5MaterialDeferred {
        private static readonly EffectDeferredGObject.Material Material = new EffectDeferredGObject.Material {
            Diffuse = 0,
            Ambient = 0,
            FresnelMaxLevel = 1,
            FresnelC = 1,
            FresnelExp = 0,
            SpecularExp = 400,
            Specular = 1
        };
        
        public MirrorMaterialDeferred() : base(Material, false) { }
    }
}