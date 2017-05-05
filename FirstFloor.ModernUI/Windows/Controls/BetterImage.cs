/* ZIP-support: option to load images from ZIP-archives. With it enabled, BetterImage
 * would look for a special separator in file’s path, and if found, load image from
 * a ZIP-archive instead. Experimental feature, don’t know if it’s a good idea or not. */
// #define ZIP_SUPPORT

/* Warn about EndInit() multithread-related crashes and calculate success rate. */
// #define DEBUG_ENDINIT_CRASHES

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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

#if ZIP_SUPPORT
using System.IO.Compression;
#endif

namespace FirstFloor.ModernUI.Windows.Controls {
    public enum ImageCropMode {
        Absolute, Relative
    }

    public interface ICancellable {
        void Cancel();
    }

    public interface IThreadPool {
        [CanBeNull]
        ICancellable Run([NotNull] Action action);
    }

    public partial class BetterImage : Control {
        /// <summary>
        /// Set true to read files in UI thread without using async IO API.
        /// </summary>
        public static bool OptionReadFileSync = false;

        /// <summary>
        /// Set true to decode all images in the UI thread.
        /// </summary>
        public static bool OptionDecodeImageSync = false;

        /// <summary>
        /// If image’s size is smaller than this, it will be decoded in the UI thread.
        /// Doesn’t do anything if OptionDecodeImageSync is true.
        /// </summary>
        public static int OptionDecodeImageSyncThreshold = 50 * 1000; // 50 KB

        /// <summary>
        /// Considering that 30 KB image takes ≈1.8 ms to be decoded and value of
        /// OptionDecodeImageSyncThreshold, how many of these images are allowed to be
        /// decoded in UI thread per second before switching to non-UI one?
        /// </summary>
        public static int OptionMaxSyncDecodedInSecond = 8;

        /// <summary>
        /// Set true to immediately move decoded image to UI thread from non-UI one.
        /// Doesn’t do anything if OptionDecodeImageSync is true.
        /// </summary>

        public static bool OptionDisplayImmediate = false;

        /// <summary>
        /// Before loading cached entity, check if according file exists and was not updated.
        /// </summary>
        public static bool OptionEnsureCacheIsFresh = true;

        /// <summary>
        /// Do not set it to zero if OptionReadFileSync is true and OptionDecodeImageSync is true!
        /// </summary>
        public static TimeSpan OptionAdditionalDelay = TimeSpan.FromMilliseconds(30);

        /// <summary>
        /// Total number of cached images (needed so cache won’t get expanded to like thousands of
        /// very small images).
        /// </summary>
        public static int OptionCacheTotalEntries = 500;

        /// <summary>
        /// Summary cache size, in bytes.
        /// </summary>
        public static long OptionCacheTotalSize = 100 * 1000 * 1000; // 100 MB

        /// <summary>
        /// Cache threshold per image (bigger images won’t be cached), in bytes.
        /// </summary>
        public static long OptionCacheMaxSize = 1000 * 1000; // 1 MB

        /// <summary>
        /// Cache threshold per image (smaller images won’t be cached), in bytes.
        /// </summary>
        public static long OptionCacheMinSize = 500; // 500 B

        /// <summary>
        /// Mark cached images for debugging and log related information.
        /// </summary>
        public static bool OptionMarkCached = false;

        /// <summary>
        /// How to compare cached images’ paths.
        /// </summary>
        public static StringComparison OptionCacheStringComparison = StringComparison.Ordinal;

        /// <summary>
        /// What User-Agent to use while loading remote images.
        /// </summary>
        public static string RemoteUserAgent;

        /// <summary>
        /// Where to store cached images.
        /// </summary>
        public static string RemoteCacheDirectory;

        #region Wrapper with size (for solving DPI-related problems)
        public struct BitmapEntry {
            // In most cases, it’s BitmapSource.
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

            public BitmapEntry(BitmapSource source) {
                BitmapSource = source;
                Width = source.PixelWidth;
                Height = source.PixelHeight;
                Downsized = true;
                Date = DateTime.Now;
            }

            public bool IsBroken => BitmapSource == null;
        }
        #endregion

        #region Properties
        public static readonly DependencyProperty ShowBrokenProperty = DependencyProperty.Register(nameof(ShowBroken), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(true, (o, e) => {
                    ((BetterImage)o)._showBroken = (bool)e.NewValue;
                }));

        private bool _showBroken = true;

        public bool ShowBroken {
            get { return _showBroken; }
            set { SetValue(ShowBrokenProperty, value); }
        }

        public static readonly DependencyProperty HideBrokenProperty = DependencyProperty.Register(nameof(HideBroken), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(false, (o, e) => {
                    ((BetterImage)o)._hideBroken = (bool)e.NewValue;
                }));

        private bool _hideBroken;

        public bool HideBroken {
            get { return _hideBroken; }
            set { SetValue(HideBrokenProperty, value); }
        }

        public static readonly DependencyProperty ClearOnChangeProperty = DependencyProperty.Register(nameof(ClearOnChange), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(false, (o, e) => {
                    ((BetterImage)o)._clearOnChange = (bool)e.NewValue;
                }));

        private bool _clearOnChange;

        public bool ClearOnChange {
            get { return _clearOnChange; }
            set { SetValue(ClearOnChangeProperty, value); }
        }

        public static readonly DependencyProperty DelayedCreationProperty = DependencyProperty.Register(nameof(DelayedCreation), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(true, (o, e) => {
                    ((BetterImage)o)._delayedCreation = (bool)e.NewValue;
                }));

        private bool _delayedCreation = true;

        public bool DelayedCreation {
            get { return _delayedCreation; }
            set { SetValue(DelayedCreationProperty, value); }
        }

        public static readonly DependencyProperty DecodeHeightProperty = DependencyProperty.Register(nameof(DecodeHeight), typeof(int),
                typeof(BetterImage), new PropertyMetadata(-1, (o, e) => {
                    ((BetterImage)o)._decodeHeight = (int)e.NewValue;
                }));

        private int _decodeHeight = -1;

        public int DecodeHeight {
            get { return _decodeHeight; }
            set { SetValue(DecodeHeightProperty, value); }
        }

        /// <summary>
        /// Applied before cropping! Also, it will affect image size, so most likely you wouldn’t want to set it
        /// with CropUnits=Absolute.
        /// 
        /// Without cropping, if set to -1 (default value), MaxWidth or Width will be used instead. Set to 0 
        /// if you want to disable this behavior and force full-size decoding. 
        /// </summary>
        public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.Register(nameof(DecodeWidth), typeof(int),
                typeof(BetterImage), new PropertyMetadata(-1, (o, e) => {
                    ((BetterImage)o)._decodeWidth = (int)e.NewValue;
                }));

        private int _decodeWidth = -1;

        public int DecodeWidth {
            get { return _decodeWidth; }
            set { SetValue(DecodeWidthProperty, value); }
        }

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(nameof(Stretch), typeof(Stretch),
                typeof(BetterImage), new FrameworkPropertyMetadata(Stretch.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((BetterImage)o)._stretch = (Stretch)e.NewValue;
                }));

        private Stretch _stretch = Stretch.Uniform;

        public Stretch Stretch {
            get { return _stretch; }
            set { SetValue(StretchProperty, value); }
        }

        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(nameof(StretchDirection), typeof(StretchDirection),
                typeof(BetterImage), new FrameworkPropertyMetadata(StretchDirection.Both, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((BetterImage)o)._stretchDirection = (StretchDirection)e.NewValue;
                }));

        private StretchDirection _stretchDirection = StretchDirection.Both;

        public StretchDirection StretchDirection {
            get { return _stretchDirection; }
            set { SetValue(StretchDirectionProperty, value); }
        }

        public static readonly DependencyProperty FilenameProperty = DependencyProperty.Register(nameof(Filename), typeof(string),
                typeof(BetterImage), new PropertyMetadata(OnFilenameChanged));

        private string _filename;

        public string Filename {
            get { return _filename; }
            set { SetValue(FilenameProperty, value); }
        }

        private static void OnFilenameChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            var b = (BetterImage)o;
            b._filename = (string)e.NewValue;
            b.OnFilenameChanged(b._filename);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object),
                typeof(BetterImage), new PropertyMetadata(OnSourceChanged));

        public object Source {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        private static bool _skipNext;

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            if (_skipNext) return;
            try {
                _skipNext = true;
                var b = (BetterImage)o;
                var potentialFilename = e.NewValue as string;
                if (potentialFilename != null) {
                    b.ImageSource = null;
                    b.Filename = potentialFilename;
                } else if (e.NewValue is BitmapEntry) {
                    b.ImageSource = null;
                    b.Filename = null;
                    b.SetBitmapEntryDirectly((BitmapEntry)e.NewValue);
                } else {
                    var source = (ImageSource)e.NewValue;
                    b.ImageSource = source;
                    b.Filename = null;
                    b.OnSourceChanged(source);
                }
            } finally {
                _skipNext = false;
            }
        }

        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource),
                typeof(BetterImage), new PropertyMetadata(OnImageSourceChanged));

        public ImageSource ImageSource {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        private static void OnImageSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            if (_skipNext) return;
            ((BetterImage)o).Source = e.NewValue;
        }

        public static readonly DependencyProperty CropProperty = DependencyProperty.Register(nameof(Crop), typeof(Rect?),
                typeof(BetterImage), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    var b = (BetterImage)o;
                    b._crop = (Rect?)e.NewValue;

                    if (b.Filename != null) {
                        b.OnFilenameChanged(b.Filename);
                    }
                }));

        private Rect? _crop;

        public Rect? Crop {
            get { return _crop; }
            set { SetValue(CropProperty, value); }
        }

        public static readonly DependencyProperty CropUnitsProperty = DependencyProperty.Register(nameof(CropUnits), typeof(ImageCropMode),
                typeof(BetterImage), new FrameworkPropertyMetadata(ImageCropMode.Relative, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((BetterImage)o)._cropUnits = (ImageCropMode)e.NewValue;
                }));

        private ImageCropMode _cropUnits = ImageCropMode.Relative;

        public ImageCropMode CropUnits {
            get { return _cropUnits; }
            set { SetValue(CropUnitsProperty, value); }
        }

        private void SetBitmapEntryDirectly(BitmapEntry value) {
            Filename = null;
            SetCurrent(value);
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

        public static readonly DependencyProperty CollapseIfMissingProperty = DependencyProperty.Register(nameof(CollapseIfMissing), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(OnCollapseIfMissingChanged));

        private bool _collapseIfMissing;

        public bool CollapseIfMissing {
            get { return _collapseIfMissing; }
            set { SetValue(CollapseIfMissingProperty, value); }
        }

        private static void OnCollapseIfMissingChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterImage)o)._collapseIfMissing = (bool)e.NewValue;
        }
        #endregion

        #region Loading
        [CanBeNull]
        private static string GetCachedFilename(Uri uri) {
            if (RemoteCacheDirectory == null) return null;

            try {
                if (!Directory.Exists(RemoteCacheDirectory)) {
                    Directory.CreateDirectory(RemoteCacheDirectory);
                }
            } catch (Exception e) {
                Logging.Error(e.Message);
                return null;
            }

            var fileNameBuilder = new StringBuilder();
            using (var sha1 = new SHA1Managed()) {
                var canonicalUrl = uri.ToString();
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(canonicalUrl));
                fileNameBuilder.Append(BitConverter.ToString(hash).Replace(@"-", "").ToLower());
                if (Path.HasExtension(canonicalUrl)) {
                    fileNameBuilder.Append(Path.GetExtension(canonicalUrl));
                }
            }

            var fileName = fileNameBuilder.ToString();
            return Path.Combine(RemoteCacheDirectory, fileName);
        }

        private static BitmapEntry LoadRemoteBitmap(Uri uri, int decodeWidth = -1, int decodeHeight = -1) {
            var httpRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpRequest.Method = "GET";

            if (RemoteUserAgent != null) {
                httpRequest.UserAgent = RemoteUserAgent;
            }

            var cache = GetCachedFilename(uri);
            var cacheFile = cache == null ? null : new FileInfo(cache);
            if (cacheFile?.Exists == true) {
                httpRequest.IfModifiedSince = cacheFile.LastWriteTime;
            }

            try {
                using (var memory = new MemoryStream())
                using (var response = (HttpWebResponse)httpRequest.GetResponse()) {
                    using (var stream = response.GetResponseStream()) {
                        if (stream == null) {
                            return cacheFile == null ? BitmapEntry.Empty :
                                    LoadBitmapSourceFromBytes(File.ReadAllBytes(cache), decodeWidth, decodeHeight);
                        }

                        stream.CopyTo(memory);
                    }

                    var bytes = memory.ToArray();
                    if (cache != null) {
                        File.WriteAllBytes(cache, bytes);
                        cacheFile.LastWriteTime = response.LastModified;
                    }

                    return LoadBitmapSourceFromBytes(bytes, decodeWidth, decodeHeight);
                }
            } catch (WebException e) {
                Logging.Error(e.Message);
            }

            return cacheFile == null ? BitmapEntry.Empty :
                    LoadBitmapSourceFromBytes(File.ReadAllBytes(cache), decodeWidth, decodeHeight);
        }

        public static BitmapEntry LoadBitmapSource(string filename, int decodeWidth = -1, int decodeHeight = -1) {
            if (string.IsNullOrEmpty(filename)) {
                return BitmapEntry.Empty;
            }

#if ZIP_SUPPORT
            string zipFilename, entryName;
            if (TryToParseZipPath(filename, out zipFilename, out entryName)) {
                using (var zip = ZipFile.OpenRead(zipFilename)) {
                    var entry = zip.Entries.First(x => x.Name == entryName);
                    using (var s = entry.Open())
                    using (var m = new MemoryStream((int)entry.Length)) {
                        s.CopyTo(m);
                        return LoadBitmapSourceFromBytes(m.ToArray(), decodeWidth, decodeHeight);
                    }
                }
            }
#endif

            Uri uri;
            try {
                uri = new Uri(filename);
            } catch (Exception) {
                return BitmapEntry.Empty;
            }

            var remote = uri.Scheme == "http" || uri.Scheme == "https";
            if (remote) {
                return LoadRemoteBitmap(uri);
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

                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = uri;
                bi.EndInit();
                bi.Freeze();

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

        /// <summary>
        /// Safe (handles all exceptions inside).
        /// </summary>
        [CanBeNull]
        private static byte[] ReadBytes([CanBeNull] string filename) {
            if (filename == null) return null;

            try {
#if ZIP_SUPPORT
                // Loading from ZIP file
                string zipFilename, entryName;
                if (TryToParseZipPath(filename, out zipFilename, out entryName)) {
                    using (var zip = ZipFile.OpenRead(zipFilename)) {
                        var entry = zip.Entries.First(x => x.Name == entryName);
                        using (var s = entry.Open())
                        using (var m = new MemoryStream((int)entry.Length)) {
                            s.CopyTo(m);
                            return m.ToArray();
                        }
                    }
                }
#endif

                // Loading from application resources (I think, there is an issue here)
                if (filename.StartsWith(@"/")) {
                    var stream = Application.GetResourceStream(new Uri(filename, UriKind.Relative))?.Stream;
                    Logging.Debug($"{filename}: {stream}");
                    if (stream != null) {
                        using (stream) {
                            var result = new byte[stream.Length];
                            stream.Read(result, 0, (int)stream.Length);
                            return result;
                        }
                    }
                }

                // Regular loading
                return File.ReadAllBytes(filename);
            } catch (FileNotFoundException) {
                return null;
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        /// <summary>
        /// Safe (handles all exceptions inside).
        /// </summary>
        [ItemCanBeNull]
        private static async Task<byte[]> ReadBytesAsync([CanBeNull] string filename, CancellationToken cancellation = default(CancellationToken)) {
            if (filename == null) return null;

            try {
#if ZIP_SUPPORT
                // Loading from ZIP file
                string zipFilename, entryName;
                if (TryToParseZipPath(filename, out zipFilename, out entryName)) {
                    using (var zip = ZipFile.OpenRead(zipFilename)) {
                        var entry = zip.Entries.First(x => x.Name == entryName);
                        using (var s = entry.Open())
                        using (var m = new MemoryStream((int)entry.Length)) {
                            await s.CopyToAsync(m);
                            return m.ToArray();
                        }
                    }
                }
#endif

                // Loading from application resources (I think, there is an issue here)
                if (filename.StartsWith(@"/")) {
                    var stream = Application.GetResourceStream(new Uri(filename, UriKind.Relative))?.Stream;
                    Logging.Debug($"{filename}: {stream}");
                    if (stream != null) {
                        using (stream) {
                            var result = new byte[stream.Length];
                            await stream.ReadAsync(result, 0, (int)stream.Length, cancellation);
                            return result;
                        }
                    }
                }

                // Regular loading
                using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    var result = new byte[stream.Length];
                    await stream.ReadAsync(result, 0, result.Length, cancellation);
                    return result;
                }
            } catch (FileNotFoundException) {
                return null;
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

#if DEBUG_ENDINIT_CRASHES
        private static int _successes, _fails;
#endif

        /// <summary>
        /// Safe (handles all exceptions inside).
        /// </summary>
        private static BitmapEntry LoadBitmapSourceFromBytes([NotNull] byte[] data, int decodeWidth = -1, int decodeHeight = -1, int attempt = 0) {
            try {
                // WrappingSteam here helps to avoid memory leaks. For more information:
                // https://code.logos.com/blog/2008/04/memory_leak_with_bitmapimage_and_memorystream.html
                using (var memory = new MemoryStream(data))
                using (var stream = new WrappingStream(memory)) {
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

                    bi.CreateOptions = BitmapCreateOptions.None;
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

#if DEBUG_ENDINIT_CRASHES
                    if (attempt == 0) {
                        ++_successes;
                    }
#endif

                    return new BitmapEntry(bi, width, height, downsized);
                }
            } catch (FileFormatException e) when (attempt < 2 && e.InnerException is COMException) {
                // Apparently, EndInit() might crash with FileFormatException when several images are
                // being loaded in several threads simultaneously. Of course, I could wrap it with lock()
                // or reduce number of threads in pool to only one since EndInit() is actually the most
                // time-consuming line here, but it will noticeably damage the performance — most of
                // the time (to be more specific, 99.1% of calls), it works just fine. So, instead of
                // making it slow, let’s just try it again and then again (but no more than that, image
                // could actually be damaged).
                ++attempt;

#if DEBUG_ENDINIT_CRASHES
                ++_fails;
                Logging.Warning($"Recover attempt: {attempt} (s. rate: {100d * _successes / (_successes + _fails):F1}%)");
#else
                if (attempt > 1) {
                    Logging.Warning($"Recover attempt: {attempt}");
                }
#endif

                return LoadBitmapSourceFromBytes(data, decodeWidth, decodeHeight, attempt);
            } catch (Exception e) {
                Logging.Warning(e);
                return BitmapEntry.Empty;
            }
        }

        public static async Task<BitmapEntry> LoadBitmapSourceAsync(string filename, int decodeWidth = -1, int decodeHeight = -1) {
            var bytes = await ReadBytesAsync(filename);
            return bytes == null ? BitmapEntry.Empty : LoadBitmapSourceFromBytes(bytes, decodeWidth, decodeHeight);
        }
        #endregion

        #region Static methods
        /// <summary>
        /// Forces all BetterImages which showing specific filename to reload their content.
        /// </summary>
        /// <param name="filename"></param>
        public static void ReloadImage([CanBeNull] string filename) {
            if (string.IsNullOrWhiteSpace(filename)) return;

#if ZIP_SUPPORT
            filename = GetActualFilename(filename);
            RemoveFromCache(filename);
            var app = Application.Current;
            if (app == null) return;
            foreach (var image in app.Windows.OfType<Window>()
                    .SelectMany(VisualTreeHelperEx.FindVisualChildren<BetterImage>)
                    .Where(x => string.Equals(GetActualFilename(x._filename), filename, StringComparison.OrdinalIgnoreCase))) {
                image.OnFilenameChanged(filename);
            }
#else
            RemoveFromCache(filename);
            var app = Application.Current;
            if (app == null) return;
            foreach (var image in app.Windows.OfType<Window>()
                    .SelectMany(VisualTreeHelperEx.FindVisualChildren<BetterImage>)
                    .Where(x => string.Equals(x.Filename, filename, StringComparison.OrdinalIgnoreCase))) {
                image.OnFilenameChanged(filename);
            }
#endif
        }
        #endregion

        #region Cache
        private class CacheEntry {
            public string Key;
            public BitmapEntry Value;
        }

        private static List<CacheEntry> _cache;

        private static List<CacheEntry> Cache => _cache ?? (_cache = new List<CacheEntry>(OptionCacheTotalEntries));

        private static void RemoveFromCache([NotNull] string filename) {
            var cache = Cache;
            lock (cache) {
                for (var i = cache.Count - 1; i >= 0; i--) {
                    if (string.Equals(
#if ZIP_SUPPORT
                            GetActualFilename(cache[i].Key)
#else
                            cache[i].Key,
#endif
                            filename, OptionCacheStringComparison)) {
                        cache.RemoveAt(i);
                    }
                }
            }
        }

        private void AddToCache(string filename, BitmapEntry entry) {
            _fromCache = false;
            if (OptionCacheTotalEntries <= 0 || OptionCacheTotalSize <= 0 ||
                    entry.Size < OptionCacheMinSize || entry.Size > OptionCacheMaxSize) return;

            var cache = Cache;
            var lastIndex = cache.Count - 1;

            lock (cache) {
                var index = -1;
                for (var i = lastIndex; i >= 0; i--) {
                    if (string.Equals(cache[i].Key, filename, OptionCacheStringComparison)) {
                        index = i;
                        break;
                    }
                }
                
                CacheEntry item;
                if (index == -1) {
                    // item does not exist now, let’s add it
                    item = new CacheEntry {
                        Key = filename,
                        Value = entry
                    };

                    // if queue is full, remove the first entry to avoid re-creating
                    // array thing inside of the list
                    if (cache.Count == OptionCacheTotalEntries) {
                        if (OptionMarkCached) {
                            Logging.Debug($"total cached: {cache.Count} entries (capacity: {cache.Capacity})");
                        }

                        cache.RemoveAt(0);
                    }

                    cache.Add(item);
                } else {
                    // item already exists, let’s update its value
                    item = cache[index];
                    item.Value = entry;

                    if (index < lastIndex) {
                        // and move it to the end of removal queue if needed
                        cache.RemoveAt(index);
                        cache.Add(item);
                    }
                }

                // remove old entries
                var total = item.Value.Size;
                for (var i = lastIndex - 1; i >= 0; i--) {
                    total += cache[i].Value.Size;
                    if (total > OptionCacheTotalSize) {
                        if (OptionMarkCached) {
                            Logging.Debug($"total cached size: {total / 1024d / 1024:F2} MB, let’s remove first {i + 1} image{(i > 0 ? "s" : "")}");
                        }

                        cache.RemoveRange(0, i + 1);
                        return;
                    }
                }

                if (OptionMarkCached) {
                    Logging.Debug($"total cached size: {total / 1024d / 1024:F2} MB, no need to remove anything");
                }
            }
        }

        private static BitmapEntry GetCached(string filename) {
            var cache = Cache;
            var lastIndex = cache.Count - 1;

            lock (cache) {
                for (var i = lastIndex; i >= 0; i--) {
                    var item = cache[i];
                    if (string.Equals(item.Key, filename, OptionCacheStringComparison)) {
                        if (i < lastIndex) {
                            cache.RemoveAt(i);
                            cache.Add(item);
                        }

                        return item.Value;
                    }
                }
            }

            return BitmapEntry.Empty;
        }
        #endregion

        #region Inner control methods
        public int InnerDecodeWidth {
            get {
                var decodeWidth = DecodeWidth;
                if (decodeWidth >= 0) return decodeWidth;

                if (Crop.HasValue) return -1;

                var maxWidth = MaxWidth;
                if (!double.IsPositiveInfinity(maxWidth)) return (int)maxWidth;

                var width = Width;
                if (!double.IsNaN(width)) return (int)width;
                return -1;
            }
        }

        public int InnerDecodeHeight => DecodeHeight;

        /// <summary>
        /// Reloads image.
        /// </summary>
        /// <returns>Returns true if image will be loaded later, and false if image is ready.</returns>
        private bool ReloadImage() {
            if (_currentTask != null) {
                Logging.Here();
                _currentTask.Cancel();
                _currentTask = null;
            }

            if (Filename == null) {
                SetCurrent(BitmapEntry.Empty);
                return false;
            }

            if (OptionCacheTotalSize > 0 && Filename.Length > 0 && Filename[0] != '/') {
                var cached = GetCached(Filename);
                var innerDecodeWidth = InnerDecodeWidth;
                if (cached.BitmapSource != null && (innerDecodeWidth == -1 ? !cached.Downsized : cached.Width >= innerDecodeWidth)) {
                    try {
                        if (OptionEnsureCacheIsFresh) {
                            var actual = GetActualFilename(Filename);
                            if (actual == null) {
                                RemoveFromCache(Filename);
                            } else {
                                var info = new FileInfo(actual);
                                if (!info.Exists) {
                                    SetCurrent(BitmapEntry.Empty, true);
                                    return false;
                                }

                                if (info.LastWriteTime > cached.Date) {
                                    RemoveFromCache(Filename);
                                } else {
                                    SetCurrent(cached, true);
                                    return false;
                                }
                            }
                        } else {
                            SetCurrent(cached, true);
                            return false;
                        }
                    } catch (Exception) {
                        // If there will be any problems with FileInfo
                        SetCurrent(BitmapEntry.Empty, true);
                        return false;
                    }
                }
            }

            if (DelayedCreation) {
                ReloadImageAsync();
                return true;
            }

            SetCurrent(LoadBitmapSource(Filename, InnerDecodeWidth, InnerDecodeHeight));
            return false;
        }

        private BitmapEntry _current;
        private int _loading;
        private bool _broken;
        private bool _fromCache;

        private void SetCurrent(BitmapEntry value, bool fromCache = false) {
            ++_loading;

            _current = value;
            _broken = value.IsBroken;
            _fromCache = fromCache;

            if (fromCache) {
                if (_broken && Filename != null) {
                    RemoveFromCache(Filename);
                }
            } else if (!_broken) {
                AddToCache(Filename, _current);
            }

            InvalidateMeasure();
            InvalidateVisual();
        }

        private void ClearCurrent() {
            _current = BitmapEntry.Empty;
            _broken = false;
            InvalidateVisual();
        }

#if ZIP_SUPPORT
        public static readonly string ZipSeparator = @"/.//";

        private static bool TryToParseZipPath([CanBeNull] string filename, out string zipFilename, out string entryName) {
            if (filename == null) {
                zipFilename = entryName = null;
                return false;
            }

            var index = filename.IndexOf(ZipSeparator, StringComparison.Ordinal);
            if (index == -1) {
                zipFilename = entryName = null;
                return false;
            }

            zipFilename = filename.Substring(0, index);
            entryName = filename.Substring(index + ZipSeparator.Length);
            return zipFilename.Length > 0 && entryName.Length > 0;
        }
#endif

        [ContractAnnotation(@"filename:null => null; filename:notnull => notnull")]
        private static string GetActualFilename([CanBeNull] string filename) {
#if ZIP_SUPPORT
            string zipFilename, entryName;
            return TryToParseZipPath(filename, out zipFilename, out entryName) ? zipFilename : filename;
#else
            return filename;
#endif
        }

        private static readonly List<DateTime> RecentlyLoaded = new List<DateTime>(50);

        private static bool UpdateLoaded() {
            var now = DateTime.Now;

            for (var i = RecentlyLoaded.Count - 1; i >= 0; i--) {
                if ((now - RecentlyLoaded[i]).TotalSeconds > 1d) {
                    RecentlyLoaded.RemoveAt(i);
                }
            }

            if (RecentlyLoaded.Count < OptionMaxSyncDecodedInSecond) {
                RecentlyLoaded.Add(now);
                return true;
            }

            return false;
        }

        private async void ReloadImageAsync() {
            var loading = ++_loading;
            _broken = false;

            var filename = Filename;

            if (OptionAdditionalDelay > TimeSpan.Zero) {
                await Task.Delay(OptionAdditionalDelay);
                if (loading != _loading || Filename != filename) return;
            }

            byte[] data;
            if (OptionReadFileSync) {
                data = ReadBytes(filename);
            } else {
                data = await ReadBytesAsync(filename);
                if (loading != _loading || Filename != filename) return;
            }

            if (data == null) {
                SetCurrent(BitmapEntry.Empty);
                return;
            }

            var decodeWidth = InnerDecodeWidth;
            var decodeHeight = InnerDecodeHeight;
            
            if (data.Length < OptionDecodeImageSyncThreshold && UpdateLoaded()) {
                SetCurrent(LoadBitmapSourceFromBytes(data, decodeWidth, decodeHeight));
                return;
            }

            if (OptionDecodeImageSync) {
                SetCurrent(LoadBitmapSourceFromBytes(data, decodeWidth, decodeHeight));
            } else {
                _currentTask = ThreadPool.Run(() => {
                    var current = LoadBitmapSourceFromBytes(data, decodeWidth, decodeHeight);

                    var result = (Action)(() => {
                        _currentTask = null;

                        if (loading != _loading || Filename != filename) return;
                        SetCurrent(current);
                    });

                    if (OptionDisplayImmediate) {
                        Dispatcher.Invoke(result);
                    } else {
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, result);
                    }
                });
            }
        }

        private ICancellable _currentTask;

        private void OnFilenameChanged(string value) {
            if (CollapseIfMissing) {
                if (File.Exists(GetActualFilename(value))) {
                    Visibility = Visibility.Visible;
                } else {
                    Visibility = Visibility.Collapsed;
                    SetCurrent(BitmapEntry.Empty);
                    return;
                }
            }

            if (ReloadImage() && ClearOnChange) {
                ClearCurrent();
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

        private static FormattedText _cachedFormattedText;

        private static FormattedText CachedFormattedText => _cachedFormattedText ??
                (_cachedFormattedText = new FormattedText(@"Cached", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface(new FontFamily(@"Arial").ToString()), 12, Brushes.Red));

        protected override void OnRender(DrawingContext dc) {
            if (_current.BitmapSource == null && HideBroken) return;

            dc.DrawRectangle(Background, null, new Rect(new Point(), RenderSize));

            var broken = false;
            if (_current.BitmapSource == null) {
                broken = true;
            } else if (Crop.HasValue) {
                try {
                    var crop = Crop.Value;
                    var cropped = new CroppedBitmap((BitmapSource)_current.BitmapSource, CropUnits == ImageCropMode.Relative ? new Int32Rect(
                            (int)(crop.Left * _current.Width), (int)(crop.Top * _current.Height),
                            (int)(crop.Width * _current.Width), (int)(crop.Height * _current.Height)) : new Int32Rect(
                                    (int)crop.Left, (int)crop.Top, (int)crop.Width, (int)crop.Height));
                    dc.DrawImage(cropped, new Rect(_offset, _size));
                } catch (ArgumentException) {
                    broken = true;
                }
            } else {
                dc.DrawImage(_current.BitmapSource, new Rect(_offset, _size));
            }

            if (broken && ShowBroken && _broken) {
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

            Size size;
            if (Crop.HasValue) {
                var crop = Crop.Value;
                size = CropUnits == ImageCropMode.Relative ?
                        new Size(crop.Width * _current.Width, crop.Height * _current.Height) :
                        new Size(crop.Width, crop.Height);
            } else {
                size = new Size(_current.Width, _current.Height);
            }

            var scaleFactor = ComputeScaleFactor(inputSize, size, Stretch, StretchDirection);
            return new Size(size.Width * scaleFactor.Width, size.Height * scaleFactor.Height);
        }
        #endregion

        #region Initialization
        static BetterImage() {
            var style = CreateDefaultStyles();
            StyleProperty.OverrideMetadata(typeof(BetterImage), new FrameworkPropertyMetadata(style));
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

