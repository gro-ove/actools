using System;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsPage {
        public SettingsPage() {
            InitializeComponent();
            if (SettingsHolder.Common.DeveloperMode) {
                Tab.Links.Add(new Link {
                    DisplayName = "dev",
                    Source = new Uri("/Pages/Settings/SettingsDev.xaml", UriKind.Relative)
                });
            }
        }
    }
}
