using System;

namespace FirstFloor.ModernUI.Windows.Controls {
    public enum SpecialMode {
        /* disabled */
        None,

        /* numbers */
        Number,
        Integer,

        [Obsolete]
        Positive,

        /* complicated strings */
        Time,
        Version,

        /* numbers with alternate string for some cases */
        IntegerOrLabel,

        [Obsolete]
        IntegerOrZeroLabel,
        [Obsolete]
        IntegerOrMinusOneLabel,
    }
}