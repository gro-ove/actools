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

        public static readonly DependencyProperty FillSpaceProperty = DependencyProperty.Register(nameof(FillSpace), typeof(bool),
                typeof(BetterImage));

        public bool FillSpace {
            get { return (bool)GetValue(FillSpaceProperty); }
            set { SetValue(FillSpaceProperty, value); }
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
                _loaded = true;
                _current = (BitmapEntry)newValue;
                InvalidateVisual();
            } else {
                var source = newValue as ImageSource;
                if (newValue == null || source != null) {
                    _loading = false;
                    _loaded = true;
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
            } catch (Exception) {
                return new BitmapEntry();
            }
        }

        [ItemNotNull]
        private static async Task<byte[]> ReadAllBytesAsync(string filename, CancellationToken cancellation = default(CancellationToken)) {
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

            try {
                var data = await ReadAllBytesAsync(filename);
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
            } catch (Exception) {
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
        private bool _loading, _loaded;

        private void ReloadImage() {
            if (DelayedCreation) {
                ReloadImageAsync();
            } else if (!_loading) {
                _current = LoadBitmapSource(Filename, InnerDecodeWidth, InnerDecodeHeight);
                _loaded = true;
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
                _loaded = true;
                InvalidateVisual();
            } finally {
                _loading = false;
            }
        }

        private void OnFilenameChanged() {
            _loaded = false;
            if (ClearOnChange) {
                _current = new BitmapEntry();
            }
            InvalidateVisual();
        }

        private Size _size;
        private Point _offset;
        private static readonly SolidColorBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);

        protected override Size MeasureOverride(Size constraint) {
            if (FillSpace) {
                _size = MeasureArrangeHelper(constraint);
                return new Size(double.IsPositiveInfinity(constraint.Width) ? _size.Width : constraint.Width,
                        double.IsPositiveInfinity(constraint.Height) ? _size.Height : constraint.Height);
            }

            return MeasureArrangeHelper(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            if (FillSpace) {
                _size = MeasureArrangeHelper(arrangeSize);
                _offset = new Point((arrangeSize.Width - _size.Width) / 2d, (arrangeSize.Height - _size.Height) / 2d);
                return new Size(double.IsPositiveInfinity(arrangeSize.Width) ? _size.Width : arrangeSize.Width,
                        double.IsPositiveInfinity(arrangeSize.Height) ? _size.Height : arrangeSize.Height);
            }

            return MeasureArrangeHelper(arrangeSize);
        }

        protected override void OnRender(DrawingContext dc) {
            if (!_loaded) ReloadImage();

            if (FillSpace) {
                dc.DrawRectangle(TransparentBrush, null, new Rect(new Point(), RenderSize));
                if (_current.BitmapSource != null) {
                    dc.DrawImage(_current.BitmapSource, new Rect(_offset, _size));
                } else if (ShowBroken && _loaded) {
                    dc.DrawImage(BrokenIcon.ImageSource,
                            new Rect(new Point((RenderSize.Width - BrokenIcon.Width) / 2d, (RenderSize.Height - BrokenIcon.Height) / 2d), BrokenIcon.Size));
                }
            } else if (_current.BitmapSource != null) {
                dc.DrawImage(_current.BitmapSource, new Rect(new Point(), RenderSize));
            } else if (ShowBroken && _loaded) {
                dc.DrawImage(BrokenIcon.ImageSource,
                        new Rect(new Point((RenderSize.Width - BrokenIcon.Width) / 2d, (RenderSize.Height - BrokenIcon.Height) / 2d), BrokenIcon.Size));
            }
        }

        private static Size ComputeScaleFactor(Size availableSize, Size contentSize, Stretch stretch, StretchDirection stretchDirection) {
            var scaleX = 1.0;
            var scaleY = 1.0;

            var isConstrainedWidth = !double.IsPositiveInfinity(availableSize.Width);
            var isConstrainedHeight = !double.IsPositiveInfinity(availableSize.Height);
            if (stretch != Stretch.Uniform && stretch != Stretch.UniformToFill && stretch != Stretch.Fill || !isConstrainedWidth && !isConstrainedHeight) {
                return new Size(scaleX, scaleY);
            }

            scaleX = Equals(contentSize.Width, 0d) ? 0.0 : availableSize.Width / contentSize.Width;
            scaleY = Equals(contentSize.Height, 0d) ? 0.0 : availableSize.Height / contentSize.Height;

            if (!isConstrainedWidth) {
                scaleX = scaleY;
            } else if (!isConstrainedHeight) {
                scaleY = scaleX;
            } else {
                switch (stretch) {
                    case Stretch.Uniform:
                        var minscale = scaleX < scaleY ? scaleX : scaleY;
                        scaleX = scaleY = minscale;
                        break;

                    case Stretch.UniformToFill:
                        var maxscale = scaleX > scaleY ? scaleX : scaleY;
                        scaleX = scaleY = maxscale;
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
        }

        private static Style CreateDefaultStyles() {
            var style = new Style(typeof(BetterImage), null);
            style.Setters.Add(new Setter(FlowDirectionProperty, FlowDirection.LeftToRight));
            style.Seal();
            return style;
        }
        #endregion
    }
}
