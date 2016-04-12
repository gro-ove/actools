using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedFontPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedFontPageViewModel : SelectedAcObjectViewModel<FontObject> {
            public SelectedFontPageViewModel([NotNull] FontObject acObject) : base(acObject) { }
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private FontObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await FontsManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = FontsManager.Instance.GetById(_id);
        }

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can't find object with provided ID");

            InitializeAcObjectPage(new SelectedFontPageViewModel(_object));
            InitializeComponent();
        }

        private SelectedFontPageViewModel Model => (SelectedFontPageViewModel)DataContext;

        private void TestTextBox_OnTextChanged(object sender, TextChangedEventArgs e) {
            if (Canvas == null) return;
            RedrawTestText();
        }

        private void SelectedFontPage_OnLoaded(object sender, RoutedEventArgs e) {
            RedrawTestText();
        }

        private void RedrawTestText() {
            Canvas.Children.Clear();

            var text = TextBox.Text;
            var bitmap = Model.SelectedObject.FontBitmap;

            if (string.IsNullOrEmpty(text) || !File.Exists(bitmap)) return;

            try {
                var list = File.ReadAllLines(Model.SelectedObject.Location).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToList();
                Canvas.Children.Add(new FontTestHost(text, UriToCachedImageConverter.Convert(bitmap), list));
            } catch (Exception e) {
                Logging.Warning("Can't update testing text: " + e);
            }
        }
    }

    public class FontTestVisual : DrawingVisual {
        public FontTestVisual(string text, BitmapSource image, List<double> list) {
            if (list.Count == 0) return;

            var totalX = 4d;
            using (var dc = RenderOpen()) {
                foreach (var i in text.Select(c => c < 32 || c > 126 ? 0 : c - 32).Where(x => x < list.Count)) {
                    var x = list[i];
                    var width = (i + 1 == list.Count ? 1d : list[i + 1]) - x;

                    if (x + width <= 0d || x >= 1d) continue;
                    if (x < 0) {
                        width += x;
                        x = 0d;
                    }

                    width = Math.Min(width, 1d - x);

                    var rect = new Int32Rect((int)(x * image.PixelWidth), 0, (int)(width * image.PixelWidth), image.PixelHeight);
                    var cropped = new CroppedBitmap(image, rect);

                    dc.DrawImage(cropped, new Rect(totalX, 4, rect.Width, rect.Height));
                    totalX += rect.Width;
                }
            }
        }
    }

    public class FontTestHost : FrameworkElement {
        private readonly FontTestVisual _visual;

        public FontTestHost(string text, BitmapSource imageSource, List<double> list) {
            _visual = new FontTestVisual(text, imageSource, list);
        }
        
        protected override Visual GetVisualChild(int index) => _visual;

        protected override int VisualChildrenCount => 1;
    }
}
