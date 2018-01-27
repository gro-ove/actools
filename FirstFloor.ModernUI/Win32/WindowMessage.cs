using System;

namespace FirstFloor.ModernUI.Win32 {
    [Flags]
    internal enum WindowMessage {
        SystemCommand = 0x0112,
        EnterSizeMove = 0x0231,
        ExitSizeMove = 0x0232,
        DpiChanged = 0x02E0,
        DwmCompositionChanged = 0x031E
    }
}