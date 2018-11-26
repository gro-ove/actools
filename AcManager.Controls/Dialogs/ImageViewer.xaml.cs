using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.Controls.Presentation;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Path = System.IO.Path;

namespace AcManager.Controls.Dialogs {
    [CanBeNull]
    public delegate object ImageViewerDetailsCallback<in T>([CanBeNull] T value);

    [NotNull, ItemCanBeNull]
    public delegate Task<object> ImageViewerImageCallback<in T>([CanBeNull] T value);

    public delegate void ImageViewerContextMenuCallback<in T>([CanBeNull] T value);

    public delegate bool ImageViewerCanBeSavedCallback<in T>([CanBeNull] T value);

    [NotNull]
    public delegate Task ImageViewerSaveCallback<in T>([CanBeNull] T value, string destination);

    public class ImageViewer<T> : ImageViewer {
        private readonly T[] _list;

        public ImageViewer([NotNull] T item, [CanBeNull] ImageViewerImageCallback<T> imageCallback,
                [CanBeNull] ImageViewerDetailsCallback<T> detailsCallback = null) : this(new[] { item }, imageCallback, detailsCallback) { }

        public ImageViewer([NotNull] IEnumerable<T> items, [CanBeNull] ImageViewerImageCallback<T> imageCallback,
                [CanBeNull] ImageViewerDetailsCallback<T> detailsCallback = null) {
            _list = items.ToArray();
            if (imageCallback == null) {
                imageCallback = x => Task.FromResult((object)x);
            }

            FinishInitialization(new ViewModel<T>(i => imageCallback(_list.ArrayElementAtOrDefault(i)), _list.Length, 0,
                    i => detailsCallback?.Invoke(_list.ArrayElementAtOrDefault(i)), _list));
        }

        public ImageViewer([NotNull] IEnumerable<T> items, int position, [CanBeNull] ImageViewerImageCallback<T> imageCallback,
                [CanBeNull] ImageViewerDetailsCallback<T> detailsCallback = null) {
            _list = items.ToArray();
            if (imageCallback == null) {
                imageCallback = x => Task.FromResult((object)x);
            }

            FinishInitialization(new ViewModel<T>(i => imageCallback(_list.ArrayElementAtOrDefault(i)), _list.Length, position,
                    i => detailsCallback?.Invoke(_list.ArrayElementAtOrDefault(i)), _list));
        }

        public new T SelectDialog() {
            Model.SelectionMode = true;
            ShowDialog();
            return IsSelected ? _list.ElementAtOrDefault(Model.CurrentPosition) : default;
        }

        public new ViewModel<T> Model => (ViewModel<T>)DataContext;

        public class ViewModel<TModel> : ViewModel {
            private readonly TModel[] _list;

            public ViewModel([NotNull] ImageViewerImageCallback imageCallback, int count, int position, [CanBeNull] ImageViewerDetailsCallback detailsCallback,
                    TModel[] list)
                    : base(imageCallback, count, position, detailsCallback) {
                _list = list;
            }

            [CanBeNull]
            public new ImageViewerContextMenuCallback<TModel> ContextMenuCallback {
                set => base.ContextMenuCallback = i => value(_list.ArrayElementAtOrDefault(i));
            }

            [CanBeNull]
            public new ImageViewerCanBeSavedCallback<TModel> CanBeSavedCallback {
                set => base.CanBeSavedCallback = i => value(_list.ArrayElementAtOrDefault(i));
            }

            [CanBeNull]
            public new ImageViewerSaveCallback<TModel> SaveCallback {
                set => base.SaveCallback = (i, s) => value(_list.ArrayElementAtOrDefault(i), s);
            }
        }
    }

    [CanBeNull]
    public delegate object ImageViewerDetailsCallback(int index);

    [NotNull, ItemCanBeNull]
    public delegate Task<object> ImageViewerImageCallback(int index);

    public delegate void ImageViewerContextMenuCallback(int index);

    public delegate bool ImageViewerCanBeSavedCallback(int index);

    [NotNull]
    public delegate Task ImageViewerSaveCallback(int index, string destination);

    public partial class ImageViewer {
        public ImageViewer(string value, [CanBeNull] ImageViewerDetailsCallback detailsCallback = null) {
            FinishInitialization(new ViewModel(i => Task.FromResult((object)value), 1, 0, detailsCallback));
        }

        public ImageViewer(ImageSource value, [CanBeNull] ImageViewerDetailsCallback detailsCallback = null) {
            FinishInitialization(new ViewModel(i => Task.FromResult((object)value), 1, 0, detailsCallback));
        }

        public ImageViewer(BetterImage.Image value, [CanBeNull] ImageViewerDetailsCallback detailsCallback = null) {
            FinishInitialization(new ViewModel(i => Task.FromResult((object)value), 1, 0, detailsCallback));
        }

        public ImageViewer(IEnumerable<string> images, int position = 0, ImageViewerDetailsCallback detailsCallback = null) {
            var list = images.ToList();
            FinishInitialization(new ViewModel(i => Task.FromResult((object)list.ElementAtOrDefault(i)), list.Count, position, detailsCallback));
        }

        public ImageViewer(IEnumerable<ImageSource> images, int position = 0, ImageViewerDetailsCallback detailsCallback = null) {
            var list = images.ToList();
            FinishInitialization(new ViewModel(i => Task.FromResult((object)list.ElementAtOrDefault(i)), list.Count, position, detailsCallback));
        }

        public ImageViewer(IEnumerable<BetterImage.Image> images, int position = 0, ImageViewerDetailsCallback detailsCallback = null) {
            var list = images.ToList();
            FinishInitialization(new ViewModel(i => Task.FromResult((object)list.ElementAtOrDefault(i)), list.Count, position, detailsCallback));
        }

        public ImageViewer(IEnumerable<object> images, int position = 0, ImageViewerDetailsCallback detailsCallback = null) {
            var list = images.ToList();
            FinishInitialization(new ViewModel(i => Task.FromResult(list.ElementAtOrDefault(i)), list.Count, position, detailsCallback));
        }

        public ImageViewer([NotNull] ImageViewerImageCallback imageCallback, int count, int position,
                [CanBeNull] ImageViewerDetailsCallback detailsCallback = null) {
            FinishInitialization(new ViewModel(imageCallback, count, position, detailsCallback));
        }

        protected ImageViewer() { }

        protected void FinishInitialization(ViewModel viewModel) {
            DataContext = viewModel;

            InitializeComponent();
            Owner = null;
            Buttons = new Button[] { };

            if (AppAppearanceManager.Instance.BlurImageViewerBackground) {
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                BlurBackground = true;
            }
        }

        public HorizontalAlignment HorizontalDetailsAlignment {
            get => Details.HorizontalAlignment;
            set => Details.HorizontalAlignment = value;
        }

        public VerticalAlignment VerticalDetailsAlignment {
            get => Details.VerticalAlignment;
            set {
                Details.VerticalAlignment = value;
                DetailsWrapper.VerticalAlignment = value;
            }
        }

        public Thickness ImageMargin {
            get => Image.Margin;
            set => Image.Margin = value;
        }

        public static readonly DependencyProperty MaxAreaWidthProperty = DependencyProperty.Register(nameof(MaxAreaWidth), typeof(double),
                typeof(ImageViewer), new FrameworkPropertyMetadata(double.PositiveInfinity));

        public double MaxAreaWidth {
            get => (double)GetValue(MaxAreaWidthProperty);
            set => SetValue(MaxAreaWidthProperty, value);
        }

        public static readonly DependencyProperty MaxAreaHeightProperty = DependencyProperty.Register(nameof(MaxAreaHeight), typeof(double),
                typeof(ImageViewer), new FrameworkPropertyMetadata(double.PositiveInfinity));

        public double MaxAreaHeight {
            get => (double)GetValue(MaxAreaHeightProperty);
            set => SetValue(MaxAreaHeightProperty, value);
        }

        private double _maxWidth = double.PositiveInfinity,
                _maxHeight = double.PositiveInfinity;

        public double MaxImageWidth {
            get => _maxWidth;
            set {
                _maxWidth = value;
                if (IsLoaded) {
                    Image.MaxWidth = _maxWidth;
                }
                Model.SetMaxWidth(value);
            }
        }

        public double MaxImageHeight {
            get => _maxHeight;
            set {
                _maxHeight = value;
                if (IsLoaded) {
                    Image.MaxHeight = _maxHeight;
                }
                Model.SetMaxHeight(value);
            }
        }

        public bool AutoHideDescriptionIfExpanded {
            get => DetailsWrapper.Style != null;
            set => DetailsWrapper.Style = value ? (Style)FindResource(@"FadingBorder") : null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (double.IsInfinity(_maxWidth)) {
                Image.MaxWidth = GetScreen().Bounds.Width;
                Model.SetMaxWidth(Image.MaxWidth);
            } else {
                Image.MaxWidth = _maxWidth;
            }

            if (double.IsInfinity(_maxHeight)) {
                Image.MaxHeight = GetScreen().Bounds.Height;
                Model.SetMaxHeight(Image.MaxHeight);
            } else {
                Image.MaxHeight = _maxHeight;
            }

            if (Image.MaxWidth > 1600 && Image.MaxHeight > 960) {
                Details.Children.Insert(0, new BlurredPiece { Visual = Image });
            } else {
                this.FindVisualChildren<BlurredPiece>().ForEach(x => x.Tag = false);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1 && !Model.Saveable) {
                e.Handled = true;
                Close();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key >= Key.D1 && e.Key <= Key.D9) {
                e.Handled = true;
                Model.CurrentPosition = e.Key - Key.D1;
            } else if (e.Key == Key.Left || e.Key == Key.K) {
                e.Handled = true;
                Model.CurrentPosition--;
            } else if (e.Key == Key.Right || e.Key == Key.J) {
                e.Handled = true;
                Model.CurrentPosition++;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack ||
                    e.Key == Key.Q || e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                e.Handled = true;
                Close();
            } else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                e.Handled = true;
                Model.SaveCommand.Execute(null);
            } else if (e.Key == Key.Enter) {
                e.Handled = true;
                if (Model.SaveCommand.CanExecute(null)) {
                    Model.SaveCommand.Execute(null);
                } else {
                    IsSelected = true;
                    Close();
                }
            }
        }

        public bool IsSelected;

        public int? SelectDialog() {
            Model.SelectionMode = true;
            ShowDialog();
            return IsSelected ? Model.CurrentPosition : (int?)null;
        }

        public new void ShowDialog() {
            if (!Model.Saveable && (DateTime.Now - LastActiveWindow?.LastActivated)?.TotalSeconds < 0.3) return;
            base.ShowDialog();
        }

        private void OnApplyButtonClick(object sender, RoutedEventArgs routedEventArgs) {
            IsSelected = true;
            Close();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs routedEventArgs) {
            Close();
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            Model.ContextMenuCallback?.Invoke(Model.CurrentPosition);
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ImageInformation : Displayable {
            private readonly ImageViewerImageCallback _imageCallback;
            private readonly ImageViewerDetailsCallback _detailsCallback;
            private readonly int _position;

            public ImageInformation(ImageViewerImageCallback imageCallback, ImageViewerDetailsCallback detailsCallback, int position) {
                _imageCallback = imageCallback;
                _detailsCallback = detailsCallback;
                _position = position;
                _details = Lazier.Create(GetDetails);
            }

            private object GetDetails() {
                return _detailsCallback?.Invoke(_position);
            }

            private readonly Lazier<object> _details;
            public object Details => _details.Value;

            private bool _isLoaded;

            public bool IsLoaded {
                get => _isLoaded;
                set => Apply(value, ref _isLoaded);
            }

            private bool _isLoading;

            public bool IsLoading {
                get => _isLoading;
                set => Apply(value, ref _isLoading);
            }

            private bool _isFailedToLoad;

            public bool IsFailedToLoad {
                get => _isFailedToLoad;
                set => Apply(value, ref _isFailedToLoad);
            }

            private string _errorMessage;

            public string ErrorMessage {
                get => _errorMessage;
                set => Apply(value, ref _errorMessage);
            }

            private BetterImage.Image _image;

            public BetterImage.Image Image {
                get {
                    if (!IsLoaded && !IsLoading) {
                        LoadImageAsync().Ignore();
                    }
                    return _image;
                }
                set => Apply(value, ref _image);
            }

            public void Preload() {
                if (!IsLoaded && !IsLoading) {
                    LoadImageAsync().Ignore();
                }
            }

            public void Unload() {
                if (IsLoaded) {
                    Image = BetterImage.Image.Empty;
                    IsLoaded = false;
                    IsLoading = false;
                    IsFailedToLoad = false;
                    ErrorMessage = null;
                }
            }

            public Task<object> GetOriginalDataAsync() {
                return _imageCallback(_position);
            }

            public async Task SaveAsync(string destination) {
                var input = await _imageCallback(_position).ConfigureAwait(false);

                switch (input) {
                    case null:
                        throw new Exception("Nothing to save");
                    case string filename:
                        await Task.Run(() => File.Copy(filename, destination)).ConfigureAwait(false);
                        break;
                    case byte[] data:
                        await FileUtils.WriteAllBytesAsync(destination, data).ConfigureAwait(false);
                        break;
                    case BetterImage.Image ready when ready.ImageSource is BitmapSource imageBitmapSource:
                        imageBitmapSource.SaveTo(destination);
                        break;
                    case BitmapSource bitmapSource:
                        bitmapSource.SaveTo(destination);
                        break;
                    default:
                        DisplayName = null;
                        throw new Exception("Not supported: " + input);
                }
            }

            private string _filename;

            public string Filename {
                get => _filename;
                set => Apply(value, ref _filename, () => DisplayName = Path.GetFileNameWithoutExtension(value));
            }

            private bool _canBeSaved;

            public bool CanBeSaved {
                get => _canBeSaved;
                set => Apply(value, ref _canBeSaved);
            }

            public int MaxWidth { get; set; }
            public int MaxHeight { get; set; }

            private async Task LoadImageAsync() {
                try {
                    IsLoading = true;

                    var input = await GetOriginalDataAsync();

                    BetterImage.Image result;
                    string filename;
                    bool canBeSaved;

                    switch (input) {
                        case null:
                            filename = null;
                            canBeSaved = false;
                            result = BetterImage.Image.Empty;
                            break;
                        case string inputFilename:
                            filename = inputFilename;
                            canBeSaved = true;
                            result = await BetterImage.LoadBitmapSourceAsync(inputFilename, MaxWidth, MaxHeight);
                            break;
                        case byte[] data:
                            filename = null;
                            canBeSaved = true;
                            result = await Task.Run(() => BetterImage.LoadBitmapSourceFromBytes(data, MaxWidth, MaxHeight));
                            break;
                        case BetterImage.Image ready:
                            filename = null;
                            canBeSaved = ready.ImageSource is BitmapSource;
                            result = ready;
                            break;
                        case BitmapSource bitmapSource:
                            filename = null;
                            canBeSaved = true;
                            result = new BetterImage.Image(bitmapSource);
                            break;
                        case ImageSource imageSource:
                            filename = null;
                            canBeSaved = false;
                            result = new BetterImage.Image(imageSource);
                            break;
                        default:
                            filename = null;
                            canBeSaved = false;
                            result = BetterImage.Image.Empty;
                            Logging.Error("Not supported: " + input.GetType());
                            break;
                    }

                    Image = result;
                    Filename = filename;
                    CanBeSaved = canBeSaved;
                } catch (Exception e) {
                    Logging.Warning(e);
                    Image = BetterImage.Image.Empty;
                    Filename = null;
                    CanBeSaved = false;
                    IsFailedToLoad = true;
                    ErrorMessage = e.Message;
                } finally {
                    IsLoading = false;
                    IsLoaded = true;
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            [NotNull]
            private readonly ImageInformation[] _images;

            public ViewModel([NotNull] ImageViewerImageCallback imageCallback, int count, int position, [CanBeNull] ImageViewerDetailsCallback detailsCallback) {
                _images = Enumerable.Range(0, count).Select(x => new ImageInformation(imageCallback, detailsCallback, x)).ToArray();
                CurrentPosition = position;
                DelayedInitialization();
            }

            internal void SetMaxWidth(double value) {
                foreach (var image in _images) {
                    image.MaxWidth = double.IsInfinity(value) ? -1 : value.RoundToInt();
                }
            }

            internal void SetMaxHeight(double value) {
                foreach (var image in _images) {
                    image.MaxHeight = double.IsInfinity(value) ? -1 : value.RoundToInt();
                }
            }

            private async void DelayedInitialization() {
                await Task.Yield();
                UpdateCurrent();
            }

            private void Preload(int position) {
                _images.ArrayElementAtOrDefault(ClampPosition(position))?.Preload();
            }

            private void Unload(int position) {
                _images.ArrayElementAtOrDefault(position)?.Unload();
            }

            private void UpdateCurrent() {
                var position = _currentPosition;
                Current = _images.ArrayElementAtOrDefault(position);

                Preload(position + 1);
                Preload(position - 1);
                for (var i = 0; i < _images.Length; i++) {
                    var offset = (i - position).Abs();

                    if (IsLooped) {
                        var around = (offset - _images.Length).Abs();
                        if (around < offset) {
                            offset = around;
                        }
                    }

                    if (offset > 5) {
                        Unload(i);
                    }
                }
            }

            private int _currentPosition;

            public int CurrentPosition {
                get => _currentPosition;
                set {
                    value = ClampPosition(value);
                    if (Equals(value, _currentPosition)) return;

                    var oldPosition = _currentPosition;
                    _currentPosition = value;
                    UpdateCurrent();
                    OnPropertyChanged();

                    var last = _images.Length - 1;
                    if (oldPosition == 0 || value == 0) {
                        _previousCommand?.RaiseCanExecuteChanged();
                    }

                    if (oldPosition == last || value == last) {
                        _nextCommand?.RaiseCanExecuteChanged();
                    }

                    _saveCommand?.RaiseCanExecuteChanged();
                }
            }

            private int ClampPosition(int value) {
                if (IsLooped) {
                    if (value < 0) {
                        value = _images.Length - 1;
                    } else if (value >= _images.Length) {
                        value = 0;
                    }
                } else {
                    value = value.Clamp(0, _images.Length - 1);
                }
                return value;
            }

            private bool _isLooped = true;

            public bool IsLooped {
                get => _isLooped;
                set => Apply(value, ref _isLooped);
            }

            private bool _saveable;

            public bool Saveable {
                get => _saveable;
                set => Apply(value, ref _saveable);
            }

            private string _saveableTitle = ControlsStrings.ImageViewer_Save_Title;

            public string SaveableTitle {
                get => _saveableTitle;
                set => Apply(value, ref _saveableTitle);
            }

            private string _saveDirectory;

            public string SaveDirectory {
                get => _saveDirectory;
                set => Apply(value, ref _saveDirectory);
            }

            private ImageInformation _current;

            [CanBeNull]
            public ImageInformation Current {
                get => _current;
                set => Apply(value, ref _current);
            }

            private bool _selectionMode;

            public bool SelectionMode {
                get => _selectionMode;
                set => Apply(value, ref _selectionMode);
            }

            private DelegateCommand _previousCommand, _nextCommand;

            public DelegateCommand PreviousCommand => _previousCommand ?? (_previousCommand =
                    new DelegateCommand(() => CurrentPosition--, () => IsLooped || CurrentPosition > 0));

            public DelegateCommand NextCommand => _nextCommand ?? (_nextCommand =
                    new DelegateCommand(() => CurrentPosition++, () => IsLooped || CurrentPosition < _images.Length - 1));

            [CanBeNull]
            public ImageViewerContextMenuCallback ContextMenuCallback { get; set; }

            [CanBeNull]
            public ImageViewerCanBeSavedCallback CanBeSavedCallback { get; set; }

            [CanBeNull]
            public ImageViewerSaveCallback SaveCallback { get; set; }

            [NotNull]
            public List<DialogFilterPiece> SaveDialogFilterPieces { get; } = new List<DialogFilterPiece>();

            private CommandBase _saveCommand;

            public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new AsyncCommand(async () => {
                try {
                    var current = Current;
                    if (current == null) return;

                    var extension = Path.GetExtension(current.Filename)?.ToLowerInvariant();
                    if (SaveDialogFilterPieces.Count == 0) {
                        if (extension == null || extension == @".png") {
                            SaveDialogFilterPieces.Add(DialogFilterPiece.PngFiles);
                        }
                        if (extension == null || extension == @".jpg" || extension == @".jpeg") {
                            SaveDialogFilterPieces.Add(DialogFilterPiece.JpegFiles);
                        }
                    }

                    var filename = FileRelatedDialogs.Save(new SaveDialogParams {
                        Filters = SaveDialogFilterPieces,
                        Title = SaveableTitle,
                        DetaultExtension = extension ?? @".png",
                        InitialDirectory = SaveDirectory
                    });

                    if (filename != null) {
                        await (SaveCallback?.Invoke(_currentPosition, filename) ?? current.SaveAsync(filename));
                    }
                } catch (Exception ex) {
                    NonfatalError.Notify(ControlsStrings.ImageViewer_CannotSave, ex);
                }
            }, () => Current?.CanBeSaved == true && CanBeSavedCallback?.Invoke(_currentPosition) != false));
        }
    }
}