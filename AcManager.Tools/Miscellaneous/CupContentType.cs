using System.ComponentModel;

namespace AcManager.Tools.Miscellaneous {
    public enum CupContentType {
        [Description("Python app")]
        App = 100,
        
        [Description("Lua app")]
        LuaApp = 110,
        
        [Description("Car")]
        Car = 500,
        
        [Description("Track")]
        Track = 510,
        
        [Description("Showroom")]
        Showroom = 520,
        
        [Description("PP filter")]
        Filter = 600,
    }

    public static class CupContentTypeUtils {
        public static string CupID(this CupContentType type) {
            return type.ToString().ToLowerInvariant();
        }
    }
}