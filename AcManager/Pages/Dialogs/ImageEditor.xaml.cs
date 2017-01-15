using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class ImageEditor {
        public class CropModel {
            public double TotalWidth,
                    TotalHeight,
                    OffsetX,
                    OffsetY,
                    ImageWidth,
                    ImageHeight,
                    TargetWidth,
                    TargetHeight,
                    MinWidth,
                    MinHeight,
                    MaxWidth,
                    MaxHeight;

            public void SetImageWidth(double newValue) {
                newValue = Math.Max(newValue, MinWidth);
                OffsetX -= (newValue - ImageWidth) / 2;
                ImageWidth = newValue;
            }

            public void SetImageHeight(double newValue) {
                newValue = Math.Max(newValue, MinHeight);
                OffsetY -= (newValue - ImageHeight) / 2;
                ImageHeight = newValue;
            }

            public void Scale(double d) {
                SetImageWidth(ImageWidth * d);
                SetImageHeight(ImageHeight * d);
            }

            public void Check() {
                if (ImageWidth < MinWidth) SetImageWidth(MinWidth);
                if (ImageHeight < MinHeight) SetImageHeight(MinHeight);

                if (ImageWidth > MaxWidth) SetImageWidth(MaxWidth);
                if (ImageHeight > MaxHeight) SetImageHeight(MaxHeight);

                if (OffsetX < 0) OffsetX = 0;
                if (OffsetY < 0) OffsetY = 0;

                if (OffsetX + ImageWidth > TotalWidth) OffsetX = TotalWidth - ImageWidth;
                if (OffsetY + ImageHeight > TotalHeight) OffsetY = TotalHeight - ImageHeight;
            }

            public void CenterAndFit() {
                ImageWidth = MaxWidth;
                ImageHeight = MaxHeight;
                OffsetX = (TotalWidth - ImageWidth) / 2;
                OffsetY = (TotalHeight - ImageHeight) / 2;
            }

            public void CalculateMinAndMax() {
                var kh = TargetHeight / TotalHeight;
                var kw = TargetWidth / TotalWidth;
                var k = Math.Min(kh, kw);
                MaxWidth = TotalWidth * k / kh;
                MaxHeight = TotalHeight * k / kw;

                MinWidth = Math.Min(TargetWidth, MaxWidth * 0.75);
                MinHeight = Math.Min(TargetHeight, MaxHeight * 0.75);
            }
        }
        
        private BetterImage.BitmapEntry _image;
        private readonly CropModel _model;

        public BitmapSource Result { get; private set; }

        public Rect ResultRectRelative { get; private set; }

        public Int32Rect ResultRect { get; private set; }

        /// <summary>
        /// Crop image to fit specified size (or leave it uncropped if specified). Even if user selected
        /// to not crop the image, it still would be resized down to 640×640. If you don’t need this behavior,
        /// change the code.
        /// </summary>
        /// <returns>Cropped image.</returns>
        [CanBeNull]
        public static BitmapSource Proceed([NotNull] string filename, Size size, bool allowNoCrop = true) {
            var dialog = new ImageEditor(filename, size, allowNoCrop, null);
            dialog.ShowDialog();
            return dialog.Result;
        }

        /// <summary>
        /// Crop image to fit specified size (or leave it uncropped if specified).
        /// </summary>
        /// <returns>Cropped image.</returns>
        [CanBeNull]
        public static BitmapSource Proceed([NotNull] string filename, Size size, bool allowNoCrop, Rect? startRect, out Rect resultRelativeRect) {
            var dialog = new ImageEditor(filename, size, allowNoCrop, startRect);
            dialog.ShowDialog();
            resultRelativeRect = dialog.ResultRectRelative;
            return dialog.Result;
        }

        /// <summary>
        /// Crop image to fit specified size (or leave it uncropped if specified).
        /// </summary>
        /// <returns>Cropped image.</returns>
        [CanBeNull]
        public static BitmapSource Proceed([NotNull] string filename, Size size, bool allowNoCrop, Rect? startRect, out Int32Rect resultRect) {
            var dialog = new ImageEditor(filename, size, allowNoCrop, startRect);
            dialog.ShowDialog();
            resultRect = dialog.ResultRect;
            return dialog.Result;
        }

        private ImageEditor(string filename, Size size, bool allowNoCrop, Rect? startRect) {
            _model = new CropModel {
                TargetWidth = size.Width,
                TargetHeight = size.Height
            };

            DataContext = this;
            InitializeComponent();

            Buttons = new[] {
                allowNoCrop ? CreateExtraDialogButton(AppStrings.CropImage_Skip, new DelegateCommand(() => {
                    Result = (BitmapSource)_image.BitmapSource;
                    ResultRect = new Int32Rect(0, 0, _image.Width, _image.Height);
                    ResultRectRelative = new Rect(0d, 0d, 1d, 1d);
                    Close();
                }, () => !_image.IsBroken)) : null,
                CreateCloseDialogButton(UiStrings.Ok, true, false, MessageBoxResult.OK, new DelegateCommand(() => {
                    var rect = new Int32Rect((int)_model.OffsetX, (int)_model.OffsetY, (int)_model.ImageWidth, (int)_model.ImageHeight);

                    ResultRect = rect;
                    ResultRectRelative = new Rect(
                            (double)rect.X / _image.Width, (double)rect.Y / _image.Height,
                            (double)rect.Width / _image.Width, (double)rect.Height / _image.Height);

                    var cropped = new CroppedBitmap((BitmapSource)_image.BitmapSource, rect);
                    Result = cropped.Resize((int)_model.TargetWidth, (int)_model.TargetHeight);
                }, () => !_image.IsBroken)),
                CancelButton
            };

            LoadImage(filename, startRect).Forget();
        }

        private async Task LoadImage(string filename, Rect? startRect) {
            _image = await BetterImage.LoadBitmapSourceAsync(filename);
            OriginalImage.SetCurrentValue(Image.SourceProperty, _image.BitmapSource);
            CommandManager.InvalidateRequerySuggested();

            if (_image.IsBroken) {
                NonfatalError.Notify("Can’t load image");
                CloseWithResult(MessageBoxResult.Cancel);
            } else {
                _model.TotalWidth = MainGrid.Width = _image.Width;
                _model.TotalHeight = MainGrid.Height = _image.Height;
                _model.CalculateMinAndMax();

                if (startRect.HasValue) {
                    var rect = startRect.Value;
                    _model.OffsetX = rect.X;
                    _model.OffsetY = rect.Y;
                    _model.ImageWidth = rect.Width;
                    _model.ImageHeight = rect.Height;
                } else {
                    _model.CenterAndFit();
                }

                UpdateView();
            }
        }

        public void UpdateView() {
            _model.Check();

            SelectedArea.Width = _model.ImageWidth;
            SelectedArea.Height = _model.ImageHeight;
            SelectedArea.Margin = new Thickness(_model.OffsetX, _model.OffsetY, 0, 0);
        }

        private EditorState _state = EditorState.Idle;
        private Point _previousPoint;

        private enum EditorState {
            Idle,
            Moving
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e) {
            _state = EditorState.Moving;
            _previousPoint = e.GetPosition(this);
        }

        private void OnMouseMove(object sender, MouseEventArgs e) {
            var pos = e.GetPosition(this);
            var dx = pos.X - _previousPoint.X;
            var dy = pos.Y - _previousPoint.Y;
            _previousPoint = pos;

            switch (_state) {
                case EditorState.Moving:
                    var scale = Viewbox.Scale;
                    _model.OffsetX += dx / scale.Width;
                    _model.OffsetY += dy / scale.Height;
                    UpdateView();
                    break;
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e) {
            _state = EditorState.Idle;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e) {
            _state = EditorState.Idle;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            _model.Scale(e.Delta > 0 ? 1.1 : 0.91);
            UpdateView();
        }
    }
}
