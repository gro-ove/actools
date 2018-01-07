using System.Runtime.InteropServices;

namespace FirstFloor.ModernUI.Win32 {
    [StructLayout(LayoutKind.Sequential)]
    internal struct AccentPolicy {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;

        public static readonly int Size = Marshal.SizeOf(typeof(AccentPolicy));
    }
}