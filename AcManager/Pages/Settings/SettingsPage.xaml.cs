using System;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsPage {
        public SettingsPage() {
            InitializeComponent();
            if (SettingsHolder.Common.MsMode) {
                Tab.Links.Add(new Link {
                    DisplayName = AcManager.Resources.Settings_Dev,
                    Source = new Uri("/Pages/Settings/SettingsDev.xaml", UriKind.Relative)
                });
            }
        }
    }
}
