using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools.ContentInstallation.Entries;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;

namespace AcManager.Pages.Settings {
    public partial class SettingsShadersPatch : ILocalKeyBindings {
        public static SettingsShadersPatch Instance { get; private set; }

        public static bool IsCustomShadersPatchInstalled() {
            return Directory.Exists(Path.Combine(AcRootDirectory.Instance.RequireValue, "extension", "config"));
        }

        public SettingsShadersPatch() {
            Instance = this;

            PatchHelper.Reload();
            KeyBindingsController = new LocalKeyBindingsController(this);

            InitializeComponent();
            DataContext = new ViewModel();
            Model.MainModel.PropertyChanged += OnModelPropertyChanged;
            SetKeyboardInputs();
            UpdateConfigsTabs();

            InputBindings.AddRange(new[] {
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(Model.ShareSectionCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control | ModifierKeys.Shift)),
                PresetsControl != null ? new InputBinding(PresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)) : null
            }.NonNull().ToList());

            ShadersPatchEntry.InstallationStart += OnPatchInstallationStart;
            ShadersPatchEntry.InstallationEnd += OnPatchInstallationEnd;

            if (PatchHelper.OptionPatchSupport) {
                PatchUpdater.Instance.PropertyChanged += OnPatchUpdaterPropertyChanged;
            }

            this.OnActualUnload(() => {
                Model?.Dispose();
                if (PatchHelper.OptionPatchSupport) {
                    PatchUpdater.Instance.PropertyChanged -= OnPatchUpdaterPropertyChanged;
                }
                Instance = null;
            });
        }

        private void OnPatchInstallationStart(object sender, ShadersPatchEntry.InstallationEventArgs e) {
            if (Model != null) {
                Model.MainModel.IsBlocked = true;
            }
        }

        private void OnPatchInstallationEnd(object sender, EventArgs e) {
            if (Model != null) {
                Model.MainModel.IsBlocked = false;
            }
        }

        private void OnPatchUpdaterPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(PatchUpdater.NothingAtAll)) {
                ActionExtension.InvokeInMainThreadAsync(() => UpdateContentTranslate(true));
            }
            ActionExtension.InvokeInMainThreadAsync(() => Model.MainModel.OnPatchUpdaterChanged(sender, e));
        }

        private EasingFunctionBase _selectionEasingFunction;

        private void UpdateContentTranslate(bool animated) {
            if (!PatchHelper.OptionPatchSupport) return;
            var width = PatchUpdater.Instance.NothingAtAll ? (GridSplitter.GetWidth() ?? 0d) : 0d;

            if (animated) {
                var easing = _selectionEasingFunction ?? (_selectionEasingFunction = (EasingFunctionBase)FindResource(@"StandardEase"));
                ((TranslateTransform)LinksList.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation { To = -width, Duration = new Duration(TimeSpan.FromSeconds(0.5)), EasingFunction = easing });
                ((TranslateTransform)GridSplitter.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation { To = -width, Duration = new Duration(TimeSpan.FromSeconds(0.5)), EasingFunction = easing });
                ((TranslateTransform)ContentCell.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation { To = -width / 3.5, Duration = new Duration(TimeSpan.FromSeconds(0.5)), EasingFunction = easing });
            } else {
                ((TranslateTransform)LinksList.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
                ((TranslateTransform)LinksList.RenderTransform).X = -width;
                ((TranslateTransform)GridSplitter.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
                ((TranslateTransform)GridSplitter.RenderTransform).X = -width;
                ((TranslateTransform)ContentCell.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
                ((TranslateTransform)ContentCell.RenderTransform).X = -width / 3.5;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            UpdateContentTranslate(false);
        }

        private void OnSplitterMoved(object sender, ModernTabSplitter.MovedEventArgs e) {
            UpdateContentTranslate(false);
        }

        private void SetKeyboardInputs() {
            KeyBindingsController.Set(Model.MainModel.SelectedPage?.Config?.SectionsOwn.SelectMany().OfType<PythonAppConfigKeyValue>());
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Model.MainModel.SelectedPage)) {
                SetKeyboardInputs();
                UpdateConfigsTabs();
            }
        }

        private void UpdateConfigsTabs() {
            try {
                PageFrame.Source = Model.MainModel.SelectedPage?.Config == null ? (Model.MainModel.SelectedPage?.Source ?? PageFrame.Source) : null;
                ConfigTab.Content = Model.MainModel.SelectedPage?.Config;
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public enum Mode {
            NoShadersPatch,
            NoConfigs,
            EverythingIsFine
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            public PatchSettingsModel MainModel { get; }

            public ViewModel() {
                MainModel = PatchSettingsModel.Create().SetupWatcher().SetupIssues();
            }

            public void Dispose() {
                MainModel?.Dispose();
            }

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(async () => {
                try {
                    var data = MainModel.ExportToPresetData();
                    if (data == null) return;
                    await SharingUiHelper.ShareAsync(SharedEntryType.CspSettings,
                            Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(MainModel.PresetableKey)), null, data);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t share preset", e);
                }
            }));

            private AsyncCommand _shareSectionCommand;

            public AsyncCommand ShareSectionCommand => _shareSectionCommand ?? (_shareSectionCommand = new AsyncCommand(async () => {
                try {
                    var data = MainModel.ExportToPresetData(new[]{ MainModel.SelectedPage?.Config }.NonNull());
                    if (data == null) return;
                    await SharingUiHelper.ShareAsync(SharedEntryType.CspSettings,
                            Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(MainModel.PresetableKey)),
                            MainModel.SelectedPage?.DisplayName, data);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t share preset", e);
                }
            }));
        }

        public LocalKeyBindingsController KeyBindingsController { get; }

        public static Action CloseOpenedSettings { get; set; }

        public static ICommand GetShowSettingsCommand() {
            return new AsyncCommand(async () => {
                var dlg = new ModernDialog {
                    ShowTitle = false,
                    Content = new SettingsShadersPatchPopup(),
                    MinHeight = 400,
                    MinWidth = 600,
                    MaxHeight = 99999,
                    MaxWidth = 1200,
                    Padding = new Thickness(0),
                    ButtonsMargin = new Thickness(0),
                    SizeToContent = SizeToContent.Manual,
                    ResizeMode = ResizeMode.CanResizeWithGrip,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    BlurBackground = true,
                    ShowTopBlob = false,
                    Topmost = true,
                    ShowActivated = true,
                    Title = "Custom Shaders Patch settings",
                    LocationAndSizeKey = @".CustomShadersPatchDialog2",
                    Owner = null,
                    Buttons = new Control[0],
                    BorderThickness = new Thickness(0),
                    Opacity = 0.95,
                    BorderBrush = new SolidColorBrush(Colors.Transparent)
                };
                dlg.Background = new SolidColorBrush(((Color)dlg.FindResource("WindowBackgroundColor")).SetAlpha(200));
                CloseOpenedSettings = () => dlg.Close();
                var opened = new[]{true};
                ((Func<Task>)(async () => {
                    while (opened[0]) {
                        await Task.Delay(100);
                        if (!dlg.IsActive) {
                            dlg.Close();
                        }
                    }
                }))().Ignore();
                await dlg.ShowAndWaitAsync();
                opened[0] = false;
                CloseOpenedSettings = null;
            });
        }

        private void OnFrameNavigated(object sender, NavigationEventArgs e) { }
    }
}