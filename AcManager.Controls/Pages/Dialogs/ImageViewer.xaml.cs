using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Path = System.IO.Path;

namespace AcManager.Controls.Pages.Dialogs {
    public partial class ImageViewer { 
        public ImageViewer(string image) : this(new[] { image }) { }

        public ImageViewer(IEnumerable<string> images, int position = 0) {
            DataContext = new ImageViewerViewModel(images, position);
            InitializeComponent();
            Buttons = new Button[] { };
        }

        private void ImageViewer_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                Close();
            }
        }

        private void ImageViewer_OnKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack ||
                    e.Key == Key.Q || e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                Close();
            } else if (e.Key == Key.Left || e.Key == Key.K) {
                Model.CurrentPosition--;
            } else if (e.Key == Key.Right || e.Key == Key.J) {
                Model.CurrentPosition++;
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
            return IsSelected ? Model.CurrentImage : null;
        }

        private void ApplyButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            IsSelected = true;
            Close();
        }

        private void CloseButton_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            Close();
        }

        public ImageViewerViewModel Model => (ImageViewerViewModel)DataContext;

        public class ImageViewerViewModel : NotifyPropertyChanged {
            /* TODO: cache & preload for better UX? */

            private readonly IReadOnlyList<string> _images;

            private int _currentPosition;

            public int CurrentPosition {
                get { return _currentPosition; }
                set {
                    if (Equals(value, _currentPosition) || value < 0 || value >= _images.Count) return;
                    var oldPosition = _currentPosition;
                    _currentPosition = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentImage));

                    if (oldPosition == 0 || value == 0) {
                        _previousCommand?.OnCanExecuteChanged();
                    }

                    var last = _images.Count - 1;
                    if (oldPosition == last || value == last) {
                        _nextCommand?.OnCanExecuteChanged();
                    }
                }
            }

            public double MaxImageWidth {
                get { return _maxImageWidth; }
                set {
                    if (value.Equals(_maxImageWidth)) return;
                    _maxImageWidth = value;
                    OnPropertyChanged();
                }
            }

            public double MaxImageHeight {
                get { return _maxImageHeight; }
                set {
                    if (value.Equals(_maxImageHeight)) return;
                    _maxImageHeight = value;
                    OnPropertyChanged();
                }
            }

            public string CurrentImage => _images[_currentPosition];

            public string CurrentImageName => Path.GetFileName(CurrentImage);

            private bool _selectionMode;

            public bool SelectionMode {
                get { return _selectionMode; }
                set {
                    if (Equals(value, _selectionMode)) return;
                    _selectionMode = value;
                    OnPropertyChanged();
                }
            }

            public ImageViewerViewModel(IEnumerable<string> images, int position) {
                _images = images.ToList();
                CurrentPosition = position;
            }

            private RelayCommand _previousCommand;

            public RelayCommand PreviousCommand => _previousCommand ?? (_previousCommand = new RelayCommand(o => {
                CurrentPosition--;
            }, o => CurrentPosition > 0));

            private RelayCommand _nextCommand;
            private double _maxImageWidth = double.MaxValue;
            private double _maxImageHeight = double.MaxValue;

            public RelayCommand NextCommand => _nextCommand ?? (_nextCommand = new RelayCommand(o => {
                CurrentPosition++;
            }, o => CurrentPosition < _images.Count - 1));
        }
    }
}
