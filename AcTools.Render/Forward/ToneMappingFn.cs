using System.ComponentModel;

namespace AcTools.Render.Forward {
    public enum ToneMappingFn {
        None = 0,

        [Description("Reinhard (H.)")]
        Reinhard = 1,

        [Description("Reinhard (Filmic)")]
        FilmicReinhard = 2,

        [Description("Filmic")]
        Filmic = 3,

        [Description("Luma-Based")]
        LumaBasedReinhard = 5,

        [Description("White-P. Luma-Based")]
        WhitePreservingLumaBasedReinhard = 6,

        [Description("Uncharted 2")]
        Uncharted2 = 4,
    }
}