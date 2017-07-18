using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Media;
using Microsoft.Win32;

namespace AcManager.Pages.Settings {
    public partial class SettingsAppearance {
        public SettingsAppearance() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            var thumb = ScaleSlider.FindVisualChild<Thumb>();
            if (thumb != null) {
                thumb.DragCompleted += (s, a) => ScaleSlider.RemoveFocus();
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            private static BitmapScalingMode? _originalScalingMode;

            public FancyBackgroundManager FancyBackgroundManager => FancyBackgroundManager.Instance;

            public AppAppearanceManager AppAppearanceManager => AppAppearanceManager.Instance;

            public SettingsHolder.InterfaceSettings Interface => SettingsHolder.Interface;

            internal ViewModel() {
                BitmapScaling = BitmapScalings.FirstOrDefault(x => x.Value == AppAppearanceManager.BitmapScalingMode) ?? BitmapScalings.First();
                TextFormatting = AppAppearanceManager.IdealFormattingMode == null ? TextFormattings[0] :
                        AppAppearanceManager.IdealFormattingMode.Value ? TextFormattings[2] : TextFormattings[1];

                if (!_originalScalingMode.HasValue) {
                    _originalScalingMode = BitmapScaling.Value;
                }
            }

            public class BitmapScalingEntry : Displayable {
                public BitmapScalingMode Value { get; set; }
            }

            private bool _bitmapScalingRestartRequired;

            public bool BitmapScalingRestartRequired {
                get => _bitmapScalingRestartRequired;
                set {
                    if (Equals(value, _bitmapScalingRestartRequired)) return;
                    _bitmapScalingRestartRequired = value;
                    OnPropertyChanged();
                }
            }

            private CommandBase _restartCommand;

            public ICommand RestartCommand => _restartCommand ?? (_restartCommand = new DelegateCommand(() => {
                WindowsHelper.RestartCurrentApplication();
            }));

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

            public DelegateCommand ChangeBackgroundImageCommand => _changeBackgroundImageCommand ?? (_changeBackgroundImageCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.ImagesFilter,
                    Title = "Select Image For Background",
                    InitialDirectory = Path.GetDirectoryName(AppAppearanceManager.BackgroundFilename) ?? FileUtils.GetDocumentsScreensDirectory(),
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() == true) {
                    AppAppearanceManager.BackgroundFilename = dialog.FileName;
                }
            }));

            private DelegateCommand _resetBackgroundImageCommand;

            public DelegateCommand ResetBackgroundImageCommand => _resetBackgroundImageCommand ?? (_resetBackgroundImageCommand = new DelegateCommand(() => {
                AppAppearanceManager.BackgroundFilename = null;
            }));
        }
    }
}
