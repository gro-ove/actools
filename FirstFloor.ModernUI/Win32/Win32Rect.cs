using System.Runtime.InteropServices;

namespace FirstFloor.ModernUI.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    internal struct Win32Rect {
        public int Left, Top, Right, Bottom;
    }
}
