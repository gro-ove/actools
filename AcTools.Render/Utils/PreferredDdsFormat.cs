namespace AcTools.Render.Utils {
    public enum PreferredDdsFormat {
        // Detect optimal way based on size
        Auto, AutoTransparency,

        // Main lossy formats
        DXT1, DXT5,

        // Something special
        Luminance, LuminanceTransparency,

        // RGB(A), 8 bit per color
        NoCompression, NoCompressionTransparency,

        // Less bits per color
        RGB565, RGBA4444,

        // High dynamic range, the most heavy format
        HDR, AutoHDR
    }
}