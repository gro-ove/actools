using AcTools.DataFile;
using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Data {
    internal static class IniExtension {
        public static Vector3 GetSlimVector3(this IniFileSection section, string key, Vector3 defaultValue = default(Vector3)) {
            return section.GetVector3F(key).ToVector3();
        }

        public static void SetSlimVector3(this IniFileSection section, string key, Vector3 value) {
            section.Set(key, value.ToArray());
        }
    }
}