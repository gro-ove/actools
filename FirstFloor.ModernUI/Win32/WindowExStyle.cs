using System;

namespace FirstFloor.ModernUI.Win32 {
    [Flags]
    internal enum WindowExStyle {
        NoActivate = 0x08000000,
        ToolWindow = 0x0080,
        AppWindow = 0x40000,
    }
}