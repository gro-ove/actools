using System;
using AcTools.Render.Utils;
using JetBrains.Annotations;

namespace AcManager.PaintShop {
    public class TextureFileName {
        [NotNull]
        public readonly string FileName;
        public readonly PreferredDdsFormat PreferredFormat;

        public TextureFileName([NotNull] string name, PreferredDdsFormat format) {
            FileName = name ?? throw new ArgumentNullException(nameof(name));
            PreferredFormat = format;
        }

        public TextureFileName([NotNull] string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));
            var index = name.IndexOf(':');
            if (index == -1) {
                FileName = name;
                PreferredFormat = PreferredDdsFormat.AutoTransparency;
            } else {
                FileName = name.Substring(0, index);
                PreferredFormat = ParseFormat(name.Substring(index + 1));
            }
        }

        private static PreferredDdsFormat ParseFormat(string format) {
            switch (format.Trim().ToLowerInvariant()) {
                case "dxt1":
                    return PreferredDdsFormat.DXT1;

                case "dxt":
                case "dxt5":
                    return PreferredDdsFormat.DXT5;

                case "l":
                case "lum":
                case "luminance":
                    return PreferredDdsFormat.Luminance;

                case "la":
                case "lumalpha":
                case "luminancealpha":
                    return PreferredDdsFormat.LuminanceTransparency;

                case "rgb565":
                case "rgb5650":
                case "565":
                case "5650":
                    return PreferredDdsFormat.RGB565;

                case "rgba4444":
                case "4444":
                    return PreferredDdsFormat.RGBA4444;

                case "rgba":
                case "rgba8888":
                case "8888":
                    return PreferredDdsFormat.NoCompressionTransparency;

                case "rgb":
                case "rgb888":
                case "rgba8880":
                case "888":
                case "8880":
                    return PreferredDdsFormat.NoCompression;
            }

            return Enum.TryParse(format, true, out PreferredDdsFormat result) ?
                    result : PreferredDdsFormat.AutoTransparency;
        }

        public override int GetHashCode() {
            return FileName.GetHashCode();
        }

        public override bool Equals(object obj) {
            return FileName.Equals((obj as TextureFileName)?.FileName);
        }
    }
}