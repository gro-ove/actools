using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsAppearance {
        public SettingsAppearance() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            private static BitmapScalingMode? _originalScalingMode;

            public FancyBackgroundManager FancyBackgroundManager => FancyBackgroundManager.Instance;

            public AppAppearanceManager AppAppearanceManager => AppAppearanceManager.Instance;

            internal ViewModel() {
                BitmapScaling = BitmapScalings.FirstOrDefault(x => x.Value == AppAppearanceManager.BitmapScalingMode) ?? BitmapScalings.First();
                TextFormatting = AppAppearanceManager.IdealFormattingMode ? TextFormattings[1] : TextFormattings[0];

                if (!_originalScalingMode.HasValue) {
                    _originalScalingMode = BitmapScaling.Value;
                }
            }

            public class BitmapScalingEntry : Displayable {
                public BitmapScalingMode Value { get; set; }
            }

            private bool _bitmapScalingRestartRequired;

            public bool BitmapScalingRestartRequired {
                get { return _bitmapScalingRestartRequired; }
                set {
                    if (Equals(value, _bitmapScalingRestartRequired)) return;
                    _bitmapScalingRestartRequired = value;
                    OnPropertyChanged();
                }
            }

            private ProperCommand _restartCommand;

            public ICommand RestartCommand => _restartCommand ?? (_restartCommand = new ProperCommand(o => {
                WindowsHelper.RestartCurrentApplication();
            }));

            private BitmapScalingEntry _bitmapScaling;

            public BitmapScalingEntry BitmapScaling {
                get { return _bitmapScaling; }
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
                get { return _textFormatting; }
                set {
                    if (Equals(value, _textFormatting)) return;
                    _textFormatting = value;
                    OnPropertyChanged();
                    AppAppearanceManager.IdealFormattingMode = value == TextFormattings[1];
                }
            }

            public Displayable[] TextFormattings { get; } = {
                new Displayable { DisplayName = Tools.ToolsStrings.AcSettings_Quality_Subpixel },
                new Displayable { DisplayName = Tools.ToolsStrings.AcSettings_Quality_Ideal },
            };
        }
    }
}
