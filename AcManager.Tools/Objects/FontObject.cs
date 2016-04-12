using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Objects {
    public class FontObject : AcCommonSingleFileObject {
        public const string FontExtension = ".txt";

        public static readonly string[] BitmapExtensions = { ".bmp", ".png" };

        public override string Extension => FontExtension;

        public FontObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) { }

        public override string DisplayName => Id.ApartFromLast(".txt", StringComparison.OrdinalIgnoreCase);

        public override bool HasData => true;

        public override bool HandleChangedFile(string filename) {
            UpdateFontBitmap();
            return true;
        }

        private string _fontBitmap;

        public string FontBitmap {
            get { return _fontBitmap; }
            set {
                if (Equals(value, _fontBitmap)) return;
                _fontBitmap = value;
                OnPropertyChanged();
            }
        }

        private void UpdateFontBitmap() {
            var baseFilename = Location.ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase);
            FontBitmap = BitmapExtensions.Select(ext => baseFilename + ext).FirstOrDefault(File.Exists);
            ErrorIf(FontBitmap == null, AcErrorType.Font_BitmapIsMissing);
        }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            UpdateFontBitmap();
        }
    }
}