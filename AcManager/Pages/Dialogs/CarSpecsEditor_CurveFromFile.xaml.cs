using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Dialogs {
    public partial class CarSpecsEditor_CurveFromFile : ModernDialog {
        public double LeftValue { get; private set; }
        public double TopValue { get; private set; }
        public double RightValue { get; private set; }
        public double BottomValue { get; private set; }


        public class CropModel {
            public double TotalWidth,
                          TotalHeight,
                          EdgeLeft,
                          EdgeTop,
                          EdgeRight,
                          EdgeBottom;

            public void Check() {
                if (EdgeLeft < 0) EdgeLeft = 0;
                if (EdgeTop < 0) EdgeTop = 0;
                if (EdgeRight < 0) EdgeRight = 0;
                if (EdgeBottom < 0) EdgeBottom = 0;

                if (EdgeLeft > TotalWidth) EdgeLeft = TotalWidth;
                if (EdgeRight > TotalWidth) EdgeRight = TotalWidth;
                if (EdgeTop > TotalHeight) EdgeTop = TotalHeight;
                if (EdgeBottom > TotalHeight) EdgeBottom = TotalHeight;
            }
        }

        private readonly BitmapImage _image;
        private readonly CropModel _model;

        public CarSpecsEditor_CurveFromFile(string filename) {
            _model = new CropModel();

            InitializeComponent();
            DataContext = this;

            Buttons = new[] { OkButton, CancelButton };

            LeftValue = BottomValue = 0.0;
            RightValue = 5000.0;
            TopValue = 200.0;

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
                //var rect = new Int32Rect((int) _model.OffsetX, (int) _model.OffsetY, (int) _model.ImageWidth, (int) _model.ImageHeight);
                //var cropped = new CroppedBitmap(_image, rect);
                //Result = cropped.Resize((int) _model.TargetWidth, (int) _model.TargetHeight);
            };
        }

        void Image_Loaded(object sender, EventArgs e) {
            _model.TotalWidth = MainGrid.Width = _image.PixelWidth;
            _model.TotalHeight = MainGrid.Height = _image.PixelHeight;
            //_model.CalculateMinAndMax();
            //_model.CenterAndFit();
            UpdateView();
        }

        public void UpdateView() {
            _model.Check();

            LeftBorder.Margin = new Thickness(0, _model.EdgeLeft, 0, 0);
            TopBorder.Margin = new Thickness(0, _model.EdgeTop, 0, 0);
            RightBorder.Margin = new Thickness(0, _model.EdgeRight, 0, 0);
            BottomBorder.Margin = new Thickness(0, _model.EdgeBottom, 0, 0);

           /* SelectedArea.Width = _model.ImageWidth;
            SelectedArea.Height = _model.ImageHeight;
            SelectedArea.Margin = new Thickness(_model.OffsetX, _model.OffsetY, 0, 0);*/
        }

        private CurveEditorState _state = CurveEditorState.Idle;
        private Point _previousPoint;

        private enum CurveEditorState {
            Idle,
            Moving
        }

        private void CurveEditor_OnMouseDown(object sender, MouseButtonEventArgs e) {
            _state = CurveEditorState.Moving;
            _previousPoint = e.GetPosition(this);
        }

        private void CurveEditor_OnMouseMove(object sender, MouseEventArgs e) {
            var pos = e.GetPosition(this);
            var dx = pos.X - _previousPoint.X;
            var dy = pos.Y - _previousPoint.Y;
            _previousPoint = pos;

            switch (_state) {
                case CurveEditorState.Moving:
                    _model.EdgeLeft += dx;
                    _model.EdgeTop += dy;
                    _model.EdgeRight -= dx;
                    _model.EdgeBottom -= dy;
                    UpdateView();
                    break;
            }
        }

        private void CurveEditor_OnMouseUp(object sender, MouseButtonEventArgs e) {
            _state = CurveEditorState.Idle;
        }

        private void CurveEditor_OnMouseLeave(object sender, MouseEventArgs e) {
            _state = CurveEditorState.Idle;
        }

    }
}
