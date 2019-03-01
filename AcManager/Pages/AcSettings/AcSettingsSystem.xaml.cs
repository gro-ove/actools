using System.Linq;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsSystem {
        public AcSettingsSystem() {
            AcSettingsHolder.System.ScreenshotFormats = SystemSettings.DefaultScreenshotFormats().Concat(
                    PatchHelper.IsFeatureSupported(PatchHelper.FeatureExtraScreenshotFormats)
                            ? PatchHelper.GetConfig("data_manifest.ini")["FEATURES"].GetStrings("SUPPORTED_SCREENSHOT_FORMATS")
                                                                                    .Select(x => new SettingEntry(x, $"{x} (added by Custom Shaders Patch)"))
                            : new SettingEntry[0]).ToArray();

            InitializeComponent();
            DataContext = new ViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() { }

            public ProximityIndicatorSettings ProximityIndicator => AcSettingsHolder.ProximityIndicator;
            public SessionInfoSettings SessionInfo => AcSettingsHolder.SessionInfo;
            public SkidmarksSettings Skidmarks => AcSettingsHolder.Skidmarks;
            public SystemSettings System => AcSettingsHolder.System;
            public PitMenuSettings PitMenu => AcSettingsHolder.PitMenu;
            public SystemOptionsSettings SystemOptions => AcSettingsHolder.SystemOptions;
            public GhostSettings Ghost => AcSettingsHolder.Ghost;

            private string _ghostDisplayColor;

            public string GhostDisplayColor {
                get => _ghostDisplayColor;
                set => Apply(value, ref _ghostDisplayColor);
            }
        }
    }
}