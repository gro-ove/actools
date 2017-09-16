using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificForward {
    public sealed class PaintShopFontSource {
        private PaintShopFontSource([NotNull] string familyName, [CanBeNull] string filename, [CanBeNull] byte[] data) {
            FamilyName = familyName;
            _filenameLazier = Lazier.Create(() => {
                if (filename != null) return filename;
                if (data == null) return null;

                string f;
                using (var sha1 = SHA1.Create()) {
                    f = Path.Combine(Path.GetTempPath(), "font-" + sha1.ComputeHash(data).ToHexString().ToLowerInvariant() + ".ttf");
                }
                if (!File.Exists(f)) {
                    File.WriteAllBytes(f, data);
                }
                return f;
            });
        }

        [NotNull]
        public static PaintShopFontSource CreateDefault() {
            return new PaintShopFontSource("Arial", null, null);
        }

        [NotNull]
        public static PaintShopFontSource FromFamilyName([NotNull] string familyName) {
            return new PaintShopFontSource(familyName, null, null);
        }

        [NotNull]
        public static PaintShopFontSource FromFilename([NotNull] string filename) {
            using (var collection = new PrivateFontCollection()) {
                collection.AddFontFile(filename);
                return new PaintShopFontSource(collection.Families[0].Name, filename, null);
            }
        }

        [NotNull]
        public static PaintShopFontSource FromMemory([NotNull] byte[] data) {
            using (var collection = new PrivateFontCollection()) {
                var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
                var pointer = pinned.AddrOfPinnedObject();
                collection.AddMemoryFont(pointer, data.Length);
                pinned.Free();
                return new PaintShopFontSource(collection.Families[0].Name, null, data);
            }
        }

        /*[CanBeNull]
        public Action<Factory> Initialize() {
            return Filename == null ? (Action<Factory>)null : factory => {
                factory.CreateFontFileReference(Filename);
            };
        }*/

        [NotNull]
        public string FamilyName { get; }

        private readonly Lazier<string> _filenameLazier;

        [CanBeNull]
        public string Filename => _filenameLazier.Value;
    }
}