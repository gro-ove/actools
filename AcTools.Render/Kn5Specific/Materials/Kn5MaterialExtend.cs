using AcTools.Kn5File;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Textures;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Materials {
    public static class Kn5MaterialExtend {
        public static float GetPropertyValueAByName(this Kn5Material mat, string name, float defaultValue = 0.0f) {
            var property = mat.GetPropertyByName(name);
            return property?.ValueA ?? defaultValue;
        }

        public static Vector3 GetPropertyValueCByName(this Kn5Material mat, string name, Vector3 defaultValue) {
            var property = mat.GetPropertyByName(name);
            return property?.ValueC.ToVector3() ?? defaultValue;
        }

        public static Vector3 GetPropertyValueCByName(this Kn5Material mat, string name) {
            return GetPropertyValueCByName(mat, name, Vector3.Zero);
        }

        public static void SetResource(this EffectResourceVariable variable, IRenderableTexture texture) {
            variable.SetResource(texture?.Resource);
        }
    }
}