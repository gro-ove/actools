using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
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

            public void AdjustImageWidth() {
                SetImageWidth(ImageHeight * TargetWidth / TargetHeight);
            }

            public void AdjustImageHeight() {
                SetImageHeight(ImageWidth * TargetHeight / TargetWidth);
            }

            public void Scale(double d) {
                SetImageWidth(ImageWidth * d);
                SetImageHeight(ImageHeight * d);
            }

            public void Move(double dx, double dy) {
                OffsetX += dx;
                OffsetY += dy;
            }

            public void MovePercentage(double dx, double dy) {
                var s = Math.Min(ImageWidth, MaxHeight);
                OffsetX += s * dx * 0.01;
                OffsetY += s * dy * 0.01;
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
            UpdateControlsToScale();
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

        private double GetScaleMultiplier() {
            if (Keyboard.Modifiers == ModifierKeys.Control) return 1.25;
            if (Keyboard.Modifiers == ModifierKeys.Shift) return 1.025;
            return 1.1;
        }

        private double GetMultiplier() {
            if (Keyboard.Modifiers == ModifierKeys.Control) return 4;
            if (Keyboard.Modifiers == ModifierKeys.Shift) return 0.25;
            return 1;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            _model.Scale(e.Delta > 0 ? GetScaleMultiplier() : 1d / GetScaleMultiplier());
            UpdateView();
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.OemMinus:
                case Key.Subtract:
                    _model.Scale(1d / GetScaleMultiplier());
                    break;
                case Key.OemPlus:
                case Key.Add:
                    _model.Scale(GetScaleMultiplier());
                    break;
                case Key.Left:
                case Key.NumPad4:
                    _model.MovePercentage(-GetMultiplier(), 0d);
                    break;
                case Key.Right:
                case Key.NumPad6:
                    _model.MovePercentage(GetMultiplier(), 0d);
                    break;
                case Key.Up:
                case Key.NumPad8:
                    _model.MovePercentage(0d, -GetMultiplier());
                    break;
                case Key.NumPad7:
                    _model.MovePercentage(-GetMultiplier(), -GetMultiplier());
                    break;
                case Key.NumPad9:
                    _model.MovePercentage(GetMultiplier(), -GetMultiplier());
                    break;
                case Key.Down:
                case Key.NumPad2:
                    _model.MovePercentage(0d, GetMultiplier());
                    break;
                case Key.NumPad1:
                    _model.MovePercentage(-GetMultiplier(), GetMultiplier());
                    break;
                case Key.NumPad3:
                    _model.MovePercentage(GetMultiplier(), GetMultiplier());
                    break;
                default:
                    return;
            }

            UpdateView();
            e.Handled = true;
        }

        private void UpdateControlsToScale() {
            var scale = Math.Max(Viewbox.Scale.Height, 0.01);
            CutBorder.BorderThickness = new Thickness(1d / scale);
            CutBorder.Margin = new Thickness(-1d / scale);

            var size = 8d / scale;
            var margin = -size / 2d;
            ResizeTop.Height = size;
            ResizeTop.Margin = new Thickness(0d, margin, 0d, 0d);
            ResizeBottom.Height = size;
            ResizeBottom.Margin = new Thickness(0d, 0d, 0d, margin);
            ResizeLeft.Width = size;
            ResizeLeft.Margin = new Thickness(margin, 0d, 0d, 0d);
            ResizeRight.Width = size;
            ResizeRight.Margin = new Thickness(0d, 0d, margin, 0d);

            ResizeTopLeft.Width = ResizeTopLeft.Height = size;
            ResizeTopLeft.Margin = new Thickness(margin, margin, 0d, 0d);
            ResizeTopRight.Width = ResizeTopRight.Height = size;
            ResizeTopRight.Margin = new Thickness(0d, margin, margin, 0d);
            ResizeBottomLeft.Width = ResizeBottomLeft.Height = size;
            ResizeBottomLeft.Margin = new Thickness(margin, 0d, 0d, margin);
            ResizeBottomRight.Width = ResizeBottomRight.Height = size;
            ResizeBottomRight.Margin = new Thickness(0d, 0d, margin, margin);
        }

        private void OnViewboxSizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateControlsToScale();
        }

        private void OnResizeTopDelta(object sender, DragDeltaEventArgs e) {
            var dy = e.VerticalChange * Viewbox.Scale.Height;
            _model.ImageHeight -= dy;
            _model.OffsetY += dy;
            _model.AdjustImageWidth();
            UpdateView();
            e.Handled = true;
        }

        private void OnResizeBottomDelta(object sender, DragDeltaEventArgs e) {
            var dy = e.VerticalChange * Viewbox.Scale.Height;
            _model.ImageHeight += dy;
            _model.AdjustImageWidth();
            UpdateView();
            e.Handled = true;
        }

        private void OnResizeLeftDelta(object sender, DragDeltaEventArgs e) {
            var dx = e.HorizontalChange * Viewbox.Scale.Width;
            _model.ImageWidth -= dx;
            _model.OffsetX += dx;
            _model.AdjustImageHeight();
            UpdateView();
            e.Handled = true;
        }

        private void OnResizeRightDelta(object sender, DragDeltaEventArgs e) {
            var dx = e.HorizontalChange * Viewbox.Scale.Width;
            _model.ImageWidth += dx;
            _model.AdjustImageHeight();
            UpdateView();
            e.Handled = true;
        }

        private void GetCornerD(DragDeltaEventArgs e, out double dx, out double dy, double invert) {
            var multiplier = GetScaleMultiplier();
            dx = e.HorizontalChange * Viewbox.Scale.Width * multiplier;
            dy = e.VerticalChange * Viewbox.Scale.Height * multiplier;
            if (dx.Abs() > dy.Abs()) {
                dx = invert.Sign() * dy * _model.TargetWidth / _model.TargetHeight;
            } else {
                dy = invert.Sign() * dx * _model.TargetHeight / _model.TargetWidth;
            }
        }

        private void OnResizeTopLeftDelta(object sender, DragDeltaEventArgs e) {
            GetCornerD(e, out var dx, out var dy, 1d);
            _model.ImageHeight -= dy;
            _model.OffsetY += dy;
            _model.ImageWidth -= dx;
            _model.OffsetX += dx;
            UpdateView();
            e.Handled = true;
        }

        private void OnResizeTopRightDelta(object sender, DragDeltaEventArgs e) {
            GetCornerD(e, out var dx, out var dy, -1d);
            _model.ImageHeight -= dy;
            _model.OffsetY += dy;
            _model.ImageWidth += dx;
            UpdateView();
            e.Handled = true;
        }

        private void OnResizeBottomLeftDelta(object sender, DragDeltaEventArgs e) {
            GetCornerD(e, out var dx, out var dy, -1d);
            _model.ImageHeight += dy;
            _model.ImageWidth -= dx;
            _model.OffsetX += dx;
            UpdateView();
            e.Handled = true;
        }

        private void OnResizeBottomRightDelta(object sender, DragDeltaEventArgs e) {
            GetCornerD(e, out var dx, out var dy, 1d);
            _model.ImageHeight += dy;
            _model.ImageWidth += dx;
            UpdateView();
            e.Handled = true;
        }
    }
}
