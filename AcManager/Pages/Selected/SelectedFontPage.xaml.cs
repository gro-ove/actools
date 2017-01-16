using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Dialogs;
using AcManager.Tools;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Selected {
    public partial class SelectedFontPage : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public class ViewModel : SelectedAcObjectViewModel<FontObject> {
            public ViewModel([NotNull] FontObject acObject) : base(acObject) { }

            public FontsManager Manager => FontsManager.Instance;

            private CommandBase _usingsRescanCommand;

            public ICommand UsingsRescanCommand => _usingsRescanCommand ?? (_usingsRescanCommand = new AsyncCommand(async () => {
                List<string> missing;
                using (var waiting = new WaitingDialog()) {
                    missing = await FontsManager.Instance.UsingsRescan(waiting, waiting.CancellationToken);
                }

                if (missing?.Any() == true) {
                    ModernDialog.ShowMessage(missing.JoinToString(@", "), AppStrings.Font_MissingFonts, MessageBoxButton.OK);
                }
            }));

            private CommandBase _disableUnusedCommand;

            public ICommand DisableUnusedCommand => _disableUnusedCommand ?? (_disableUnusedCommand = new AsyncCommand(async () => {
                using (var waiting = new WaitingDialog(ToolsStrings.Common_Scanning)) {
                    await FontsManager.Instance.UsingsRescan(waiting, waiting.CancellationToken);
                    if (waiting.CancellationToken.IsCancellationRequested) return;

                    waiting.Title = null;
                    var toDisable = FontsManager.Instance.LoadedOnly.Where(x => x.Enabled && x.UsingsCarsIds.Length == 0).ToList();
                    foreach (var font in toDisable) {
                        waiting.Report(string.Format(AppStrings.Common_DisablingFormat, font.DisplayName));
                        font.ToggleCommand.Execute(null);
                        await Task.Delay(500, waiting.CancellationToken);
                        if (waiting.CancellationToken.IsCancellationRequested) break;
                    }
                }
            }));

            private CommandBase _createNewFontCommand;

            public ICommand CreateNewFontCommand => _createNewFontCommand ?? (_createNewFontCommand = new DelegateCommand(() => {
                Process.Start(FontCreationTool);
            }, () => File.Exists(FontCreationTool)));

            public string FontCreationTool => Path.Combine(AcRootDirectory.Instance.RequireValue, @"sdk", @"dev", @"ksFontGenerator", @"ksFontGenerator.exe");
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private FontObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await FontsManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = FontsManager.Instance.GetById(_id);
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            SetModel();
            InitializeComponent();
        }

        bool IImmediateContent.ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = FontsManager.Instance.GetById(id);
            if (obj == null) return false;

            _id = id;
            _object = obj;
            SetModel();
            RedrawTestText();
            return true;
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(Model.CreateNewFontCommand, new KeyGesture(Key.N, ModifierKeys.Control)),
                new InputBinding(Model.UsingsRescanCommand, new KeyGesture(Key.U, ModifierKeys.Control)),
                new InputBinding(Model.DisableUnusedCommand, new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift))
            });
        }

        private const string KeyTestText = "SelectedFontPage.TestText";

        private ViewModel Model => (ViewModel)DataContext;

        private void TestTextBox_OnTextChanged(object sender, TextChangedEventArgs e) {
            ValuesStorage.Set(KeyTestText, TextBox.Text);
            RedrawTestText();
        }

        private void SelectedFontPage_OnLoaded(object sender, RoutedEventArgs e) {
            TextBox.Text = ValuesStorage.GetString(KeyTestText, @"0123456789 Test");
        }

        private void RedrawTestText() {
            if (Canvas == null || TextBox == null) return;
            Canvas.Children.Clear();

            var text = TextBox.Text;
            if (string.IsNullOrEmpty(text)) return;

            try {
                Canvas.Children.Add(new FontTestHost(text, Model.SelectedObject));
            } catch (Exception e) {
                Logging.Warning("Can’t update testing text: " + e);
            }
        }

        private void AcObjectBase_OnIconMouseDown(object sender, MouseButtonEventArgs e) {
            new ImageViewer(Model.SelectedObject.FontBitmap).ShowDialog();
        }

        public static IValueConverter UsingsConverter { get; } = new InnerUsingsConverter();

        private class InnerUsingsConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                var array = value as IReadOnlyList<string>;
                return array?.Select(x => CarsManager.Instance.GetById(x)?.DisplayName ?? x).JoinToString(@", ");
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }
    }

    public class FontTestVisual : DrawingVisual {
        public double Width { get; }

        public double Height { get; }

        public FontTestVisual(string text, FontObject fontObject) {
            var totalX = 0d;
            var totalY = 0d;

            using (var dc = RenderOpen()) {
                foreach (var cropped in text.Select(fontObject.BitmapForChar).Where(cropped => cropped != null)) {
                    dc.DrawImage(cropped, new Rect(totalX, 0, cropped.PixelWidth, cropped.PixelHeight));
                    totalX += cropped.PixelWidth;
                    totalY = Math.Max(totalY, cropped.PixelHeight);
                }
            }

            Width = totalX;
            Height = totalY;
        }
    }

    public class FontTestHost : FrameworkElement {
        private readonly FontTestVisual _visual;

        public FontTestHost(string text, FontObject fontObject) {
            _visual = new FontTestVisual(text, fontObject);
            Width = _visual.Width;
            Height = _visual.Height;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = new Thickness(4d);
        }
        
        protected override Visual GetVisualChild(int index) => _visual;

        protected override int VisualChildrenCount => 1;
    }
}
