using System.ComponentModel;

namespace AcManager.Tools.Tyres {
    public enum TyresAppropriateLevel {
        [Description("Full match")]
        A = 0,

        [Description("Almost perfect")]
        B = 1,

        [Description("Good enough")]
        C = 2,

        [Description("Not recommended")]
        D = 3,

        [Description("Not recommended, way off")]
        E = 4,

        [Description("Completely different")]
        F = 5
    }
}