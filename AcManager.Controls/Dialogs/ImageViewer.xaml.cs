using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace AcManager.Controls.Dialogs {
    [CanBeNull]
    public delegate object ImageViewerDetailsCallback([CanBeNull] object image);

    public partial class ImageViewer {
        public ImageViewer(ImageSource imageSource, ImageViewerDetailsCallback details = null) : this(new[] { imageSource }, details: details) { }

        public ImageViewer(string image, double maxWidth = double.MaxValue, double maxHeight = double.MaxValue, ImageViewerDetailsCallback details = null) :
                this(new[] { image }, 0, maxWidth, maxHeight, details) { }

        public ImageViewer(IEnumerable<object> images, int position = 0, double maxWidth = double.MaxValue, double maxHeight = double.MaxValue,
                ImageViewerDetailsCallback details = null) {
            DataContext = new ViewModel(images, position, details) {
                MaxImageWidth = maxWidth,
                MaxImageHeight = maxHeight
            };

            InitializeComponent();
            Buttons = new Button[] { };
            ((ViewModel)DataContext).PropertyChanged += OnModelPropertyChanged;
        }

        public HorizontalAlignment HorizontalDetailsAlignment {
            get => Details.HorizontalAlignment;
            set => Details.HorizontalAlignment = value;
        }

        public VerticalAlignment VerticalDetailsAlignment {
            get => Details.VerticalAlignment;
            set => Details.VerticalAlignment = value;
        }

        public Thickness ImageMargin {
            get => Image.Margin;
            set => Image.Margin= value;
        }

        private void OnModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            // if (e.PropertyName == )
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                Close();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key >= Key.D1 && e.Key <= Key.D9) {
                Model.CurrentPosition = e.Key - Key.D1;
            } else if (e.Key == Key.Left || e.Key == Key.K) {
                Model.CurrentPosition--;
            } else if (e.Key == Key.Right || e.Key == Key.J) {
                Model.CurrentPosition++;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack ||
                    e.Key == Key.Q || e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                Close();
            } else if (e.Key == Key.Enter) {
                IsSelected = true;
                Close();
            }
        }

        public bool IsSelected;

        public int? ShowDialogInSelectMode() {
            Model.SelectionMode = true;
            ShowDialog();
            return IsSelected ? Model.CurrentPosition : (int?)null;
        }

        [CanBeNull]
        public string ShowDialogInSelectFileMode() {
            Model.SelectionMode = true;
            ShowDialog();
            return IsSelected ? Model.CurrentOriginalImage as string : null;
        }

        private void OnApplyButtonClick(object sender, MouseButtonEventArgs e) {
            IsSelected = true;
            Close();
        }

        private void OnCloseButtonClick(object sender, MouseButtonEventArgs e) {
            Close();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (double.IsInfinity(Model.MaxImageHeight)) {
                Model.MaxImageHeight = Wrapper.Height;
            }

            if (double.IsInfinity(Model.MaxImageWidth)) {
                Model.MaxImageWidth = Wrapper.Width;
            }
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            [CanBeNull]
            private readonly ImageViewerDetailsCallback _details;

            [NotNull]
            private readonly object[] _images;

            [NotNull]
            private int _imagesLength;

            [NotNull]
            private readonly object[] _originalImages;

            public ViewModel(IEnumerable<object> images, int position, [CanBeNull] ImageViewerDetailsCallback details) {
                _details = details;
                _originalImages = images.ToArray();
                _images = _originalImages.ToArray();
                _imagesLength = _images.Length;

                CurrentPosition = position;
                UpdateCurrent();
            }

            private void Preload(int position) {
                if (position < 0 || position >= _imagesLength) return;

                string path;
                lock (_images) {
                    path = _images[position] as string;
                }

                if (path != null) {
                    BetterImage.LoadBitmapSourceAsync(path, double.IsPositiveInfinity(MaxImageWidth) ? -1 : (int)MaxImageWidth).ContinueWith(r => {
                        if (r.Result.IsBroken) return;
                        lock (_images) {
                            var updated = _images[position];
                            if (updated as string == path) {
                                _images[position] = r.Result;
                            }
                        }
                    }, TaskContinuationOptions.OnlyOnRanToCompletion);
                }
            }

            private void Unload(int position) {
                lock (_images) {
                    _images[position] = _originalImages[position];
                }
            }

            private void UpdateCurrent() {
                var position = _currentPosition;

                object current;
                lock (_images) {
                    current = _images[position];
                }

                CurrentDetails = _details?.Invoke(_originalImages[position]);

                var path = current as string;
                if (path != null) {
                    var loaded = BetterImage.LoadBitmapSource(path, double.IsPositiveInfinity(MaxImageWidth) ? -1 : (int)MaxImageWidth);
                    lock (_images) {
                        _images[position] = loaded;
                    }
                }

                Preload(position + 1);
                Preload(position - 1);

                for (var i = 0; i < _imagesLength; i++) {
                    var offset = (i - position).Abs();
                    if (offset > 5) {
                        Unload(i);
                    }
                }
            }

            private int _currentPosition;

            public int CurrentPosition {
                get => _currentPosition;
                set {
                    value = value.Clamp(0, _imagesLength - 1);
                    if (Equals(value, _currentPosition)) return;

                    var oldPosition = _currentPosition;
                    _currentPosition = value;
                    UpdateCurrent();

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentImage));
                    OnPropertyChanged(nameof(CurrentImageName));
                    OnPropertyChanged(nameof(CurrentOriginalImage));

                    var last = _imagesLength - 1;
                    if (oldPosition == 0 || value == 0) {
                        _previousCommand?.RaiseCanExecuteChanged();
                    }

                    if (oldPosition == last || value == last) {
                        _nextCommand?.RaiseCanExecuteChanged();
                    }

                    _saveCommand?.RaiseCanExecuteChanged();
                }
            }

            private bool _saveable;

            public bool Saveable {
                get => _saveable;
                set {
                    if (Equals(value, _saveable)) return;
                    _saveable = value;
                    OnPropertyChanged();
                }
            }

            private string _saveableTitle = ControlsStrings.ImageViewer_Save_Title;

            public string SaveableTitle {
                get => _saveableTitle;
                set {
                    if (Equals(value, _saveableTitle)) return;
                    _saveableTitle = value;
                    OnPropertyChanged();
                }
            }

            private string _saveDirectory;

            public string SaveDirectory {
                get => _saveDirectory;
                set {
                    if (Equals(value, _saveDirectory)) return;
                    _saveDirectory = value;
                    OnPropertyChanged();
                }
            }

            private double _maxImageWidth = double.MaxValue;

            public double MaxImageWidth {
                get => _maxImageWidth;
                set {
                    if (value.Equals(_maxImageWidth)) return;
                    _maxImageWidth = value;
                    OnPropertyChanged();
                }
            }

            private double _maxImageHeight = double.MaxValue;

            public double MaxImageHeight {
                get => _maxImageHeight;
                set {
                    if (value.Equals(_maxImageHeight)) return;
                    _maxImageHeight = value;
                    OnPropertyChanged();
                }
            }

            private object _currentDetails;

            public object CurrentDetails {
                get => _currentDetails;
                set {
                    if (Equals(value, _currentDetails)) return;
                    _currentDetails = value;
                    OnPropertyChanged();
                }
            }

            public object CurrentImage {
                get {
                    lock (_images) {
                        return _images[_currentPosition];
                    }
                }
            }

            public object CurrentOriginalImage => _originalImages[_currentPosition];
            public string CurrentImageName => Path.GetFileName(CurrentOriginalImage as string ?? ControlsStrings.ImageViewer_DefaultName);

            private bool _selectionMode;

            public bool SelectionMode {
                get => _selectionMode;
                set {
                    if (Equals(value, _selectionMode)) return;
                    _selectionMode = value;
                    OnPropertyChanged();
                }
            }

            private CommandBase _previousCommand;

            public ICommand PreviousCommand => _previousCommand ?? (_previousCommand = new DelegateCommand(() => {
                CurrentPosition--;
            }, () => CurrentPosition > 0));

            private CommandBase _nextCommand;

            public ICommand NextCommand => _nextCommand ?? (_nextCommand = new DelegateCommand(() => {
                CurrentPosition++;
            }, () => CurrentPosition < _imagesLength - 1));

            private CommandBase _saveCommand;

            public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new AsyncCommand(async () => {
                if (!(CurrentOriginalImage is string origin)) {
                    throw new NotSupportedException();
                }

                var filename = FileRelatedDialogs.Save(new SaveDialogParams {
                    Filters = { DialogFilterPiece.ImageFiles },
                    Title = SaveableTitle,
                    DetaultExtension = Path.GetExtension(origin),
                    InitialDirectory = SaveDirectory
                });
                if (filename == null) return;

                try {
                    await Task.Run(() => File.Copy(origin, filename, true));
                } catch (Exception ex) {
                    NonfatalError.Notify(ControlsStrings.ImageViewer_CannotSave, ex);
                }
            }, () => CurrentOriginalImage is string));
        }
    }
}
