using System.Runtime.InteropServices;

namespace FirstFloor.ModernUI.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT {
        public int left, top, right, bottom;
    }
}
