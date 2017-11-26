namespace AcTools.Render.Utils {
    public static class PreferredDdsFormatExtension {
        public static bool IsAuto(this PreferredDdsFormat v) {
            return v == PreferredDdsFormat.Auto || v == PreferredDdsFormat.AutoTransparency;
        }

        public static bool IsHdr(this PreferredDdsFormat v) {
            return v == PreferredDdsFormat.HDR || v == PreferredDdsFormat.AutoHDR;
        }
    }
}