using System;
using System.Collections;
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
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
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

        private bool _fontObjectBitmapReady;
        private Task<FontObjectBitmap> _fontObjectBitmapTask;
        private FontObjectBitmap _fontObjectBitmapCache;

        [ItemCanBeNull]
        public Task<FontObjectBitmap> GetFontObjectBitmapAsync() {
            if (_fontObjectBitmapReady) {
                return Task.FromResult(_fontObjectBitmapCache);
            }

            if (_fontObjectBitmapTask != null) {
                return _fontObjectBitmapTask;
            }

            return _fontObjectBitmapTask = LoadBitmapAsync();
            async Task<FontObjectBitmap> LoadBitmapAsync() {
                await Task.Yield();
                var ret = await FontObjectBitmap.CreateAsync(FontBitmap, Location);
                _fontObjectBitmapReady = true;
                _fontObjectBitmapCache = ret;
                _fontObjectBitmapTask = null;
                return ret;
            }
        }

        private void ResetFontObjectCache() {
            _fontObjectBitmapReady = false;
        }

        private void UpdateFontBitmap() {
            ResetFontObjectCache();
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
                _iconBitmapLoaded = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IconBitmap));
                ResetFontObjectCache();
            }
        }

        private CroppedBitmap _iconBitmap;
        private bool _iconBitmapLoaded;

        private async Task LoadIcon() {
            _iconBitmapLoaded = true;
            try {
                _iconBitmap = (await GetFontObjectBitmapAsync())?.GetIcon();
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t load font’s icon", e);
            }
            OnPropertyChanged(nameof(IconBitmap));
        }

        public CroppedBitmap IconBitmap {
            get {
                if (!_iconBitmapLoaded) {
                    LoadIcon().Ignore();
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
                ValuesStorage.Storage.SetStringList(KeyUsingsCarsIds, value);
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
                Author = (DataProvider.Instance.GetKunosContentIds(@"fonts")?.ArrayContains(Id) ?? false) ? AuthorKunos : null;
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