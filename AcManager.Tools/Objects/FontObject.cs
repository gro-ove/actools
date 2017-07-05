using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class FontObjectBitmap {
        public FontObjectBitmap(string bitmapFilename, string fontFilename) :
                this(UriToCachedImageConverter.Convert(bitmapFilename), File.ReadAllBytes(fontFilename)) {}

        public FontObjectBitmap(byte[] bitmapData, byte[] fontData) :
                this((BitmapSource)BetterImage.LoadBitmapSourceFromBytes(bitmapData).BitmapSource, fontData) {}

        private FontObjectBitmap(BitmapSource font, byte[] fontData) {
            _fontBitmapImage = font;

            try {
                _fontList = fontData.ToUtf8String().Split('\n').Select(line => FlexibleParser.TryParseDouble(line) ?? 0d).ToList();
            } catch (Exception e) {
                Logging.Warning("File damaged: " + e);
            }
        }

        private readonly BitmapSource _fontBitmapImage;
        private readonly List<double> _fontList;

        private const char FirstChar = (char)32;
        private const char LastChar = (char)126;

        private static int CharToId(char c) => c < FirstChar || c > LastChar ? 0 : c - FirstChar;

        [CanBeNull]
        public CroppedBitmap BitmapForChar(char c) {
            if (_fontBitmapImage == null) {
                Logging.Warning("_fontBitmapImage == null!");
                return null;
            }

            if (_fontList == null) {
                Logging.Warning("_fontList == null!");
                return null;
            }

            var i = CharToId(c);
            var x = _fontList[i];
            var width = (i + 1 == _fontList.Count ? 1d : _fontList[i + 1]) - x;

            if (x + width <= 0d || x >= 1d) return null;
            if (x < 0) {
                width += x;
                x = 0d;
            }

            width = Math.Min(width, 1d - x);

            var rect = new Int32Rect((int)(x * _fontBitmapImage.PixelWidth), 0, (int)(width * _fontBitmapImage.PixelWidth), _fontBitmapImage.PixelHeight);
            var result = new CroppedBitmap(_fontBitmapImage, rect);
            result.Freeze();
            return result;
        }

        public CroppedBitmap GetIcon() {
            return BitmapForChar(SettingsHolder.Content.FontIconCharacter?.Cast<char?>().FirstOrDefault() ?? '3');
        }
    }

    public class FontObject : AcCommonSingleFileObject, IAcObjectAuthorInformation {
        public const string FontExtension = ".txt";

        public static readonly string[] BitmapExtensions = { @".bmp", @".png" };

        public override string Extension => FontExtension;

        public FontObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {
            AcId = id.ApartFromLast(FontExtension);

            _usingsCarsIds = ValuesStorage.GetStringList(KeyUsingsCarsIds).ToArray();
            IsUsed = _usingsCarsIds.Any();
        }

        public override string DisplayName => Id.ApartFromLast(@".txt", StringComparison.OrdinalIgnoreCase);

        public override bool HasData => true;

        public override bool HandleChangedFile(string filename) {
            UpdateFontBitmap();
            return true;
        }

        private FontObjectBitmap _fontObjectBitmap;
        public FontObjectBitmap FontObjectBitmap => _fontObjectBitmap ?? (_fontObjectBitmap = new FontObjectBitmap(FontBitmap, Location));

        private void UpdateFontBitmap() {
            _fontObjectBitmap = null;
            _iconBitmapLoaded = false;
            OnPropertyChanged(nameof(IconBitmap));

            var baseFilename = Location.ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase);
            var bitmap = BitmapExtensions.Select(ext => baseFilename + ext).FirstOrDefault(File.Exists);

            if (bitmap != FontBitmap) {
                FontBitmap = bitmap;
            } else {
                OnImageChangedValue(FontBitmap);
            }

            ErrorIf(FontBitmap == null, AcErrorType.Font_BitmapIsMissing);
        }

        private string _fontBitmap;

        public string FontBitmap {
            get => _fontBitmap;
            set {
                if (Equals(value, _fontBitmap)) return;
                _fontBitmap = value;
                _fontObjectBitmap = null;
                _iconBitmapLoaded = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IconBitmap));
            }
        }

        private CroppedBitmap _iconBitmap;
        private bool _iconBitmapLoaded;

        private async Task LoadIcon() {
            _iconBitmapLoaded = true;

            try {
                _iconBitmap = await Task.Run(() => FontObjectBitmap.GetIcon());
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t load font’s icon", e);
            }

            OnPropertyChanged(nameof(IconBitmap));
        }

        public CroppedBitmap IconBitmap {
            get {
                if (!_iconBitmapLoaded) {
                    LoadIcon().Forget();
                }

                return _iconBitmap;
            }
        }

        protected override Task ToggleOverrideAsync() {
            if (Enabled && UsingsCarsIds.Length > 0 &&
                ModernDialog.ShowMessage(ToolsStrings.FontObject_Disabling_SomeCarsNeedThisFont, ToolsStrings.FontObject_DisableFont,
                        MessageBoxButton.YesNo) != MessageBoxResult.Yes) return Task.Delay(0);
            return base.ToggleOverrideAsync();
        }

        protected override Task DeleteOverrideAsync() {
            if (Enabled && UsingsCarsIds.Length > 0 &&
                ModernDialog.ShowMessage(ToolsStrings.FontObject_Deleting_SomeCarsNeedThisFont, ToolsStrings.FontObject_DeleteFont,
                        MessageBoxButton.YesNo) != MessageBoxResult.Yes) return Task.Delay(0);
            return base.DeleteOverrideAsync();
        }

        private string KeyUsingsCarsIds => @"__tmp_FontObject.UsingsCarsIds_" + Id;

        [NotNull]
        private string[] _usingsCarsIds;

        [NotNull]
        public string[] UsingsCarsIds {
            get => _usingsCarsIds;
            set {
                if (Equals(value, _usingsCarsIds)) return;
                _usingsCarsIds = value;
                OnPropertyChanged();

                IsUsed = value.Any();
                ValuesStorage.Set(KeyUsingsCarsIds, value);
            }
        }

        private bool _isUsed;

        public bool IsUsed {
            get => _isUsed;
            set {
                if (Equals(value, _isUsed)) return;
                _isUsed = value;
                OnPropertyChanged();

                ErrorIf(IsUsed && !Enabled, AcErrorType.Font_UsedButDisabled);
            }
        }

        public string AcId { get; }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            UpdateFontBitmap();
            ErrorIf(IsUsed && !Enabled, AcErrorType.Font_UsedButDisabled);

            try {
                Author = (DataProvider.Instance.KunosContent[@"fonts"]?.Contains(Id) ?? false) ? AuthorKunos : null;
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        public void ResetIconBitmap() {
            _iconBitmapLoaded = false;
            OnPropertyChanged(nameof(IconBitmap));
        }

        public string Author { get; private set; }

        #region Packing
        private class FontPacker : AcCommonObjectPacker<FontObject> {
            protected override string GetBasePath(FontObject t) {
                return "content/fonts";
            }

            protected override IEnumerable PackOverride(FontObject t) {
                yield return AddFilename(Path.GetFileName(t.Location), t.Location);

                if (t.FontBitmap != null) {
                    yield return AddFilename(Path.GetFileName(t.FontBitmap), t.FontBitmap);
                }
            }

            protected override PackedDescription GetDescriptionOverride(FontObject t) {
                return new PackedDescription(t.Id, t.Name, null, FontsManager.Instance.Directories.GetMainDirectory(), true);
            }
        }

        protected override AcCommonObjectPacker CreatePacker() {
            return new FontPacker();
        }
        #endregion
    }
}