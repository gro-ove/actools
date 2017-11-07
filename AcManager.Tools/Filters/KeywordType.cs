using System;

namespace AcManager.Tools.Filters {
    [Flags]
    public enum KeywordType {
        Child = 1 << 0,
        String = 1 << 1,
        Flag = 1 << 2,
        Number = 1 << 3,
        Distance = 1 << 4,
        DateTime = 1 << 5,
        TimeSpan = 1 << 6,
        FileSize = 1 << 7,
    }
}