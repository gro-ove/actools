namespace AcTools.Kn5File {
    internal static class Kn5MaterialExtension {
        public static bool IsValidDepthMode(this int v) {
            return v >= 0 && v <= 2;
        }

        public static bool IsValidBlendMode(this byte v) {
            return v <= 2;
        }
    }
}