using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    public class FontObject : AcCommonSingleFileObject {
        public const string FontExtension = ".txt";

        public override string Extension => FontExtension;

        public FontObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) { }

        public override string DisplayName => Id.ApartFromLast(".txt", StringComparison.OrdinalIgnoreCase);

        public override bool HasData => true;

        private string _fontBitmap;

        public string FontBitmap {
            get { return _fontBitmap; }
            set {
                if (Equals(value, _fontBitmap)) return;
                _fontBitmap = value;
                OnPropertyChanged();
            }
        }

        private string FindFontBitmap() {
            var baseFilename = Location.ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase);
            return new [] { ".bmp", ".png" }.Select(ext => baseFilename + ext).FirstOrDefault(File.Exists);
        }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            FontBitmap = FindFontBitmap();
            ErrorIf(FontBitmap == null, AcErrorType.Font_BitmapIsMissing);
        }
    }
}