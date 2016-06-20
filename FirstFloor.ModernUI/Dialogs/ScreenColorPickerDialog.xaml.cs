using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Color = System.Windows.Media.Color;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace FirstFloor.ModernUI.Dialogs {
    public partial class ScreenColorPickerDialog : INotifyPropertyChanged {
        private static BitmapSource CopyScreen() {
            using (var bmp = new Bitmap((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, PixelFormat.Format32bppArgb)) {
                using (var graphics = Graphics.FromImage(bmp)) {
                    graphics.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                    return Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
            }
        }

        private readonly BitmapSource _screenSource;

        private Color _color;

        public Color Color {
            get { return _color; }
            set {
                if (value.Equals(_color)) return;
                _color = value;
                OnPropertyChanged();
            }
        }

        public Color? ResultColor { get; private set; }

        public ScreenColorPickerDialog() {
            DataContext = this;
            InitializeComponent();
            _screenSource = CopyScreen();
            CapturedImage.Source = _screenSource;
        }

        private static bool _hasBeenWarned;

        public Color GetPixelColor(int x, int y) {
            Color color;
            var bytesPerPixel = (_screenSource.Format.BitsPerPixel + 7) / 8;
            var bytes = new byte[bytesPerPixel];
            var rect = new Int32Rect(x, y, 1, 1);

            _screenSource.CopyPixels(rect, bytes, bytesPerPixel, 0);
            
            if (_screenSource.Format == PixelFormats.Pbgra32) {
                color = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
            } else if (_screenSource.Format == PixelFormats.Bgr32) {
                color = Color.FromArgb(0xFF, bytes[2], bytes[1], bytes[0]);
            } else if (_screenSource.Format == PixelFormats.Bgra32) {
                color = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
            } else {
                if (!_hasBeenWarned) {
                    Logging.Warning("[ScreenColorPickerDialog] Unsupported format: " + _screenSource.Format);
                    _hasBeenWarned = true;
                }

                color = Colors.Black;
            }

            return color;
        }

        public static Color? Pick() {
            var dialog = new ScreenColorPickerDialog();
            dialog.ShowDialog();
            return dialog.ResultColor;
        }

        private void OnMouseMove(object sender, MouseEventArgs e) {
            if (!FloatingTip.IsOpen) {
                FloatingTip.IsOpen = true;
            }
            
            var position = Mouse.GetPosition(this);
            FloatingTip.HorizontalOffset = position.X + 20;
            FloatingTip.VerticalOffset = position.Y + 20;

            Color = GetPixelColor((int)position.X, (int)position.Y);
        }

        private void OnMouseLeave(object sender, MouseEventArgs e) {
            FloatingTip.IsOpen = false;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            ResultColor = Color;
            Close();
        }

        private void OnKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.BrowserBack ||
                    e.Key == Key.Q || e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                Close();
            } else if (e.Key == Key.Enter) {
                ResultColor = Color;
                Close();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
