using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.Dialogs {
    public partial class AppIconEditor {
        private ViewModel Model => (ViewModel)DataContext;

        private AppIconEditor([NotNull] IEnumerable<PythonAppWindow> windows) {
            DataContext = new ViewModel(windows);
            InitializeComponent();
            Buttons = new[] {
                OkButton,
                CancelButton
            };

            Closing += OnClosing;
        }

        public static async Task RunAsync([NotNull] PythonAppObject app) {
            IReadOnlyList<PythonAppWindow> list;
            using (WaitingDialog.Create("Searching for app windows…")) {
                list = await app.Windows.GetValueAsync();
            }

            if (list == null || list.Count == 0) {
                ShowMessage(
                        "No app windows found. You can add lines like “# app window: Window Name” to your code to help CM figure out their names.",
                        "No app windows found", MessageBoxButton.OK);
                return;
            }

            await new AppIconEditor(list).ShowDialogAsync();
        }

        public static async Task RunAsync([NotNull] IEnumerable<PythonAppWindow> windows) {
            await new AppIconEditor(windows).ShowDialogAsync();
        }

        public static async Task RunAsync([NotNull] params PythonAppWindow[] windows) {
            await new AppIconEditor(windows).ShowDialogAsync();
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            if (!IsResultOk) return;

            foreach (var item in IconsList.FindVisualChildren<FrameworkElement>().Where(x => x.Name == @"NewIcon")) {
                if (!(item.DataContext is AppWindowItem data) || !data.IsInEditMode) continue;

                item.DataContext = data;
                data.Save();
                var size = new Size(CommonAcConsts.AppIconWidth, CommonAcConsts.AppIconHeight);
                var result = new ContentPresenter { Width = size.Width, Height = size.Height, Content = item };

                SaveIcon();
                data.ShowEnabled = !data.ShowEnabled;
                SaveIcon();

                void SaveIcon() {
                    result.Measure(size);
                    result.Arrange(new Rect(size));
                    result.ApplyTemplate();
                    result.UpdateLayout();

                    var bmp = new RenderTargetBitmap(CommonAcConsts.AppIconWidth, CommonAcConsts.AppIconHeight, 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(result);
                    bmp.SaveTo(data.IconOriginal);
                    BetterImage.Refresh(data.IconOriginal);
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class AppWindowItem : NotifyPropertyChanged {
            private readonly string _key;
            public PythonAppWindow Window { get; }

            [JsonProperty("defaultLabelText")]
            private readonly string _defaultLabelText;

            public AppWindowItem(PythonAppWindow window) {
                _key = "AppIconEditor.Stored:" + window.DisplayName;

                Window = window;
                IsInEditMode = !File.Exists(window.IconOff);
                FontFamily = new FontFamily("Segoe UI");
                LabelText = _defaultLabelText = window.DisplayName.Where((x, i) => i == 0 || char.IsWhiteSpace(window.DisplayName[i - 1]))
                                                      .Take(3).JoinToString().ToUpper();

                if (ValuesStorage.Contains(_key)) {
                    try {
                        JsonConvert.PopulateObject(ValuesStorage.Get<string>(_key), this);
                    } catch (Exception e) {
                        NonfatalError.NotifyBackground("Can’t load previous values", e);
                    }
                }
            }

            public void Save() {
                ValuesStorage.Set(_key, JsonConvert.SerializeObject(this));
            }

            private bool _movementMode;

            public bool MovementMode {
                get => _movementMode;
                set => Apply(value, ref _movementMode);
            }

            private Point _iconPosition = new Point(12, 12);

            [JsonProperty("iconPosition")]
            public Point IconPosition {
                get => _iconPosition;
                set => Apply(value, ref _iconPosition);
            }

            private string _iconFilename;

            [JsonProperty("iconFilename")]
            public string IconFilename {
                get => _iconFilename;
                set {
                    if (Equals(value, _iconFilename)) return;
                    _iconFilename = value;
                    OnPropertyChanged();
                    _removeIconCommand?.RaiseCanExecuteChanged();
                    if (LabelText == _defaultLabelText) {
                        LabelText = "";
                    }
                }
            }

            private double _iconScale = 1d;

            [JsonProperty("iconScale")]
            public double IconScale {
                get => _iconScale;
                set => Apply(value, ref _iconScale);
            }

            private bool _iconShadow;

            [JsonProperty("iconShadow")]
            public bool IconShadow {
                get => _iconShadow;
                set => Apply(value, ref _iconShadow);
            }

            private string _labelText;

            [JsonProperty("labelText")]
            public string LabelText {
                get => _labelText;
                set {
                    if (string.IsNullOrWhiteSpace(value)) value = null;
                    if (Equals(value, _labelText)) return;
                    _labelText = value;
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _selectIconCommand;

            public DelegateCommand SelectIconCommand => _selectIconCommand ?? (_selectIconCommand = new DelegateCommand(() => {
                IconFilename = FileRelatedDialogs.Open(new OpenDialogParams {
                    DirectorySaveKey = "AppIconEditor.Icon",
                    Filters = { DialogFilterPiece.PngFiles, DialogFilterPiece.ImageFiles },
                    DetaultExtension = DialogFilterPiece.PngFiles.BaseExtension?.ToLower()
                });
            }));

            private DelegateCommand _resetIconTransformCommand;

            public DelegateCommand ResetIconTransformCommand => _resetIconTransformCommand ?? (_resetIconTransformCommand = new DelegateCommand(() => {
                IconScale = 1d;
                IconPosition = new Point(8, 8);
            }));

            private DelegateCommand _removeIconCommand;

            public DelegateCommand RemoveIconCommand => _removeIconCommand ?? (_removeIconCommand = new DelegateCommand(() => {
                IconFilename = null;
            }, () => IconFilename != null));

            private FontFamily _fontFamily;

            [JsonProperty("fontFamily")]
            public FontFamily FontFamily {
                get => _fontFamily;
                set {
                    if (Equals(value, _fontFamily)) return;
                    _fontFamily = value;
                    OnPropertyChanged();
                    AvailableFontWeights = value.FamilyTypefaces.Select(x => x.Weight).Distinct().ToList();
                    AvailableFontStyles = value.FamilyTypefaces.Select(x => x.Style).Distinct().ToList();
                    AvailableFontStretchs = value.FamilyTypefaces.Select(x => x.Stretch).Distinct().ToList();
                }
            }

            private IReadOnlyCollection<FontWeight> _availableFontWeights;

            public IReadOnlyCollection<FontWeight> AvailableFontWeights {
                get => _availableFontWeights;
                set {
                    if (Equals(value, _availableFontWeights)) return;
                    _availableFontWeights = value;
                    OnPropertyChanged();
                    FontWeight = value.Contains(FontWeight) ? FontWeight : value.FirstOrDefault();
                }
            }

            private IReadOnlyCollection<FontStyle> _availableFontStyles;

            public IReadOnlyCollection<FontStyle> AvailableFontStyles {
                get => _availableFontStyles;
                set {
                    if (Equals(value, _availableFontStyles)) return;
                    _availableFontStyles = value;
                    OnPropertyChanged();
                    FontStyle = value.Contains(FontStyle) ? FontStyle : value.FirstOrDefault();
                }
            }

            private IReadOnlyCollection<FontStretch> _availableFontStretchs;

            public IReadOnlyCollection<FontStretch> AvailableFontStretchs {
                get => _availableFontStretchs;
                set {
                    if (Equals(value, _availableFontStretchs)) return;
                    _availableFontStretchs = value;
                    OnPropertyChanged();
                    FontStretch = value.Contains(FontStretch) ? FontStretch : value.FirstOrDefault();
                }
            }

            private FontWeight _fontWeight = FontWeights.Bold;

            [JsonProperty("fontWeight")]
            public FontWeight FontWeight {
                get => _fontWeight;
                set => Apply(value, ref _fontWeight);
            }

            private FontStyle _fontStyle = FontStyles.Normal;

            [JsonProperty("fontStyle")]
            public FontStyle FontStyle {
                get => _fontStyle;
                set => Apply(value, ref _fontStyle);
            }

            private FontStretch _fontStretch = FontStretches.Normal;

            [JsonProperty("fontStretch")]
            public FontStretch FontStretch {
                get => _fontStretch;
                set => Apply(value, ref _fontStretch);
            }

            private double _fontSize = 25d;

            [JsonProperty("fontSize")]
            public double FontSize {
                get => _fontSize;
                set => Apply(value, ref _fontSize);
            }

            private Color _textColor = Colors.White;

            [JsonProperty("textColor")]
            public Color TextColor {
                get => _textColor;
                set => Apply(value, ref _textColor);
            }

            private Point _textPosition = new Point(0, 12);

            [JsonProperty("textPosition")]
            public Point TextPosition {
                get => _textPosition;
                set => Apply(value, ref _textPosition);
            }

            private bool _textShadow;

            [JsonProperty("textShadow")]
            public bool TextShadow {
                get => _textShadow;
                set => Apply(value, ref _textShadow);
            }

            private bool _isInEditMode;

            public bool IsInEditMode {
                get => _isInEditMode;
                set => Apply(value, ref _isInEditMode);
            }

            private string _iconOriginal;

            public string IconOriginal {
                get => _iconOriginal;
                set => Apply(value, ref _iconOriginal);
            }

            private bool? _showEnabled;

            public bool ShowEnabled {
                get => _showEnabled ?? false;
                set {
                    if (Equals(value, _showEnabled)) return;
                    _showEnabled = value;
                    OnPropertyChanged();
                    IconOriginal = value ? Window.IconOn : Window.IconOff;
                }
            }

            public void CopyFrom(AppWindowItem item) {
                if (IconFilename == null) IconFilename = item.IconFilename;
                IconPosition = item.IconPosition;
                IconScale = item.IconScale;
                IconShadow = item.IconShadow;

                if (LabelText == null) LabelText = item.LabelText;
                FontFamily = item.FontFamily;
                FontWeight = item.FontWeight;
                FontStyle = item.FontStyle;
                FontStretch = item.FontStretch;
                FontSize = item.FontSize;
                TextShadow = item.TextShadow;
                TextColor = item.TextColor;
                TextPosition = item.TextPosition;
            }
        }

        private class ViewModel : NotifyPropertyChanged {
            public List<AppWindowItem> Windows { get; }

            public StoredValue<bool> ShowEnabled { get; } = Stored.Get("AppIconEditor.ShowEnabled", false);
            public StoredValue<bool> InverseBackground { get; } = Stored.Get("AppIconEditor.InverseBackground", false);
            public StoredValue<bool> MovementMode { get; } = Stored.Get("AppIconEditor.MovementMode", false);

            public ViewModel([NotNull] IEnumerable<PythonAppWindow> windows) {
                ShowEnabled.SubscribeWeak((s, e) => UpdateWindowsShowEnabled());
                MovementMode.SubscribeWeak((s, e) => UpdateMovementMode());

                Windows = windows.Select(x => new AppWindowItem(x)).ToList();
                UpdateWindowsShowEnabled();
                UpdateMovementMode();
            }

            private void UpdateWindowsShowEnabled() {
                Windows.ForEach(x => x.ShowEnabled = ShowEnabled.Value);
            }

            private void UpdateMovementMode() {
                Windows.ForEach(x => x.MovementMode = MovementMode.Value);
            }

            public void Apply() {}
        }

        private void OnLabelThumbDragDelta(object sender, DragDeltaEventArgs e) {
            var fe = (FrameworkElement)sender;
            if (fe.IsManipulationEnabled) {
                var item = (AppWindowItem)fe.DataContext;
                var multiplier = Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == ModifierKeys.Shift ? 0.1 : 1;
                item.TextPosition = new Point(
                        item.TextPosition.X + e.HorizontalChange * multiplier,
                        item.TextPosition.Y + e.VerticalChange * multiplier);
                e.Handled = true;
            }
        }

        private void OnLabelThumbMouseWheel(object sender, MouseWheelEventArgs e) {
            var fe = (FrameworkElement)sender;
            if (fe.IsManipulationEnabled) {
                var item = (AppWindowItem)fe.DataContext;
                item.FontSize = (item.FontSize + e.Delta.Sign()).Clamp(5, 45);
                e.Handled = true;
            }
        }

        private void OnIconThumbDragDelta(object sender, DragDeltaEventArgs e) {
            var fe = (FrameworkElement)sender;
            if (fe.IsManipulationEnabled) {
                var item = (AppWindowItem)fe.DataContext;
                var multiplier = Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == ModifierKeys.Shift ? 0.1 : 1;
                item.IconPosition = new Point(
                        item.IconPosition.X + e.HorizontalChange * multiplier,
                        item.IconPosition.Y + e.VerticalChange * multiplier);
                e.Handled = true;
            }
        }

        private void OnIconThumbMouseWheel(object sender, MouseWheelEventArgs e) {
            var fe = (FrameworkElement)sender;
            if (fe.IsManipulationEnabled) {
                var item = (AppWindowItem)fe.DataContext;
                item.IconScale = (item.FontSize + e.Delta.Sign() * 0.1).Clamp(0.2, 2);
                e.Handled = true;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            foreach (var thumb in this.FindVisualChildren<Thumb>()) {
                switch (thumb.Tag as string) {
                    case "LabelThumb":
                        thumb.DragDelta += OnLabelThumbDragDelta;
                        thumb.MouseWheel += OnLabelThumbMouseWheel;
                        break;
                    case "IconThumb":
                        thumb.DragDelta += OnIconThumbDragDelta;
                        thumb.MouseWheel += OnIconThumbMouseWheel;
                        break;
                }
            }

            foreach (var canvas in this.FindVisualChildren<DockPanel>().Where(x => x.Tag as string == "Item")) {
                canvas.Drop += OnDrop;
            }
        }

        private void OnDrop(object sender, DragEventArgs e) {
            var fe = (FrameworkElement)sender;
            var item = (AppWindowItem)fe.DataContext;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var file = ((string[])e.Data.GetData(DataFormats.FileDrop))?.FirstOrDefault();
            if (file != null) {
                item.IconFilename = file;
            }
        }

        private void OnCopyStylesButtonClick(object sender, RoutedEventArgs e) {
            var fe = (FrameworkElement)sender;
            var item = (AppWindowItem)fe.DataContext;

            foreach (var windowItem in Model.Windows.ApartFrom(item)) {
                windowItem.CopyFrom(item);
            }
        }
    }
}
