using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Application = System.Windows.Application;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;

namespace AcManager.Pages.Settings {
    public partial class SettingsAppearance {
        public SettingsAppearance() {
            InitializeComponent();
            DataContext = new ViewModel();
            BackgroundChangeMenu.DataContext = DataContext;
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ScaleSlider.PreviewMouseLeftButtonUp += (s, a) => ApplyScale();
            /*var thumb = ScaleSlider.FindVisualChild<Thumb>();
            if (thumb != null) {
                thumb.DragDelta += (s, a) => ApplyScale();
            }*/
        }

        private void ApplyScale() {
            var window = Application.Current.MainWindow as DpiAwareWindow;
            if (window == null) {
                ScaleSlider.RemoveFocus();
            } else {
                var position = window.GetMousePosition();
                var before = position * new Matrix(window.ScaleX, 0, 0, window.ScaleY, 0, 0);
                AppearanceManager.Instance.AppScale = ScaleSlider.Value.Round(0.01);
                var after = position * new Matrix(window.ScaleX, 0, 0, window.ScaleY, 0, 0);
                window.Left += before.X - after.X;
                window.Top += before.Y - after.Y;
            }

            window?.EnsureOnScreen();
        }

        public class ViewModel : NotifyPropertyChanged {
            private static BitmapScalingMode? _originalScalingMode;
            private static bool? _originalSoftwareRendering;
            private static bool? _originalDisableTransparency;

            public FancyBackgroundManager FancyBackgroundManager => FancyBackgroundManager.Instance;
            public AppearanceManager AppearanceManager => AppearanceManager.Instance;
            public AppAppearanceManager AppAppearanceManager => AppAppearanceManager.Instance;
            public SettingsHolder.InterfaceSettings Interface => SettingsHolder.Interface;

            public SettingEntry[] Screens { get; }

            private SettingEntry _forceScreen;

            [CanBeNull]
            public SettingEntry ForceScreen {
                get => _forceScreen;
                set {
                    if (Equals(value, _forceScreen)) return;
                    _forceScreen = value;
                    OnPropertyChanged();
                    AppearanceManager.Instance.ForceScreenName = value?.Value;
                }
            }

            internal ViewModel() {
                var friendlyNames = ScreenInterrogatory.GetFriendlyNames();
                Screens = Screen.AllScreens.Select(x => new SettingEntry(x.DeviceName, GetScreenName(x, friendlyNames?.GetValueOrDefault(x)))).ToArray();
                _forceScreen = Screens.GetByIdOrDefault(AppearanceManager.Instance.ForceScreenName);

                BitmapScaling = BitmapScalings.FirstOrDefault(x => x.Value == AppAppearanceManager.BitmapScalingMode) ?? BitmapScalings.First();
                TextFormatting = AppAppearanceManager.IdealFormattingMode == null ? TextFormattings[0] :
                        AppAppearanceManager.IdealFormattingMode.Value ? TextFormattings[2] : TextFormattings[1];

                if (!_originalScalingMode.HasValue) {
                    _originalScalingMode = BitmapScaling.Value;
                }

                if (!_originalSoftwareRendering.HasValue) {
                    _originalSoftwareRendering = AppAppearanceManager.SoftwareRenderingMode;
                    _originalDisableTransparency = AppAppearanceManager.DisallowTransparency;
                }

                AppAppearanceManager.SubscribeWeak(OnAppAppearanceManagerChanged);
            }

            private void OnAppAppearanceManagerChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(AppAppearanceManager.SoftwareRenderingMode)
                        || e.PropertyName == nameof(AppAppearanceManager.DisallowTransparency)) {
                    OnPropertyChanged(nameof(SoftwareRenderingRestartRequired));
                }
            }

            private static string GetScreenName(Screen x, [CanBeNull] string friendlyName) {
                return $@"{friendlyName ?? x.DeviceName} ({x.Bounds.ToString().Replace(@",", @", ").TrimStart('{').TrimEnd('}')})";
            }

            public class BitmapScalingEntry : Displayable {
                public BitmapScalingMode Value { get; set; }
            }

            private bool _bitmapScalingRestartRequired;

            public bool BitmapScalingRestartRequired {
                get => _bitmapScalingRestartRequired;
                set => Apply(value, ref _bitmapScalingRestartRequired);
            }

            private CommandBase _restartCommand;

            public ICommand RestartCommand => _restartCommand ?? (_restartCommand = new DelegateCommand(WindowsHelper.RestartCurrentApplication));

            private BitmapScalingEntry _bitmapScaling;

            public BitmapScalingEntry BitmapScaling {
                get => _bitmapScaling;
                set {
                    if (Equals(value, _bitmapScaling)) return;
                    _bitmapScaling = value;
                    OnPropertyChanged();

                    if (_originalScalingMode.HasValue && value != null) {
                        AppAppearanceManager.BitmapScalingMode = value.Value;
                        BitmapScalingRestartRequired = value.Value != _originalScalingMode;
                    }
                }
            }

            public BitmapScalingEntry[] BitmapScalings { get; } = {
                new BitmapScalingEntry { DisplayName = Tools.ToolsStrings.AcSettings_Quality_Low, Value = BitmapScalingMode.NearestNeighbor },
                new BitmapScalingEntry { DisplayName = Tools.ToolsStrings.AcSettings_Quality_Normal, Value = BitmapScalingMode.LowQuality },
                new BitmapScalingEntry { DisplayName = Tools.ToolsStrings.AcSettings_Quality_High, Value = BitmapScalingMode.HighQuality }
            };

            public bool SoftwareRenderingRestartRequired => AppAppearanceManager.SoftwareRenderingMode != _originalSoftwareRendering
                    || AppAppearanceManager.DisallowTransparency != _originalDisableTransparency;

            private Displayable _textFormatting;

            public Displayable TextFormatting {
                get => _textFormatting;
                set {
                    if (Equals(value, _textFormatting)) return;
                    _textFormatting = value;
                    OnPropertyChanged();
                    AppAppearanceManager.IdealFormattingMode = value == TextFormattings[0] ? (bool?)null :
                            value == TextFormattings[2];
                }
            }

            public Displayable[] TextFormattings { get; } = {
                new Displayable { DisplayName = string.Format(Tools.ToolsStrings.Common_Recommended, "Auto") },
                new Displayable { DisplayName = Tools.ToolsStrings.AcSettings_Quality_Subpixel },
                new Displayable { DisplayName = Tools.ToolsStrings.AcSettings_Quality_Ideal },
            };

            private DelegateCommand _changeBackgroundImageCommand;

            public DelegateCommand ChangeBackgroundImageCommand => _changeBackgroundImageCommand
                    ?? (_changeBackgroundImageCommand = new DelegateCommand(() => {
                        var dialog = new OpenFileDialog {
                            Filter = FileDialogFilters.ImagesFilter,
                            Title = "Select background image",
                            InitialDirectory = Path.GetDirectoryName(AppAppearanceManager.BackgroundFilename) ?? AcPaths.GetDocumentsScreensDirectory(),
                            RestoreDirectory = true
                        };

                        if (dialog.ShowDialog() == true) {
                            AppAppearanceManager.BackgroundFilename = dialog.FileName;
                        }
                    }));

            private DelegateCommand _changeBackgroundSlideshowCommand;

            public DelegateCommand ChangeBackgroundSlideshowCommand => _changeBackgroundSlideshowCommand
                    ?? (_changeBackgroundSlideshowCommand = new DelegateCommand(() => {
                        var dialog = new FolderBrowserDialog {
                            ShowNewFolderButton = false,
                            Description = "Select folder with background images",
                            SelectedPath = (AppAppearanceManager.BackgroundFilename?.EndsWith(@"\") == true
                                    ? AppAppearanceManager.BackgroundFilename.TrimEnd('\\')
                                    : Path.GetDirectoryName(AppAppearanceManager.BackgroundFilename))
                                    ?? AcPaths.GetDocumentsScreensDirectory()
                        };

                        if (dialog.ShowDialog() == DialogResult.OK) {
                            AppAppearanceManager.BackgroundFilename = dialog.SelectedPath.TrimEnd('/', '\\') + @"\\";
                        }
                    }));

            private DelegateCommand _resetBackgroundImageCommand;

            public DelegateCommand ResetBackgroundImageCommand => _resetBackgroundImageCommand
                    ?? (_resetBackgroundImageCommand = new DelegateCommand(() => AppAppearanceManager.BackgroundFilename = null));
        }

        private void OnBackgroundChangeClick(object sender, RoutedEventArgs e) {
            BackgroundChangeMenu.IsOpen = !BackgroundChangeMenu.IsOpen;
        }
    }
}