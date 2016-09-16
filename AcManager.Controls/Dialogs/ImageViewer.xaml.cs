using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace AcManager.Controls.Dialogs {
    public partial class ImageViewer {
        public ImageViewer(ImageSource imageSource) : this(new[] { imageSource }) { }

        public ImageViewer(string image, double maxWidth = double.MaxValue, double maxHeight = double.MaxValue) : this(new[] { image }, 0, maxWidth, maxHeight) { }

        public ImageViewer(IEnumerable<object> images, int position = 0, double maxWidth = double.MaxValue, double maxHeight = double.MaxValue) {
            DataContext = new ViewModel(images, position) {
                MaxImageWidth = maxWidth,
                MaxImageHeight = maxHeight
            };
            InitializeComponent();
            Buttons = new Button[] { };
        }

        private void ImageViewer_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                Close();
            }
        }

        private void ImageViewer_OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key >= Key.D1 && e.Key <= Key.D9) {
                Model.CurrentPosition = e.Key - Key.D1;
            } else if (e.Key == Key.Left || e.Key == Key.K) {
                Model.CurrentPosition--;
            } else if (e.Key == Key.Right || e.Key == Key.J) {
                Model.CurrentPosition++;
            }
        }

        private void ImageViewer_OnKeyUp(object sender, KeyEventArgs e) {
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

        private void ApplyButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            IsSelected = true;
            Close();
        }

        private void CloseButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            Close();
        }

        private void ImageViewer_OnLoaded(object sender, RoutedEventArgs e) {
            if (double.IsInfinity(Model.MaxImageHeight)) {
                Model.MaxImageHeight = Wrapper.Height;
            }

            if (double.IsInfinity(Model.MaxImageWidth)) {
                Model.MaxImageWidth = Wrapper.Width;
            }
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private readonly object[] _images;
            private readonly object[] _originalImages;
            
            public ViewModel(IEnumerable<object> images, int position) {
                _originalImages = images.ToArray();
                _images = _originalImages.ToArray();

                CurrentPosition = position;
                UpdateCurrent();
            }

            private async void UpdateCurrent() {
                var position = _currentPosition;
                var path = _images[position] as string;
                if (path != null) {
                    _images[position] = BetterImage.LoadBitmapSource(path, double.IsPositiveInfinity(MaxImageWidth) ? -1 : (int)MaxImageWidth);
                }

                if (position < _images.Length - 1) {
                    var next = position + 1;
                    var nextPath = _images[next] as string;
                    if (nextPath != null) {
                        var loaded = await BetterImage.LoadBitmapSourceAsync(nextPath, double.IsPositiveInfinity(MaxImageWidth) ? -1 : (int)MaxImageWidth);
                        var updated = _images[next];
                        if (updated as string != nextPath) return;
                        _images[next] = loaded;
                    }
                }

                if (position > 1) {
                    var next = position - 1;
                    var nextPath = _images[next] as string;
                    if (nextPath != null) {
                        var loaded = await BetterImage.LoadBitmapSourceAsync(nextPath, double.IsPositiveInfinity(MaxImageWidth) ? -1 : (int)MaxImageWidth);
                        var updated = _images[next];
                        if (updated as string != nextPath) return;
                        _images[next] = loaded;
                    }
                }

                for (var i = 0; i < position - 2; i++) {
                    _images[i] = _originalImages[i];
                }

                for (var i = position + 3; i < _images.Length; i++) {
                    _images[i] = _originalImages[i];
                }
            }

            private int _currentPosition;

            public int CurrentPosition {
                get { return _currentPosition; }
                set {
                    value = value.Clamp(0, _images.Length - 1);
                    if (Equals(value, _currentPosition)) return;

                    var oldPosition = _currentPosition;
                    _currentPosition = value;
                    UpdateCurrent();

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentImage));
                    OnPropertyChanged(nameof(CurrentImageName));
                    OnPropertyChanged(nameof(CurrentOriginalImage));

                    var last = _images.Length - 1;
                    if (oldPosition == 0 || value == 0) {
                        _previousCommand?.OnCanExecuteChanged();
                    }

                    if (oldPosition == last || value == last) {
                        _nextCommand?.OnCanExecuteChanged();
                    }

                    _saveCommand?.OnCanExecuteChanged();
                }
            }

            private bool _saveable;

            public bool Saveable {
                get { return _saveable; }
                set {
                    if (Equals(value, _saveable)) return;
                    _saveable = value;
                    OnPropertyChanged();
                }
            }

            private string _saveableTitle = ControlsStrings.ImageViewer_Save_Title;

            public string SaveableTitle {
                get { return _saveableTitle; }
                set {
                    if (Equals(value, _saveableTitle)) return;
                    _saveableTitle = value;
                    OnPropertyChanged();
                }
            }

            private string _saveDirectory;

            public string SaveDirectory {
                get { return _saveDirectory; }
                set {
                    if (Equals(value, _saveDirectory)) return;
                    _saveDirectory = value;
                    OnPropertyChanged();
                }
            }

            private double _maxImageWidth = double.MaxValue;

            public double MaxImageWidth {
                get { return _maxImageWidth; }
                set {
                    if (value.Equals(_maxImageWidth)) return;
                    _maxImageWidth = value;
                    OnPropertyChanged();
                }
            }

            private double _maxImageHeight = double.MaxValue;

            public double MaxImageHeight {
                get { return _maxImageHeight; }
                set {
                    if (value.Equals(_maxImageHeight)) return;
                    _maxImageHeight = value;
                    OnPropertyChanged();
                }
            }

            public object CurrentImage => _images[_currentPosition];

            public object CurrentOriginalImage => _originalImages[_currentPosition];

            public string CurrentImageName => Path.GetFileName(CurrentOriginalImage as string ?? ControlsStrings.ImageViewer_DefaultName);

            private bool _selectionMode;

            public bool SelectionMode {
                get { return _selectionMode; }
                set {
                    if (Equals(value, _selectionMode)) return;
                    _selectionMode = value;
                    OnPropertyChanged();
                }
            }

            private ICommandExt _previousCommand;

            public ICommand PreviousCommand => _previousCommand ?? (_previousCommand = new DelegateCommand(() => {
                CurrentPosition--;
            }, () => CurrentPosition > 0));

            private ICommandExt _nextCommand;

            public ICommand NextCommand => _nextCommand ?? (_nextCommand = new DelegateCommand(() => {
                CurrentPosition++;
            }, () => CurrentPosition < _images.Length - 1));

            private ICommandExt _saveCommand;

            public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new AsyncCommand(async () => {
                var origin = CurrentOriginalImage as string;
                if (origin == null) {
                    throw new NotSupportedException();
                }

                var dialog = new SaveFileDialog {
                    Filter = FileDialogFilters.ImagesFilter,
                    Title = SaveableTitle,
                    DefaultExt = Path.GetExtension(origin)
                };

                if (SaveDirectory != null) {
                    dialog.InitialDirectory = SaveDirectory;
                }

                if (dialog.ShowDialog() != true) return;

                try {
                    await Task.Run(() => File.Copy(origin, dialog.FileName));
                } catch (Exception ex) {
                    NonfatalError.Notify(ControlsStrings.ImageViewer_CannotSave, ex);
                }
            }, () => CurrentOriginalImage is string));
        }
    }
}
