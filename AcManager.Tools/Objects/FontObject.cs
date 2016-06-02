using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class FontObject : AcCommonSingleFileObject {
        public const char FirstChar = (char)32;
        public const char LastChar = (char)126;

        public static int CharToId(char c) => c < FirstChar || c > LastChar ? 0 : c - FirstChar;

        public const string FontExtension = ".txt";

        public static readonly string[] BitmapExtensions = { ".bmp", ".png" };

        public override string Extension => FontExtension;

        public FontObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {
            AcId = id.ApartFromLast(FontExtension);

            _usingsCarsIds = ValuesStorage.GetStringList(KeyUsingsCarsIds).ToArray();
            IsUsed = _usingsCarsIds.Any();
        }

        public override string DisplayName => Id.ApartFromLast(".txt", StringComparison.OrdinalIgnoreCase);

        public override bool HasData => true;

        public override bool HandleChangedFile(string filename) {
            UpdateFontBitmap();
            return true;
        }

        private string _fontBitmap;
        private bool _fontBitmapLoaded;
        private BitmapSource _fontBitmapImage;
        private bool _fontListLoaded;
        private List<double> _fontList;

        public string FontBitmap {
            get { return _fontBitmap; }
            set {
                if (Equals(value, _fontBitmap)) return;
                _fontBitmap = value;
                OnPropertyChanged();
            }
        }

        private CroppedBitmap _iconBitmap;
        private bool _iconBitmapLoaded;

        public CroppedBitmap IconBitmap {
            get {
                if (_iconBitmapLoaded) return _iconBitmap;

                _iconBitmapLoaded = true;
                _iconBitmap = BitmapForChar(SettingsHolder.Content.FontIconCharacter?.Cast<char?>().FirstOrDefault() ?? '3');
                return _iconBitmap;
            }
            set {
                if (Equals(value, _iconBitmap)) return;
                _iconBitmap = value;
                OnPropertyChanged();
            }
        }

        private ICommand _toggleCommand;
        public override ICommand ToggleCommand => _toggleCommand ?? (_toggleCommand = new RelayCommand(o => {
            if (Enabled && UsingsCarsIds.Length > 0 &&
                ModernDialog.ShowMessage("There are some cars which need this font. Are you sure you want to disable it?", "Disable Font",
                        MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try {
                Toggle();
            } catch (ToggleException ex) {
                NonfatalError.Notify(@"Can’t toggle: " + ex.Message, @"Make sure there is no runned app working with object's folder.");
            } catch (Exception ex) {
                NonfatalError.Notify(@"Can’t toggle", @"Make sure there is no runned app working with object's folder.", ex);
            }
        }));

        private ICommand _deleteCommand;

        public override ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new RelayCommand(o => {
            if (Enabled && UsingsCarsIds.Length > 0 &&
                ModernDialog.ShowMessage("There are some cars which need this font. Are you sure you want to delete it?", "Delete Font",
                        MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try {
                Delete();
            } catch (Exception ex) {
                NonfatalError.Notify(@"Can’t delete", @"Make sure there is no runned app working with object's folder.", ex);
            }
        }));

        private void UpdateFontBitmap() {
            _fontBitmapLoaded = false;
            _fontBitmapImage = null;
            _iconBitmapLoaded = false;
            OnPropertyChanged(nameof(IconBitmap));

            var baseFilename = Location.ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase);
            var bitmap = BitmapExtensions.Select(ext => baseFilename + ext).FirstOrDefault(File.Exists);

            if (bitmap != FontBitmap) {
                FontBitmap = bitmap;
            } else {
                OnImageChanged(nameof(FontBitmap));
            }

            ErrorIf(FontBitmap == null, AcErrorType.Font_BitmapIsMissing);
        }

        [CanBeNull]
        public CroppedBitmap BitmapForChar(char c) {
            if (!_fontBitmapLoaded) {
                _fontBitmapLoaded = true;
                _fontBitmapImage = UriToCachedImageConverter.Convert(FontBitmap);
            }

            if (!_fontListLoaded) {
                _fontListLoaded = true;

                try {
                    _fontList = File.ReadAllLines(Location).Select(line => double.Parse(line, CultureInfo.InvariantCulture)).ToList();
                } catch (Exception e) {
                    Logging.Warning("[FONTOBJECT] File damaged: " + e);
                }
            }

            if (_fontBitmapImage == null || _fontList == null) {
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
            return new CroppedBitmap(_fontBitmapImage, rect);
        }

        private string KeyUsingsCarsIds => "__tmp_FontObject.UsingsCarsIds_" + Id;

        [NotNull]
        private string[] _usingsCarsIds;

        [NotNull]
        public string[] UsingsCarsIds {
            get { return _usingsCarsIds; }
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
            get { return _isUsed; }
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
        }

        public void ResetIconBitmap() {
            _iconBitmapLoaded = false;
            OnPropertyChanged(nameof(IconBitmap));
        }
    }
}