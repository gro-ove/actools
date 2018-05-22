using System.Runtime.InteropServices;

namespace FirstFloor.ModernUI.Win32 {
    // Feels like it doesn’t really work, Windows 10 just isn’t there yet. Also, this way sharing
    // might be cancelled later, and if user will try again, it would be nice not to ask him details.

    [StructLayout(LayoutKind.Sequential)]
    internal struct AccentPolicy {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;

        public static readonly int Size = Marshal.SizeOf(typeof(AccentPolicy));
    }
}