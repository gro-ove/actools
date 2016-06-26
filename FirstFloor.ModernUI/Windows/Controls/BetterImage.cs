using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BetterImage : Control {
        #region Wrapper with size (for solving DPI-related problems)
        public struct BitmapEntry {
            public readonly ImageSource BitmapSource;
            public readonly int Width, Height;

            public BitmapEntry(ImageSource source, int width, int height) {
                BitmapSource = source;
                Width = width;
                Height = height;
            }
        }
        #endregion

        #region Properties
        public static readonly DependencyProperty ShowBrokenProperty = DependencyProperty.Register(nameof(ShowBroken), typeof(bool),
                typeof(BetterImage), new PropertyMetadata(true));

        public bool ShowBroken {
            get { return (bool)GetValue(ShowBrokenProperty); }
            set { SetValue(ShowBrokenProperty, value); }
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
            ((BetterImage)o).OnFilenameChanged();
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object),
                typeof(BetterImage), new PropertyMetadata(OnSourceChanged));

        public object Source {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BetterImage)o).OnSourceChanged(e.NewValue);
        }

        private void OnSourceChanged(object newValue) {
            if (newValue is BitmapEntry) {
                _loading = false;
                _current = (BitmapEntry)newValue;
                InvalidateVisual();
            } else {
                var source = newValue as ImageSource;
                if (newValue == null || source != null) {
                    _loading = false;
                    _current = source == null ? new BitmapEntry() : new BitmapEntry(source, (int)source.Width, (int)source.Height);
                    InvalidateVisual();
                } else {
                    Filename = newValue as string;
                }
            }
        }
        #endregion

        #region Loading
        public static BitmapEntry LoadBitmapSource(string filename, int decodeWidth = -1, int decodeHeight = -1) {
            if (string.IsNullOrEmpty(filename)) {
                return new BitmapEntry();
            }

            Uri uri;
            try {
                uri = new Uri(filename);
            } catch (Exception) {
                return new BitmapEntry();
            }

            try {
                var bi = new BitmapImage();
                bi.BeginInit();

                var width = 0;
                var height = 0;

                if (decodeWidth > 0) {
                    bi.DecodePixelWidth = (int)(decodeWidth * DpiAwareWindow.OptionScale);
                    width = decodeWidth;
                }

                if (decodeHeight > 0) {
                    bi.DecodePixelHeight = (int)(decodeHeight * DpiAwareWindow.OptionScale);
                    height = decodeHeight;
                }

                bi.CreateOptions = uri.Scheme == "http" || uri.Scheme == "https"
                        ? BitmapCreateOptions.None : BitmapCreateOptions.IgnoreImageCache;
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

                return new BitmapEntry(bi, width, height);
            } catch (Exception e) {
                Logging.Warning("[BetterImage] Loading failed: " + e);
                return new BitmapEntry();
            }
        }

        [ItemNotNull]
        private static async Task<byte[]> ReadAllBytesAsync(string filename, CancellationToken cancellation = default(CancellationToken)) {
            if (filename.StartsWith("/")) {
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

        public static async Task<BitmapEntry> LoadBitmapSourceAsync(string filename, int decodeWidth = -1, int decodeHeight = -1) {
            if (string.IsNullOrEmpty(filename)) {
                return new BitmapEntry();
            }

            byte[] data;
            try {
                // TODO: cancellation?
                data = await ReadAllBytesAsync(filename);
            } catch (Exception) {
                return new BitmapEntry();
            }

            try {
                using (var stream = new WrappingStream(new MemoryStream(data))) {
                    int width = 0, height = 0;

                    var bi = new BitmapImage();
                    bi.BeginInit();

                    if (decodeWidth > 0) {
                        bi.DecodePixelWidth = (int)(decodeWidth * DpiAwareWindow.OptionScale);
                        width = decodeWidth;
                    }

                    if (decodeHeight > 0) {
                        bi.DecodePixelHeight = (int)(decodeHeight * DpiAwareWindow.OptionScale);
                        height = decodeHeight;
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

                    return new BitmapEntry(bi, width, height);
                }
            } catch (Exception e) {
                Logging.Warning("[BetterImage] Loading failed: " + e);
                return new BitmapEntry();
            }
        }
        #endregion

        #region Static methods
        /// <summary>
        /// Forces all BetterImages which showing specific filename to reload their content.
        /// </summary>
        /// <param name="filename"></param>
        public static void ReloadImage(string filename) {
            foreach (var image in Application.Current.Windows.OfType<Window>()
                                             .SelectMany(VisualTreeHelperEx.FindVisualChildren<BetterImage>)
                                             .Where(x => string.Equals(x.Filename, filename, StringComparison.OrdinalIgnoreCase))) {
                image.OnFilenameChanged();
            }
        }
        #endregion

        #region Inner control methods
        public int InnerDecodeWidth {
            get {
                if (DecodeWidth >= 0) return DecodeWidth;
                if (!double.IsPositiveInfinity(MaxWidth)) return (int)MaxWidth;
                if (!double.IsNaN(Width)) return (int)Width;
                return -1;
            }
        }

        public int InnerDecodeHeight => DecodeHeight;

        private BitmapEntry _current;
        private bool _loading;

        private void ReloadImage() {
            if (DelayedCreation) {
                ReloadImageAsync();
            } else if (!_loading) {
                _current = LoadBitmapSource(Filename, InnerDecodeWidth, InnerDecodeHeight);
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        private async void ReloadImageAsync() {
            if (_loading) return;
            _loading = true;

            try {
                var current = await LoadBitmapSourceAsync(Filename, InnerDecodeWidth, InnerDecodeHeight);
                if (!_loading) return;

                _current = current;
                InvalidateMeasure();
                InvalidateVisual();
            } finally {
                _loading = false;
            }
        }

        private void OnFilenameChanged() {
            ReloadImage();
            if (DelayedCreation && ClearOnChange) {
                _current = new BitmapEntry();
                InvalidateVisual();
            }
        }

        private Size _size;
        private Point _offset;

        protected override Size MeasureOverride(Size constraint) {
            return MeasureArrangeHelper(constraint);
        }

        private double HorizontalContentAlignmentMultipler => HorizontalContentAlignment == HorizontalAlignment.Center ? 0.5d :
                HorizontalContentAlignment == HorizontalAlignment.Right ? 1d : 0d;

        private double VerticalContentAlignmentMultipler => VerticalContentAlignment == VerticalAlignment.Center ? 0.5d :
                VerticalContentAlignment == VerticalAlignment.Bottom ? 1d : 0d;

        protected override Size ArrangeOverride(Size arrangeSize) {
            _size = MeasureArrangeHelper(arrangeSize);
            if (Filename?.Contains("preview.jpg") == true) {
                Logging.Write(Filename + ": " + arrangeSize + "; " + _size.Width + "×" + _size.Height + $"; {_current.Width}×{_current.Height}");
            }
            _offset = new Point((arrangeSize.Width - _size.Width) * HorizontalContentAlignmentMultipler,
                    (arrangeSize.Height - _size.Height) * VerticalContentAlignmentMultipler);
            return new Size(double.IsPositiveInfinity(arrangeSize.Width) ? _size.Width : arrangeSize.Width,
                    double.IsPositiveInfinity(arrangeSize.Height) ? _size.Height : arrangeSize.Height);
        }

        protected override void OnRender(DrawingContext dc) {
            dc.DrawRectangle(Background, null, new Rect(new Point(), RenderSize));
            if (_current.BitmapSource != null) {
                dc.DrawImage(_current.BitmapSource, new Rect(_offset, _size));
            } else if (ShowBroken && !_loading) {
                dc.DrawImage(BrokenIcon.ImageSource,
                        new Rect(new Point((RenderSize.Width - BrokenIcon.Width) / 2d, (RenderSize.Height - BrokenIcon.Height) / 2d), BrokenIcon.Size));
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
