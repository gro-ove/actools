/* Warn about EndInit() multithread-related crashes and calculate success rate. */

// #define DEBUG_ENDINIT_CRASHES

/* WebP support (500 KB library is required; converting of 1920×1080 image takes about 100 ms) */

// #define WEBP_SUPPORT

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
#if WEBP_SUPPORT
using System.Drawing.Imaging;
using Noesis.Drawing.Imaging.WebP;
#endif
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace FirstFloor.ModernUI.Windows.Controls {
    public enum ImageCropMode {
        Absolute,
        Relative
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
        /// If image’s size is smaller than this, it will be decoded in the UI thread.
        /// Doesn’t do anything if OptionDecodeImageSync is true.
        /// </summary>
        public static int OptionDecodeImageSyncThreshold = 50 * 1000; // 50 KB

        // public static int OptionDecodeImageSyncThreshold = 50; // 50 KB

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
        // public static bool OptionEnsureCacheIsFresh = true;
        public static bool OptionEnsureCacheIsFresh = false;

        /// <summary>
        /// Do not set it to zero if OptionReadFileSync is true and OptionDecodeImageSync is true!
        /// </summary>
        public static TimeSpan OptionAdditionalDelay = TimeSpan.FromMilliseconds(30);

        /// <summary>
        /// Total number of cached images (needed so cache won’t get expanded to like thousands of
        /// very small images).
        /// </summary>
        public static int OptionCacheTotalEntries = 2000;

        /// <summary>
        /// Summary cache size, in bytes.
        /// </summary>
        public static long OptionCacheTotalSize = 100 * 1024 * 1024; // 100 MB

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

        public static void CleanUpCache() {
            var cache = Cache;
            lock (cache) {
                cache.Clear();
            }
        }

        #region Wrapper with size (for solving DPI-related problems)
        public struct Image {
            // In most cases, it’s BitmapSource.
            public readonly ImageSource ImageSource;
            public readonly int Width, Height;
            public readonly bool Downsized;
            public readonly DateTime Date;

            // In bytes
            public int Size => Width * Height * 3;

            public static Image Empty => new Image();

            public Image(ImageSource source, int width, int height, bool downsized) {
                ImageSource = source;
                Width = width;
                Height = height;
                Downsized = downsized;
                Date = DateTime.Now;
            }

            public Image(ImageSource source) {
                ImageSource = source;
                Width = (int)source.Width;
                Height = (int)source.Height;
                Downsized = true;
                Date = DateTime.Now;
            }

            public Image(BitmapSource source) {
                ImageSource = source;
                Width = source.PixelWidth;
                Height = source.PixelHeight;
                Downsized = true;
                Date = DateTime.Now;
            }

            public bool IsBroken => ImageSource == null;
        }
        #endregion

        #region Properties
        public static readonly DependencyProperty ShowBrokenProperty = DependencyProperty.Register(nameof(ShowBroken), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(true, (o, e) => { ((BetterImage)o)._showBroken = (bool)e.NewValue; }));

        private bool _showBroken = true;

        public bool ShowBroken {
            get => _showBroken;
            set => SetValue(ShowBrokenProperty, value);
        }

        public static readonly DependencyProperty HideBrokenProperty = DependencyProperty.Register(nameof(HideBroken), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(false, (o, e) => { ((BetterImage)o)._hideBroken = (bool)e.NewValue; }));

        private bool _hideBroken;

        public bool HideBroken {
            get => _hideBroken;
            set => SetValue(HideBrokenProperty, value);
        }

        public static readonly DependencyProperty ClearOnChangeProperty = DependencyProperty.Register(nameof(ClearOnChange), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(false, (o, e) => { ((BetterImage)o)._clearOnChange = (bool)e.NewValue; }));

        private bool _clearOnChange;

        public bool ClearOnChange {
            get => _clearOnChange;
            set => SetValue(ClearOnChangeProperty, value);
        }

        public static readonly DependencyProperty DelayedCreationProperty = DependencyProperty.Register(nameof(DelayedCreation), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(true, (o, e) => { ((BetterImage)o)._delayedCreation = (bool)e.NewValue; }));

        private bool _delayedCreation = true;

        public bool DelayedCreation {
            get => _delayedCreation;
            set => SetValue(DelayedCreationProperty, value);
        }

        public static readonly DependencyProperty DecodeHeightProperty = DependencyProperty.Register(nameof(DecodeHeight), typeof(int),
                typeof(BetterImage), new PropertyMetadata(-1, (o, e) => { ((BetterImage)o)._decodeHeight = (int)e.NewValue; }));

        private int _decodeHeight = -1;

        public int DecodeHeight {
            get => _decodeHeight;
            set => SetValue(DecodeHeightProperty, value);
        }

        /// <summary>
        /// Applied before cropping! Also, it will affect image size, so most likely you wouldn’t want to set it
        /// with CropUnits=Absolute.
        ///
        /// Without cropping, if set to -1 (default value), MaxWidth or Width will be used instead. Set to 0
        /// if you want to disable this behavior and force full-size decoding.
        /// </summary>
        public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.Register(nameof(DecodeWidth), typeof(int),
                typeof(BetterImage), new PropertyMetadata(-1, (o, e) => { ((BetterImage)o)._decodeWidth = (int)e.NewValue; }));

        private int _decodeWidth = -1;

        public int DecodeWidth {
            get => _decodeWidth;
            set => SetValue(DecodeWidthProperty, value);
        }

        public static readonly DependencyProperty AsyncDecodeProperty = DependencyProperty.Register(nameof(AsyncDecode), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(true,
                        (o, e) => ((BetterImage)o)._asyncDecode = (bool)e.NewValue));

        private bool _asyncDecode = true;

        public bool AsyncDecode {
            get => _asyncDecode;
            set => SetValue(AsyncDecodeProperty, value);
        }

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(nameof(Stretch), typeof(Stretch),
                typeof(BetterImage),
                new FrameworkPropertyMetadata(Stretch.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure,
                        (o, e) => { ((BetterImage)o)._stretch = (Stretch)e.NewValue; }));

        private Stretch _stretch = Stretch.Uniform;

        public Stretch Stretch {
            get => _stretch;
            set => SetValue(StretchProperty, value);
        }

        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(nameof(StretchDirection), typeof(StretchDirection),
                typeof(BetterImage),
                new FrameworkPropertyMetadata(StretchDirection.Both, FrameworkPropertyMetadataOptions.AffectsMeasure,
                        (o, e) => { ((BetterImage)o)._stretchDirection = (StretchDirection)e.NewValue; }));

        private StretchDirection _stretchDirection = StretchDirection.Both;

        public StretchDirection StretchDirection {
            get => _stretchDirection;
            set => SetValue(StretchDirectionProperty, value);
        }

        public static readonly DependencyProperty FilenameProperty = DependencyProperty.Register(nameof(Filename), typeof(string),
                typeof(BetterImage), new PropertyMetadata(OnFilenameChanged));

        private string _filename;

        [CanBeNull]
        public string Filename {
            get => _filename;
            set => SetValue(FilenameProperty, value);
        }

        private static void OnFilenameChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            var b = (BetterImage)o;
            b._filename = (string)e.NewValue;
            b.OnFilenameChanged(b._filename);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object),
                typeof(BetterImage), new PropertyMetadata(OnSourceChanged));

        public object Source {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private static bool _skipNext;

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            if (_skipNext) return;
            try {
                _skipNext = true;
                var b = (BetterImage)o;
                if (e.NewValue is string potentialFilename) {
                    b.ImageSource = null;
                    b.Filename = potentialFilename;
                } else if (e.NewValue is Image be) {
                    b.ImageSource = null;
                    b.Filename = null;
                    b.SetBitmapEntryDirectly(be);
                } else if (e.NewValue is byte[] by) {
                    b.ImageSource = null;
                    b.Filename = null;
                    b.SetBitmapEntryDirectly(LoadBitmapSourceFromBytes(by, b.InnerDecodeWidth, sourceDebug: "bytes array"));
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
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
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
            get => _crop;
            set => SetValue(CropProperty, value);
        }

        public static readonly DependencyProperty CropUnitsProperty = DependencyProperty.Register(nameof(CropUnits), typeof(ImageCropMode),
                typeof(BetterImage),
                new FrameworkPropertyMetadata(ImageCropMode.Relative, FrameworkPropertyMetadataOptions.AffectsMeasure,
                        (o, e) => { ((BetterImage)o)._cropUnits = (ImageCropMode)e.NewValue; }));

        private ImageCropMode _cropUnits = ImageCropMode.Relative;

        public ImageCropMode CropUnits {
            get => _cropUnits;
            set => SetValue(CropUnitsProperty, value);
        }

        public static readonly DependencyProperty CropTransparentAreasProperty = DependencyProperty.Register(nameof(CropTransparentAreas), typeof(bool),
                typeof(BetterImage),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure,
                        (o, e) => ((BetterImage)o)._cropTransparentAreas = (bool)e.NewValue));

        private bool _cropTransparentAreas;

        /// <summary>
        /// If enabled, Crop and CropUnits will be overwritten!
        /// </summary>
        public bool CropTransparentAreas {
            get => _cropTransparentAreas;
            set => SetValue(CropTransparentAreasProperty, value);
        }

        private void SetBitmapEntryDirectly(Image value) {
            Filename = null;
            SetCurrent(value);
        }

        private void OnSourceChanged(ImageSource newValue) {
            SetBitmapEntryDirectly(newValue == null ? Image.Empty : new Image(newValue, (int)newValue.Width, (int)newValue.Height, false));
        }

        public static readonly DependencyProperty ForceFillProperty = DependencyProperty.Register(nameof(ForceFill), typeof(bool),
                typeof(BetterImage));

        public bool ForceFill {
            get => GetValue(ForceFillProperty) as bool? == true;
            set => SetValue(ForceFillProperty, value);
        }

        public static readonly DependencyProperty CollapseIfMissingProperty = DependencyProperty.Register(nameof(CollapseIfMissing), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(OnCollapseIfMissingChanged));

        private bool _collapseIfMissing;

        public bool CollapseIfMissing {
            get => _collapseIfMissing;
            set => SetValue(CollapseIfMissingProperty, value);
        }

        private static void OnCollapseIfMissingChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterImage)o)._collapseIfMissing = (bool)e.NewValue;
        }

        public static readonly DependencyProperty CollapseIfNullProperty = DependencyProperty.Register(nameof(CollapseIfNull), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(OnCollapseIfNullChanged));

        private bool _collapseIfNull;

        public bool CollapseIfNull {
            get => _collapseIfNull;
            set => SetValue(CollapseIfNullProperty, value);
        }

        private static void OnCollapseIfNullChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            var b = (BetterImage)o;
            b._collapseIfNull = (bool)e.NewValue;
            if (b._collapseIfNull && b._filename == null) {
                b.Visibility = Visibility.Collapsed;
            }
        }

        private bool _hideIfNull;

        public static readonly DependencyProperty HideIfNullProperty = DependencyProperty.Register(nameof(HideIfNull), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(OnHideIfNullChanged));

        public bool HideIfNull {
            get => _hideIfNull;
            set => SetValue(HideIfNullProperty, value);
        }

        private static void OnHideIfNullChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            var b = (BetterImage)o;
            b._hideIfNull = (bool)e.NewValue;
            if (b._hideIfNull && b._filename == null) {
                b.Visibility = Visibility.Hidden;
            }
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
            using (var sha1 = SHA1.Create()) {
                var canonicalUrl = uri.ToString();
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(canonicalUrl));
                fileNameBuilder.Append(BitConverter.ToString(hash).Replace(@"-", "").ToLower());

                try {
                    if (Path.HasExtension(canonicalUrl)) {
                        fileNameBuilder.Append(Regex.Match(Path.GetExtension(canonicalUrl), @"^\.[-_a-zA-Z0-9]+").Value);
                    }
                } catch (ArgumentException) { }
            }

            var fileName = fileNameBuilder.ToString();
            return Path.Combine(RemoteCacheDirectory, fileName);
        }

        private static async Task<Image> LoadRemoteBitmapAsync(Uri uri, int decodeWidth = -1, int decodeHeight = -1) {
            var httpRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpRequest.Method = "GET";

            if (RemoteUserAgent != null) {
                httpRequest.UserAgent = RemoteUserAgent;
            }

            var cache = GetCachedFilename(uri);
            var cacheFile = cache == null ? null : new FileInfo(cache);
            if (cacheFile?.Exists == true) {
                var lastWriteTime = cacheFile.LastWriteTime;
                if ((DateTime.Now - lastWriteTime).TotalDays < 1d) {
                    // Because barely any servers respect If-Modified-Since header
                    return LoadBitmapSourceFromBytes(File.ReadAllBytes(cache), decodeWidth, decodeHeight, sourceDebug: uri.OriginalString);
                }

                httpRequest.IfModifiedSince = lastWriteTime;
            }

            try {
                using (var memory = new MemoryStream())
                using (var response = (HttpWebResponse)await httpRequest.GetResponseAsync()) {
                    using (var stream = response.GetResponseStream()) {
                        if (stream == null) {
                            return cacheFile == null ? Image.Empty
                                    : LoadBitmapSourceFromBytes(File.ReadAllBytes(cache), decodeWidth, decodeHeight, sourceDebug: uri.OriginalString);
                        }

                        await stream.CopyToAsync(memory);
                    }

                    var bytes = memory.ToArray();
                    if (cache != null) {
                        await Task.Run(() => {
                            try {
                                File.WriteAllBytes(cache, bytes);
                                cacheFile.LastWriteTime = response.LastModified;
                            } catch (IOException) { }
                        });
                    }

                    return LoadBitmapSourceFromBytes(bytes, decodeWidth, decodeHeight, sourceDebug: uri.OriginalString);
                }
            } catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode != HttpStatusCode.NotModified) {
                Logging.Error(e.Message);
            } catch (WebException) { }

            if (cacheFile == null) return Image.Empty;
            cacheFile.LastWriteTime = DateTime.Now; // this way, checking with web request will be skipped for the next day
            var cacheBytes = await ReadBytesAsync(cache);
            return cacheBytes == null ? Image.Empty : LoadBitmapSourceFromBytes(cacheBytes, decodeWidth, decodeHeight, sourceDebug: uri.OriginalString);
        }

        public static Image LoadBitmapSource(string filename, int decodeWidth = -1, int decodeHeight = -1) {
            if (string.IsNullOrEmpty(filename)) {
                return Image.Empty;
            }

            return LoadBitmapSourceAsync(filename, decodeWidth, decodeHeight).Result;
        }

        /// <summary>
        /// Safe (handles all exceptions inside).
        /// </summary>
        [CanBeNull]
        private static byte[] ReadBytes([CanBeNull] string filename) {
            if (filename == null) return null;

            try {
                // Loading from application resources (I think, there is an issue here)
                if (filename.StartsWith(@"/")) {
                    var stream = Application.GetResourceStream(new Uri(filename, UriKind.Relative))?.Stream;
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
            } catch (DirectoryNotFoundException) {
                return null;
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        private static byte[] ConvertSpecial(byte[] input) {
#if WEBP_SUPPORT
            if (IsWebPImage(input)) {
                return ConvertWebPImage(input);
            }
#endif

            return input;
        }

        private static Task<byte[]> ConvertSpecialAsync(byte[] input) {
#if WEBP_SUPPORT
            if (IsWebPImage(input)) {
                return Task.Run(() => ConvertWebPImage(input));
            }
#endif

            return Task.FromResult(input);
        }

#if WEBP_SUPPORT
        private static byte[] ConvertWebPImage(byte[] input) {
            using (var stream = new MemoryStream())
            using (var image = WebPFormat.Load(input)) {
                image.Save(stream, ImageFormat.Bmp);
                return stream.ToArray();
            }
        }

        private static bool IsWebPImage(byte[] input) {
            return input.Length > 15 && input[0] == 0x52 && input[1] == 0x49 && input[2] == 0x46 && input[3] == 0x46 &&
                    input[4] == 0xA6 && input[5] == 0x5B && input[6] == 0x07 && input[7] == 0x00 && input[8] == 0x57 &&
                    input[9] == 0x45 && input[10] == 0x42 && input[11] == 0x50 && input[12] == 0x56 && input[13] == 0x50 &&
                    input[14] == 0x38;
        }
#endif

        /// <summary>
        /// Safe (handles all exceptions inside).
        /// </summary>
        [ItemCanBeNull]
        private static async Task<byte[]> ReadBytesAsync([CanBeNull] string filename, CancellationToken cancellation = default, BetterImage origin = null) {
            if (filename == null) return null;

            try {
                // Loading from application resources (I think, there is an issue here)
                if (filename.StartsWith(@"/")) {
                    var stream = Application.GetResourceStream(new Uri(filename, UriKind.Relative))?.Stream;
                    if (stream != null) {
                        using (stream) {
                            var result = new byte[stream.Length];
                            await stream.ReadAsync(result, 0, (int)stream.Length, cancellation).ConfigureAwait(false);
                            return await ConvertSpecialAsync(result).ConfigureAwait(false);
                        }
                    }
                }

                // Regular loading
                var tcs = new TaskCompletionSource<byte[]>();

                // We need it this way: file opening is done in thread pool, but subsequent reading uses async I/O API
                // ReSharper disable once AsyncVoidLambda
                ThreadPool.Run(async () => {
                    if (origin != null && origin.Filename != filename) {
                        tcs.SetResult(null);
                        return;
                    }
                    try {
                        using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true)) {
                            if (stream.Length > 40 * 1024 * 1024) {
                                throw new Exception($"Image “{filename}” is way too big to display: {stream.Length.ToReadableSize()}");
                            }
                            var result = new byte[stream.Length];
                            await stream.ReadAsync(result, 0, result.Length, cancellation).ConfigureAwait(false);
                            tcs.SetResult(await ConvertSpecialAsync(result).ConfigureAwait(false));
                        }
                    } catch (Exception e) {
                        tcs.SetException(e);
                    }
                });
                return await tcs.Task.ConfigureAwait(false);
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
        public static Image LoadBitmapSourceFromBytes([NotNull] byte[] data, int decodeWidth = -1, int decodeHeight = -1, int attempt = 0,
                [Localizable(false)] string sourceDebug = null) {
            PrepareDecodeLimits(data, ref decodeWidth, ref decodeHeight, sourceDebug);

            try {
                data = ConvertSpecial(data);

                // WrappingSteam here helps to avoid memory leaks. For more information:
                // https://code.logos.com/blog/2008/04/memory_leak_with_bitmapimage_and_memorystream.html
                using (var memory = new MemoryStream(data))
                using (var stream = new WrappingStream(memory)) {
                    int width = 0, height = 0;
                    var downsized = false;

                    var bi = new BitmapImage();
                    bi.BeginInit();

                    if (decodeWidth > 0) {
                        bi.DecodePixelWidth = decodeWidth;
                        width = bi.DecodePixelWidth;
                        downsized = true;
                    }

                    if (decodeHeight > 0) {
                        bi.DecodePixelHeight = decodeHeight;
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

                    return new Image(bi, width, height, downsized);
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

                return LoadBitmapSourceFromBytes(data, decodeWidth, decodeHeight, attempt, sourceDebug);
            } catch (NotSupportedException e) when (e.ToString().Contains(@"0x88982F50")) {
                Logging.Warning(e.Message);
                return Image.Empty;
            } catch (Exception e) {
                Logging.Warning(e);
                return Image.Empty;
            }
        }

        private static void PrepareDecodeLimits(byte[] data, ref int decodeWidth, ref int decodeHeight,
                [Localizable(false)] string sourceDebug) {
            if (decodeWidth <= 0 && decodeHeight <= 0) return;

            decodeWidth = (int)(decodeWidth * Math.Max(DpiInformation.MaxScaleX, 1d));
            decodeHeight = (int)(decodeHeight * Math.Max(DpiInformation.MaxScaleY, 1d));

            var size = GetImageSize(data, sourceDebug) ?? new System.Drawing.Size(1, 1);
            if (decodeHeight > 0 && decodeWidth > 0) {
                var minimum = Math.Min((double)decodeWidth / size.Width, (double)decodeHeight / size.Height);
                decodeWidth = minimum >= 1d ? 0 : (int)(size.Width * minimum);
                decodeHeight = minimum >= 1d ? 0 : (int)(size.Height * minimum);
            } else if (decodeWidth >= size.Width || decodeHeight >= size.Height) {
                decodeWidth = decodeHeight = 0;
            }
        }

        [NotNull]
        public static async Task<Image> LoadBitmapSourceAsync([CanBeNull] string filename, int decodeWidth = -1, int decodeHeight = -1) {
            if (filename.IsWebUrl()) {
                Uri uri;
                try {
                    uri = new Uri(filename);
                } catch (Exception) {
                    return Image.Empty;
                }

                return await LoadRemoteBitmapAsync(uri).ConfigureAwait(false);
            }

            var bytes = await ConvertSpecialAsync(await ReadBytesAsync(filename).ConfigureAwait(false)).ConfigureAwait(false);
            return bytes == null ? Image.Empty : LoadBitmapSourceFromBytes(bytes, decodeWidth, decodeHeight, sourceDebug: filename);
        }
        #endregion

        #region Static methods
        /// <summary>
        /// Forces all BetterImages which showing specific filename to reload their content.
        /// </summary>
        /// <param name="filename"></param>
        public static void Refresh([CanBeNull] string filename) {
            if (string.IsNullOrWhiteSpace(filename)) return;

            RemoveFromCache(filename);
            foreach (var image in VisualTreeHelperEx.GetAllOfType<BetterImage>()
                    .Where(x => string.Equals(x.Filename, filename, StringComparison.OrdinalIgnoreCase))) {
                image.OnFilenameChanged(filename);
            }
        }
        #endregion

        #region Cache
        private class CacheEntry {
            public string Key;
            public Image Value;
        }

        private static List<CacheEntry> _cache;

        private static List<CacheEntry> Cache => _cache ?? (_cache = new List<CacheEntry>(OptionCacheTotalEntries));

        private static void RemoveFromCache([NotNull] string filename) {
            var cache = Cache;
            lock (cache) {
                for (var i = cache.Count - 1; i >= 0; i--) {
                    if (string.Equals(cache[i].Key, filename, OptionCacheStringComparison)) {
                        cache.RemoveAt(i);
                    }
                }
            }
        }

        private void AddToCache(string filename, Image entry) {
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
                            Logging.Debug($"Total cached: {cache.Count} entries (capacity: {cache.Capacity})");
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
                long total = item.Value.Size;
                for (var i = lastIndex - 1; i >= 0; i--) {
                    total += cache[i].Value.Size;
                    if (total > OptionCacheTotalSize) {
                        if (OptionMarkCached) {
                            Logging.Debug($"Total cached size: {total.ToReadableSize()}, let’s remove first {i + 1} image{(i > 0 ? "s" : "")}");
                        }

                        cache.RemoveRange(0, i + 1);
                        return;
                    }
                }

                if (OptionMarkCached) {
                    Logging.Debug($"Total cached size: {total.ToReadableSize()}, no need to remove anything");
                }
            }
        }

        private static Image GetCached(string filename) {
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

            return Image.Empty;
        }
        #endregion

        #region Inner control methods
        protected int InnerDecodeWidth {
            get {
                var decodeWidth = DecodeWidth;
                if (decodeWidth >= 0) return decodeWidth;

                if (Crop.HasValue || CropTransparentAreas) return -1;

                var maxWidth = MaxWidth;
                if (!double.IsPositiveInfinity(maxWidth)) return (int)maxWidth;

                var width = Width;
                if (!double.IsNaN(width)) return (int)width;
                return -1;
            }
        }

        public int InnerDecodeHeight => DecodeHeight;

        [Pure]
        private bool IsRemoteImage() {
            return Filename != null && (Filename.StartsWith(@"http://") || Filename.StartsWith(@"https://"));
        }

        /// <summary>
        /// Reloads image.
        /// </summary>
        /// <returns>Returns true if image will be loaded later, and false if image is ready.</returns>
        private bool ReloadImage() {
            try {
                if (_currentTask != null) {
                    _currentTask.Cancel();
                    _currentTask = null;
                }

                if (Filename == null) {
                    SetCurrent(Image.Empty);
                    return false;
                }

                if (IsRemoteImage()) {
                    LoadRemoteBitmapAsync(new Uri(Filename, UriKind.RelativeOrAbsolute), InnerDecodeWidth, InnerDecodeHeight).ContinueWith(
                            v => ActionExtension.InvokeInMainThread(() => SetCurrent(v.IsCompleted ? v.Result : Image.Empty)));
                    return true;
                }

                if (OptionCacheTotalSize > 0 && Filename.Length > 0 && Filename[0] != '/') {
                    var cached = GetCached(Filename);
                    var innerDecodeWidth = InnerDecodeWidth;
                    if (cached.ImageSource != null && (innerDecodeWidth == -1 ? !cached.Downsized : cached.Width >= innerDecodeWidth)) {
                        try {
                            if (OptionEnsureCacheIsFresh) {
                                var actual = GetActualFilename(Filename);
                                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                // ReSharper disable HeuristicUnreachableCode
                                if (actual == null) {
                                    RemoveFromCache(Filename);
                                } else {
                                    var info = new FileInfo(actual);
                                    if (!info.Exists) {
                                        SetCurrent(Image.Empty, true);
                                        return false;
                                    }

                                    if (info.LastWriteTime > cached.Date) {
                                        RemoveFromCache(Filename);
                                    } else {
                                        SetCurrent(cached, true);
                                        return false;
                                    }
                                }
                                // ReSharper restore HeuristicUnreachableCode
                            } else {
                                SetCurrent(cached, true);
                                return false;
                            }
                        } catch (Exception) {
                            // If there will be any problems with FileInfo
                            SetCurrent(Image.Empty, true);
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
            } catch (Exception e) {
                Logging.Error($"Failed to load “{Filename}”: {e}");
                SetCurrent(Image.Empty, true);
                return false;
            }
        }

        private Image _current;
        private int _loading;
        private bool _broken;
        private bool _fromCache;

        protected void SetCurrent(Image value, bool? fromCache = false) {
            ++_loading;

            _current = value;
            _broken = value.IsBroken;
            _fromCache = fromCache == true;

            if (fromCache.HasValue) {
                if (fromCache.Value) {
                    if (_broken && Filename != null) {
                        RemoveFromCache(Filename);
                    }
                } else if (!_broken) {
                    AddToCache(Filename, _current);
                }
            }

            InvalidateMeasure();
            InvalidateVisual();
        }

        protected void ClearCurrent() {
            _current = Image.Empty;
            _broken = false;
            InvalidateVisual();
        }

        [ContractAnnotation(@"filename:null => null; filename:notnull => notnull")]
        private static string GetActualFilename([CanBeNull] string filename) {
            return filename;
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

        private void ApplyReloadImage(string filename, int loading, byte[] data) {
            if (loading != _loading || Filename != filename) return;

            if (data == null) {
                SetCurrent(Image.Empty);
                return;
            }

            var decodeWidth = InnerDecodeWidth;
            var decodeHeight = InnerDecodeHeight;

            if (data.Length < OptionDecodeImageSyncThreshold && UpdateLoaded()) {
                SetCurrent(LoadBitmapSourceFromBytes(data, decodeWidth, decodeHeight, sourceDebug: filename));
                return;
            }

            if (AsyncDecode) {
                _currentTask = ThreadPool.Run(() => {
                    var current = LoadBitmapSourceFromBytes(data, decodeWidth, decodeHeight, sourceDebug: filename);

                    var result = (Action)(() => {
                        _currentTask = null;

                        if (loading != _loading || Filename != filename) return;
                        SetCurrent(current);
                    });

                    if (OptionDisplayImmediate) {
                        Dispatcher?.Invoke(result);
                    } else {
                        Dispatcher?.BeginInvoke(DispatcherPriority.Background, result);
                    }
                });
            } else {
                SetCurrent(LoadBitmapSourceFromBytes(data, decodeWidth, decodeHeight, sourceDebug: filename));
            }
        }

        private async void ReloadImageAsync() {
            var loading = ++_loading;
            _broken = false;

            var filename = Filename;

            if (OptionAdditionalDelay > TimeSpan.Zero) {
                await Task.Delay(OptionAdditionalDelay);
                if (loading != _loading || Filename != filename) return;
            }

            if (OptionReadFileSync) {
                ApplyReloadImage(filename, loading, ReadBytes(filename));
            } else {
                ApplyReloadImage(filename, loading, await ReadBytesAsync(filename, default, this));
                /*ThreadPool.Run(() => {
                    ApplyReloadImage(filename, loading, ReadBytes(filename));
                });*/
            }
        }

        private ICancellable _currentTask;
        private bool _isLoadedHandlerSet;

        protected void OnFilenameChanged(string value) {
            if (!IsLoaded) {
                if (!_isLoadedHandlerSet) {
                    Loaded += OnLoaded;
                    _isLoadedHandlerSet = true;
                }
                return;
            }

            if (CollapseIfNull) {
                if (value != null) {
                    Visibility = Visibility.Visible;
                } else {
                    Visibility = Visibility.Collapsed;
                    SetCurrent(Image.Empty);
                    return;
                }
            }

            if (_hideIfNull) {
                if (value != null) {
                    Visibility = Visibility.Visible;
                } else {
                    Visibility = Visibility.Hidden;
                    SetCurrent(Image.Empty);
                    return;
                }
            }

            if (CollapseIfMissing && !IsRemoteImage()) {
                if (File.Exists(GetActualFilename(value))) {
                    Visibility = Visibility.Visible;
                } else {
                    Visibility = Visibility.Collapsed;
                    SetCurrent(Image.Empty);
                    return;
                }
            }

            if (ReloadImage() && ClearOnChange) {
                ClearCurrent();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            Loaded -= OnLoaded;
            _isLoadedHandlerSet = false;
            OnFilenameChanged(_filename);
        }

        private Size _size;
        private Point _offset;

        protected override Size MeasureOverride(Size constraint) {
            if (_current.ImageSource == null && HideBroken) return new Size();

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
            if (_current.ImageSource == null && HideBroken) return new Size();

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

        private WeakReference<ImageSource> _lastCropMaskRef;
        private Rect? _lastCropMaskRect;

        private Rect? FindCropMask(ImageSource img) {
            if (_lastCropMaskRef != null && _lastCropMaskRef.TryGetTarget(out var v) && ReferenceEquals(v, img)) {
                return _lastCropMaskRect;
            }

            var b = (BitmapSource)img;
            FindCropArea(b, out var x0, out var y0, out var x1, out var y1);

            _lastCropMaskRef = new WeakReference<ImageSource>(img);
            _lastCropMaskRect = x1 == 0 ? (Rect?)null : new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            return _lastCropMaskRect;
        }

        private bool CropAsync(ImageSource img) {
            if (_lastCropMaskRef != null && _lastCropMaskRef.TryGetTarget(out var v) && ReferenceEquals(v, img)) {
                return true;
            }

            ThreadPool.Run(() => {
                FindCropMask(img);
                ActionExtension.InvokeInMainThreadAsync(() => {
                    InvalidateMeasure();
                    InvalidateVisual();
                });
            });
            return false;
        }

        public static Rect? FindTransparentCropMask(BitmapSource b) {
            FindTransparentCropArea("static", b, out var x0, out var y0, out var x1, out var y1);
            return x1 == 0 ? (Rect?)null : new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        private static ThreadLocal<byte[]> _transparentCropData = new ThreadLocal<byte[]>(() => new byte[800 * 450]);

        private static unsafe bool FindTransparentCropArea(string tag, BitmapSource b, out int left, out int top, out int right, out int bottom) {
            if (b.Format.Masks.Count != 4) {
                left = top = right = bottom = 0;
                return true;
            }

            int w = b.PixelWidth, h = b.PixelHeight, s = (w * b.Format.BitsPerPixel + 7) / 8;
            if (w > 800 || h > 450) {
                Logging.Warning($"Image is too large to crop: {w}×{h}, tag: {tag}");
                left = top = right = bottom = 0;
                return true;
            }

            var data = _transparentCropData.Value;
            b.CopyPixels(data, s, 0);

            var k = b.Format.Masks.ElementAtOrDefault(0).Mask;
            if (k == null) {
                left = top = right = bottom = 0;
                return true;
            }

            var m = BitConverter.ToUInt32(k.Reverse().ToArray(), 0);

            left = w;
            top = h;
            bottom = right = 0;
            fixed (byte* p = data) {
                var u = (uint*)p;
                var o = 0;
                for (var y = 0; y < h; y++) {
                    for (var x = 0; x < w; x++) {
                        if ((u[o++] & m) != 0) {
                            if (x < left) left = x;
                            if (y < top) top = y;
                            if (x > right) right = x;
                            if (y > bottom) bottom = y;
                        }
                    }
                }
            }
            return false;
        }

        protected virtual bool FindCropArea(BitmapSource b, out int left, out int top, out int right, out int bottom) {
            return FindTransparentCropArea(Filename, b, out left, out top, out right, out bottom);
        }

        protected override void OnRender(DrawingContext dc) {
            var bi = _current.ImageSource;
            if (bi == null && HideBroken) return;

            dc.DrawRectangle(Background, null, new Rect(new Point(), RenderSize));

            var broken = false;
            if (bi == null) {
                broken = true;
            } else {
                var cropTransparent = CropTransparentAreas;
                if (cropTransparent && !CropAsync(bi)) return;

                var cropNullable = cropTransparent ? _lastCropMaskRect : Crop;
                if (cropNullable.HasValue) {
                    try {
                        var crop = cropNullable.Value;
                        var cropped = new CroppedBitmap((BitmapSource)bi, cropTransparent || CropUnits != ImageCropMode.Relative ?
                                new Int32Rect((int)crop.Left, (int)crop.Top, (int)crop.Width, (int)crop.Height) :
                                new Int32Rect(
                                        (int)(crop.Left * _current.Width), (int)(crop.Top * _current.Height),
                                        (int)(crop.Width * _current.Width), (int)(crop.Height * _current.Height)));
                        dc.DrawImage(cropped, new Rect(_offset, _size));
                    } catch (ArgumentException) {
                        broken = true;
                    }
                } else {
                    dc.DrawImage(bi, new Rect(_offset, _size));
                }
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
            var bi = _current.ImageSource;
            if (bi == null) {
                return new Size(double.IsNaN(Width) ? 0d : Width, double.IsNaN(Height) ? 0d : Height);
            }

            var cropTransparent = CropTransparentAreas;
            if (cropTransparent && !CropAsync(bi)) {
                return new Size(double.IsNaN(Width) ? 0d : Width, double.IsNaN(Height) ? 0d : Height);
            }

            Size size;
            var cropNullable = cropTransparent ? _lastCropMaskRect : Crop;
            if (cropNullable.HasValue) {
                var crop = cropNullable.Value;
                size = cropTransparent || CropUnits != ImageCropMode.Relative ?
                        new Size(crop.Width, crop.Height) :
                        new Size(crop.Width * _current.Width, crop.Height * _current.Height);
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