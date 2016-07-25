using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Helpers;

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

            public void Scale(double d) {
                OffsetX -= (Math.Max(ImageWidth*d, MinWidth) - ImageWidth)/2;
                OffsetY -= (Math.Max(ImageHeight*d, MinHeight) - ImageHeight)/2;
                ImageWidth *= d;
                ImageHeight *= d;
            }

            public void Check() {
                if (ImageWidth < MinWidth) ImageWidth = MinWidth;
                if (ImageHeight < MinHeight) ImageHeight = MinHeight;

                if (ImageWidth > MaxWidth) ImageWidth = MaxWidth;
                if (ImageHeight > MaxHeight) ImageHeight = MaxHeight;

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

                MinWidth = Math.Min(TargetWidth, MaxWidth*0.75);
                MinHeight = Math.Min(TargetHeight, MaxHeight*0.75);
            }
        }

        private readonly BitmapImage _image;
        private readonly CropModel _model;

        public BitmapSource Result { get; private set; }

        public ImageEditor(string filename, Size size) {
            _model = new CropModel {
                TargetWidth = size.Width,
                TargetHeight = size.Height
            };

            DataContext = this;
            InitializeComponent();

            Buttons = new[] {
                CreateExtraDialogButton(AppStrings.CropImage_Skip, () => {
                    Result = _image;
                    Close();
                }),
                OkButton,
                CancelButton
            };

            _image = new BitmapImage();
            _image.BeginInit();
            _image.UriSource = new Uri(filename);
            _image.EndInit();

            OriginalImage.SetCurrentValue(Image.SourceProperty, _image);
            if (_image.IsDownloading) {
                _image.DownloadCompleted += Image_Loaded;
            } else {
                Image_Loaded(this, null);
            }

            Closing += (sender, args) => {
                if (MessageBoxResult != MessageBoxResult.OK) return;
                var rect = new Int32Rect((int) _model.OffsetX, (int) _model.OffsetY, (int) _model.ImageWidth, (int) _model.ImageHeight);
                var cropped = new CroppedBitmap(_image, rect);
                Result = cropped.Resize((int) _model.TargetWidth, (int) _model.TargetHeight);
            };
        }

        public static BitmapSource Proceed(string filename, Size size) {
            var dialog = new ImageEditor(filename, size);
            dialog.ShowDialog();
            return dialog.Result;
        }

        void Image_Loaded(object sender, EventArgs e) {
            _model.TotalWidth = MainGrid.Width = _image.PixelWidth;
            _model.TotalHeight = MainGrid.Height = _image.PixelHeight;
            _model.CalculateMinAndMax();
            _model.CenterAndFit();
            UpdateView();
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

        private void Editor_OnMouseDown(object sender, MouseButtonEventArgs e) {
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
                    _model.OffsetX += dx;
                    _model.OffsetY += dy;
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
            _model.Scale(e.Delta > 0 ? 1.1 : 0.9090909);
            UpdateView();
        }
    }
}
