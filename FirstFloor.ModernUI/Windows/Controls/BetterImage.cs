using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BetterImage : Control {
        public static bool OptionBackgroundLoading = true;
        public static long OptionCacheTotalSize = 100 * 1000 * 100;
        public static long OptionCacheLimitSize = 100 * 1000;
        public static bool OptionMarkCached = false;

        #region Wrapper with size (for solving DPI-related problems)
        public struct BitmapEntry {
            public readonly ImageSource BitmapSource;
            public readonly int Width, Height;
            public readonly bool Downsized;
            public readonly DateTime Date;

            public int Size => Width * Height;

            public static BitmapEntry Empty => new BitmapEntry();

            public BitmapEntry(ImageSource source, int width, int height, bool downsized) {
                BitmapSource = source;
                Width = width;
                Height = height;
                Downsized = downsized;
                Date = DateTime.Now;
            }

            public bool IsBroken => BitmapSource == null;
        }
        #endregion

        #region Properties
        public static readonly DependencyProperty ShowBrokenProperty = DependencyProperty.Register(nameof(ShowBroken), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(true));

        public bool ShowBroken {
            get { return (bool)GetValue(ShowBrokenProperty); }
            set { SetValue(ShowBrokenProperty, value); }
        }

        public static readonly DependencyProperty HideBrokenProperty = DependencyProperty.Register(nameof(HideBroken), typeof(bool),
                typeof(BetterImage));

        public bool HideBroken {
            get { return (bool)GetValue(HideBrokenProperty); }
            set { SetValue(HideBrokenProperty, value); }
        }

        public static readonly DependencyProperty ClearOnChangeProperty = DependencyProperty.Register(nameof(ClearOnChange), typeof(bool),
                typeof(BetterImage));

        public bool ClearOnChange {
            get { return (bool)GetValue(ClearOnChangeProperty); }
            set { SetValue(ClearOnChangeProperty, value); }
        }

        public static readonly DependencyProperty DelayedCreationProperty = DependencyProperty.Register(nameof(DelayedCreation), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(true));

        public bool DelayedCreation {
            get { return (bool)GetValue(DelayedCreationProperty); }
            set { SetValue(DelayedCreationProperty, value); }
        }

        public static readonly DependencyProperty DecodeHeightProperty = DependencyProperty.Register(nameof(DecodeHeight), typeof(int),
                typeof(BetterImage), new PropertyMetadata(-1));

        public int DecodeHeight {
            get { return (int)GetValue(DecodeHeightProperty); }
            set { SetValue(DecodeHeightProperty, value); }
        }

        /// <summary>
        /// Set to 0 if value shouldn’t be copied from Width.
        /// </summary>
        public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.Register(nameof(DecodeWidth), typeof(int),
                typeof(BetterImage), new PropertyMetadata(-1));

        public int DecodeWidth {
            get { return (int)GetValue(DecodeWidthProperty); }
            set { SetValue(DecodeWidthProperty, value); }
        }

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(nameof(Stretch), typeof(Stretch),
                typeof(BetterImage));

        public Stretch Stretch {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(nameof(StretchDirection), typeof(StretchDirection),
                typeof(BetterImage));

        public StretchDirection StretchDirection {
            get { return (StretchDirection)GetValue(StretchDirectionProperty); }
            set { SetValue(StretchDirectionProperty, value); }
        }

        public static readonly DependencyProperty FilenameProperty = DependencyProperty.Register(nameof(Filename), typeof(string),
                typeof(BetterImage), new PropertyMetadata(OnFilenameChanged));

        public string Filename {
            get { return (string)GetValue(FilenameProperty); }
            set { SetValue(FilenameProperty, value); }
        }

        private static void OnFilenameChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterImage)o).OnFilenameChanged((string)e.NewValue);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object),
                typeof(BetterImage), new PropertyMetadata(OnSourceChanged));

        public object Source {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            var potentialFilename = e.NewValue as string;
            if (potentialFilename != null) {
                ((BetterImage)o).Filename = potentialFilename;
            } else if (e.NewValue is BitmapEntry) {
                ((BetterImage)o).SetBitmapEntryDirectly((BitmapEntry)e.NewValue);
            } else {
                ((BetterImage)o).OnSourceChanged((ImageSource)e.NewValue);
            }
        }

        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource),
                typeof(BetterImage), new PropertyMetadata(OnImageSourceChanged));

        public ImageSource ImageSource {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        private static void OnImageSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterImage)o).Source = e.NewValue;
        }

        private void SetBitmapEntryDirectly(BitmapEntry value) {
            Filename = null;
            ++_loading;
            _current = value;
            _broken = value.IsBroken;
            InvalidateMeasure();
            InvalidateVisual();
        }

        private void OnSourceChanged(ImageSource newValue) {
            SetBitmapEntryDirectly(newValue == null ? BitmapEntry.Empty : new BitmapEntry(newValue, (int)newValue.Width, (int)newValue.Height, false));
        }

        public static readonly DependencyProperty ForceFillProperty = DependencyProperty.Register(nameof(ForceFill), typeof(bool),
                typeof(BetterImage));

        public bool ForceFill {
            get { return (bool)GetValue(ForceFillProperty); }
            set { SetValue(ForceFillProperty, value); }
        }
        #endregion

        #region Loading
        public static BitmapEntry LoadBitmapSource(string filename, int decodeWidth = -1, int decodeHeight = -1) {
            if (string.IsNullOrEmpty(filename)) {
                return BitmapEntry.Empty;
            }

            Uri uri;
            try {
                uri = new Uri(filename);
            } catch (Exception) {
                return BitmapEntry.Empty;
            }

            try {
                var bi = new BitmapImage();
                bi.BeginInit();

                var width = 0;
                var height = 0;
                var downsized = false;

                if (decodeWidth > 0) {
                    bi.DecodePixelWidth = (int)(decodeWidth * DpiAwareWindow.OptionScale);
                    width = decodeWidth;
                    downsized = true;
                }

                if (decodeHeight > 0) {
                    bi.DecodePixelHeight = (int)(decodeHeight * DpiAwareWindow.OptionScale);
                    height = decodeHeight;
                    downsized = true;
                }

                var remote = uri.Scheme == "http" || uri.Scheme == "https";
                bi.CreateOptions = remote ? BitmapCreateOptions.None : BitmapCreateOptions.IgnoreImageCache;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = uri;
                bi.EndInit();

                if (!remote) {
                    bi.Freeze();
                }

                if (decodeWidth <= 0) {
                    width = bi.PixelWidth;
                }

                if (decodeHeight <= 0) {
                    height = bi.PixelHeight;
                }

                return new BitmapEntry(bi, width, height, downsized);
            } catch (FileNotFoundException) {
                return BitmapEntry.Empty;
            } catch (Exception e) {
                Logging.Warning("Loading failed: " + e);
                return BitmapEntry.Empty;
            }
        }

        [ItemNotNull]
        private static async Task<byte[]> ReadAllBytesAsync(string filename, CancellationToken cancellation = default(CancellationToken)) {
            if (filename.StartsWith(@"/")) {
                try {
                    var stream = Application.GetResourceStream(new Uri(filename, UriKind.Relative))?.Stream;
                    if (stream != null) {
                        using (stream) {
                            var result = new byte[stream.Length];
                            await stream.ReadAsync(result, 0, (int)stream.Length, cancellation);
                            return result;
                        }
                    }
                } catch (Exception) {
                    // ignored
                }
            }

            using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                var result = new byte[stream.Length];
                await stream.ReadAsync(result, 0, (int)stream.Length, cancellation);
                return result;
            }
        }

        /// <summary>
        /// Safe (handles all exceptions inside).
        /// </summary>
        private static BitmapEntry LoadBitmapSourceFromBytes(byte[] data, int decodeWidth = -1, int decodeHeight = -1, int attempt = 0) {
            try {
                // for more information about WrappingStream: https://code.logos.com/blog/2008/04/memory_leak_with_bitmapimage_and_memorystream.html
                using (var stream = new WrappingStream(new MemoryStream(data))) {
                    int width = 0, height = 0;
                    var downsized = false;

                    var bi = new BitmapImage();
                    bi.BeginInit();

                    if (decodeWidth > 0) {
                        bi.DecodePixelWidth = (int)(decodeWidth * DpiAwareWindow.OptionScale);
                        width = bi.DecodePixelWidth;
                        downsized = true;
                    }

                    if (decodeHeight > 0) {
                        bi.DecodePixelHeight = (int)(decodeHeight * DpiAwareWindow.OptionScale);
                        height = bi.DecodePixelHeight;
                        downsized = true;
                    }

                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = stream;
                    bi.EndInit();
                    bi.Freeze();

                    if (decodeWidth <= 0) {
                        width = bi.PixelWidth;
                    }

                    if (decodeHeight <= 0) {
                        height = bi.PixelHeight;
                    }

                    return new BitmapEntry(bi, width, height, downsized);
                }
            } catch (FileFormatException e) {
                // I AM THE GOD OF STUPID WORKAROUNDS, AND I BRING YOU, WORKAROUND!
                if (attempt++ < 2 && e.InnerException is COMException) {
                    Logging.Warning("Recover attempt: " + attempt);
                    return LoadBitmapSourceFromBytes(data, decodeWidth, decodeHeight, attempt);
                }

                Logging.Warning("Loading failed: " + e);
                return BitmapEntry.Empty;
            } catch (Exception e) {
                Logging.Warning("Loading failed: " + e);
                return BitmapEntry.Empty;
            }
        }

        public static async Task<BitmapEntry> LoadBitmapSourceAsync(string filename, int decodeWidth = -1, int decodeHeight = -1) {
            if (string.IsNullOrEmpty(filename)) {
                return BitmapEntry.Empty;
            }

            try {
                return LoadBitmapSourceFromBytes(await ReadAllBytesAsync(filename), decodeWidth, decodeHeight);
            } catch (Exception) {
                return BitmapEntry.Empty;
            }
        }
        #endregion

        #region Static methods
        /// <summary>
        /// Forces all BetterImages which showing specific filename to reload their content.
        /// </summary>
        /// <param name="filename"></param>
        public static void ReloadImage(string filename) {
            RemoveFromCache(filename);
            var app = Application.Current;
            if (app == null) return;
            foreach (var image in app.Windows.OfType<Window>()
                    .SelectMany(VisualTreeHelperEx.FindVisualChildren<BetterImage>)
                    .Where(x => string.Equals(x._filename, filename, StringComparison.OrdinalIgnoreCase))) {
                image.OnFilenameChanged(filename);
            }
        }
        #endregion

        #region Cache
        private class CacheEntry {
            public string Key;
            public BitmapEntry Value;
        }

        private static readonly List<CacheEntry> Cache = new List<CacheEntry>(100);

        private static void RemoveFromCache(string filename) {
            lock (Cache) {
                var i = Cache.FindIndex(x => string.Equals(x.Key, filename, StringComparison.OrdinalIgnoreCase));
                if (i != -1) {
                    Cache.RemoveAt(i);
                }
            }
        }

        private void AddToCache(string filename, BitmapEntry entry) {
            _fromCache = false;
            if (OptionCacheTotalSize <= 0 || entry.Size > OptionCacheLimitSize) return;

            lock (Cache) {
                var i = Cache.FindIndex(x => string.Equals(x.Key, filename, StringComparison.OrdinalIgnoreCase));
                CacheEntry item;
                if (i == -1) {
                    item = new CacheEntry {
                        Key = filename,
                        Value = entry
                    };
                    Cache.Insert(0, item);
                } else {
                    item = Cache[i];
                    item.Value = entry;

                    if (i > 0) {
                        Cache.RemoveAt(i);
                        Cache.Insert(0, item);
                    }
                }

                // removing old entries
                var total = item.Value.Size;
                for (i = 1; i < Cache.Count; i++) {
                    total += Cache[i].Value.Size;
                    if (total > OptionCacheTotalSize) {
                        Cache.RemoveRange(i, Cache.Count - i);
                        return;
                    }
                }
            }
        }

        private static BitmapEntry GetCached(string filename) {
            lock (Cache) {
                var i = Cache.FindIndex(x => string.Equals(x.Key, filename, StringComparison.OrdinalIgnoreCase));
                if (i == -1) return BitmapEntry.Empty;

                var item = Cache[i];
                if (i > 0) {
                    Cache.RemoveAt(i);
                    Cache.Insert(0, item);
                }

                return item.Value;
            }
        }
        #endregion

        #region Inner control methods
        public int InnerDecodeWidth {
            get {
                var decodeWidth = DecodeWidth;
                if (decodeWidth >= 0) return decodeWidth;

                var maxWidth = MaxWidth;
                if (!double.IsPositiveInfinity(maxWidth)) return (int)maxWidth;

                var width = Width;
                if (!double.IsNaN(width)) return (int)width;
                return -1;
            }
        }

        public int InnerDecodeHeight => DecodeHeight;

        private BitmapEntry _current;
        private int _loading;
        private bool _broken;
        private bool _fromCache;

        /// <summary>
        /// Reloads image.
        /// </summary>
        /// <returns>Returns true if image will be loaded later, and false if image is ready.</returns>
        private bool ReloadImage() {
            if (OptionCacheTotalSize > 0) {
                var cached = GetCached(_filename);
                var innerDecodeWidth = InnerDecodeWidth;
                if (cached.BitmapSource != null && (innerDecodeWidth == -1 ? !cached.Downsized : cached.Width >= innerDecodeWidth)) {
                    try {
                        var info = new FileInfo(_filename);
                        if (!info.Exists) {
                            _current = BitmapEntry.Empty;
                            _broken = true;
                            RemoveFromCache(_filename);
                            return false;
                        }

                        if (info.LastWriteTime > cached.Date) {
                            RemoveFromCache(_filename);
                        } else {
                            _current = cached;
                            _broken = false;
                            _fromCache = true;
                            InvalidateMeasure();
                            InvalidateVisual();
                            return false;
                        }
                    } catch (Exception) {
                        // If there will be any problems with FileInfo
                        _current = BitmapEntry.Empty;
                        _broken = true;
                        RemoveFromCache(_filename);
                        return false;
                    }
                }
            }

            if (DelayedCreation) {
                if (OptionBackgroundLoading) {
                    ReloadImageAsync();
                } else {
                    ReloadImageAsyncOld();
                }
                return true;
            }

            ++_loading;
            _current = LoadBitmapSource(_filename, InnerDecodeWidth, InnerDecodeHeight);
            _broken = _current.BitmapSource == null;
            if (!_broken) {
                AddToCache(_filename, _current);
            }

            InvalidateVisual();
            return false;
        }

        private static readonly List<DateTime> RecentlyLoaded = new List<DateTime>(50);

        private static void UpdateLoaded() {
            var now = DateTime.Now;
            var remove = RecentlyLoaded.TakeWhile(x => now - x > TimeSpan.FromSeconds(3)).Count();
            if (remove > 0) {
                RecentlyLoaded.RemoveRange(0, remove);
            }

            RecentlyLoaded.Add(now);
        }

        private async void ReloadImageAsyncOld() {
            var loading = ++_loading;
            _broken = false;

            var current = await LoadBitmapSourceAsync(_filename, InnerDecodeWidth, InnerDecodeHeight);
            if (loading != _loading) return;

            _current = current;
            _broken = _current.BitmapSource == null;
            if (!_broken) {
                AddToCache(_filename, _current);
            }

            InvalidateMeasure();
            InvalidateVisual();
        }

        private async void ReloadImageAsync() {
            var loading = ++_loading;
            _broken = false;

            var filename = _filename;

            byte[] data;
            if (string.IsNullOrEmpty(filename)) {
                data = null;
            } else {
                try {
                    data = await ReadAllBytesAsync(filename);
                } catch (Exception) {
                    data = null;
                }
            }

            if (loading != _loading || _filename != filename) return;
            if (data == null) {
                _current = BitmapEntry.Empty;
                _broken = true;

                InvalidateMeasure();
                InvalidateVisual();
                return;
            }

            var innerDecodeWidth = InnerDecodeWidth;
            var innerDecodeHeight = InnerDecodeHeight;

            UpdateLoaded();
            if (RecentlyLoaded.Count < 2 || data.Length < 1000) {
                _current = LoadBitmapSourceFromBytes(data, innerDecodeWidth, innerDecodeHeight);
                _broken = _current.BitmapSource == null;
                if (!_broken) {
                    AddToCache(filename, _current);
                }

                InvalidateMeasure();
                InvalidateVisual();
                return;
            }

            if (RecentlyLoaded.Count > 5) {
                await Task.Delay((RecentlyLoaded.Count - 3) * 10);
                if (loading != _loading || _filename != filename) return;
            }

            ThreadPool.QueueUserWorkItem(o => {
                var current = LoadBitmapSourceFromBytes(data, innerDecodeWidth, innerDecodeHeight);
                Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)(() => {
                    if (loading != _loading || _filename != filename) return;

                    _current = current;
                    _broken = _current.IsBroken;
                    if (!_broken) {
                        AddToCache(filename, _current);
                    }

                    InvalidateMeasure();
                    InvalidateVisual();
                }));
            });
        }

        private string _filename;

        private void OnFilenameChanged(string value) {
            _filename = value;
            if (ReloadImage() && ClearOnChange) {
                _current = BitmapEntry.Empty;
                _broken = false;
                InvalidateVisual();
            }
        }

        private Size _size;
        private Point _offset;

        protected override Size MeasureOverride(Size constraint) {
            if (_current.BitmapSource == null && HideBroken) return new Size();

            _size = MeasureArrangeHelper(constraint);

            var forceFill = ForceFill;
            return new Size(forceFill && !double.IsPositiveInfinity(constraint.Width) ? constraint.Width : Math.Min(_size.Width, constraint.Width),
                    forceFill && !double.IsPositiveInfinity(constraint.Height) ? constraint.Height : Math.Min(_size.Height, constraint.Height));
        }

        private double HorizontalContentAlignmentMultipler => HorizontalContentAlignment == HorizontalAlignment.Center ? 0.5d :
                HorizontalContentAlignment == HorizontalAlignment.Right ? 1d : 0d;

        private double VerticalContentAlignmentMultipler => VerticalContentAlignment == VerticalAlignment.Center ? 0.5d :
                VerticalContentAlignment == VerticalAlignment.Bottom ? 1d : 0d;

        protected override Size ArrangeOverride(Size arrangeSize) {
            if (_current.BitmapSource == null && HideBroken) return new Size();

            _size = MeasureArrangeHelper(arrangeSize);
            _offset = new Point((arrangeSize.Width - _size.Width) * HorizontalContentAlignmentMultipler,
                    (arrangeSize.Height - _size.Height) * VerticalContentAlignmentMultipler);
            return new Size(double.IsPositiveInfinity(arrangeSize.Width) ? _size.Width : arrangeSize.Width,
                    double.IsPositiveInfinity(arrangeSize.Height) ? _size.Height : arrangeSize.Height);
        }

        private static readonly FormattedText CachedFormattedText = new FormattedText("Cached", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface(new FontFamily(@"Arial").ToString()), 12, Brushes.Red);

        protected override void OnRender(DrawingContext dc) {
            if (_current.BitmapSource == null && HideBroken) return;

            dc.DrawRectangle(Background, null, new Rect(new Point(), RenderSize));

            if (_current.BitmapSource != null) {
                dc.DrawImage(_current.BitmapSource, new Rect(_offset, _size));
            } else if (ShowBroken && _broken) {
                dc.DrawImage(BrokenIcon.ImageSource,
                        new Rect(new Point((RenderSize.Width - BrokenIcon.Width) / 2d, (RenderSize.Height - BrokenIcon.Height) / 2d), BrokenIcon.Size));
            }

            if (OptionMarkCached && _fromCache) {
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, 255, 255, 0)), null, new Rect(_offset, _size));
                dc.DrawText(CachedFormattedText, new Point(_offset.X + 4, _offset.Y + 4));
            }
        }

        private static Size ComputeScaleFactor(Size availableSize, Size contentSize, Stretch stretch, StretchDirection stretchDirection) {
            var unlimitedWidth = double.IsPositiveInfinity(availableSize.Width);
            var unlimitedHeight = double.IsPositiveInfinity(availableSize.Height);
            if (stretch != Stretch.Uniform && stretch != Stretch.UniformToFill && stretch != Stretch.Fill || unlimitedWidth && unlimitedHeight) {
                return new Size(1d, 1d);
            }

            var scaleX = Equals(contentSize.Width, 0d) ? 0.0 : availableSize.Width / contentSize.Width;
            var scaleY = Equals(contentSize.Height, 0d) ? 0.0 : availableSize.Height / contentSize.Height;
            if (unlimitedWidth) {
                scaleX = scaleY;
            } else if (unlimitedHeight) {
                scaleY = scaleX;
            } else {
                switch (stretch) {
                    case Stretch.Uniform:
                        scaleX = scaleY = scaleX < scaleY ? scaleX : scaleY;
                        break;
                    case Stretch.UniformToFill:
                        scaleX = scaleY = scaleX > scaleY ? scaleX : scaleY;
                        break;
                    case Stretch.Fill:
                        break;
                }
            }

            switch (stretchDirection) {
                case StretchDirection.UpOnly:
                    if (scaleX < 1.0) scaleX = 1.0;
                    if (scaleY < 1.0) scaleY = 1.0;
                    break;
                case StretchDirection.DownOnly:
                    if (scaleX > 1.0) scaleX = 1.0;
                    if (scaleY > 1.0) scaleY = 1.0;
                    break;
                case StretchDirection.Both:
                    break;
            }

            return new Size(scaleX, scaleY);
        }

        private Size MeasureArrangeHelper(Size inputSize) {
            if (_current.BitmapSource == null) {
                return new Size(double.IsNaN(Width) ? 0d : Width, double.IsNaN(Height) ? 0d : Height);
            }

            var scaleFactor = ComputeScaleFactor(inputSize, new Size(_current.Width, _current.Height), Stretch, StretchDirection);
            return new Size(_current.Width * scaleFactor.Width, _current.Height * scaleFactor.Height);
        }
        #endregion

        #region Initialization
        static BetterImage() {
            var style = CreateDefaultStyles();
            StyleProperty.OverrideMetadata(typeof(BetterImage), new FrameworkPropertyMetadata(style));
            StretchProperty.OverrideMetadata(typeof(BetterImage),
                    new FrameworkPropertyMetadata(Stretch.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure));
            StretchDirectionProperty.OverrideMetadata(typeof(BetterImage),
                    new FrameworkPropertyMetadata(StretchDirection.Both, FrameworkPropertyMetadataOptions.AffectsMeasure));
            ClipToBoundsProperty.OverrideMetadata(typeof(BetterImage),
                    new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
            BackgroundProperty.OverrideMetadata(typeof(BetterImage),
                    new FrameworkPropertyMetadata(new SolidColorBrush(Colors.Transparent), FrameworkPropertyMetadataOptions.AffectsRender));
        }

        private static Style CreateDefaultStyles() {
            var style = new Style(typeof(BetterImage), null);
            style.Setters.Add(new Setter(FlowDirectionProperty, FlowDirection.LeftToRight));
            style.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Seal();
            return style;
        }
        #endregion
    }
}
