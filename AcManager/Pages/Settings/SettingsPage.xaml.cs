using System;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsPage : IParametrizedUriContent {
        public SettingsPage() {
            InitializeComponent();
            if (SettingsHolder.Common.DeveloperMode) {
                Tab.Links.Add(new Link {
                    DisplayName = AppStrings.Settings_Dev,
                    Source = new Uri("/Pages/Settings/SettingsDev.xaml", UriKind.Relative)
                });
            }

            #if DEBUG
            Tab.Links.Add(new Link {
                DisplayName = "Debug",
                Source = new Uri("/Pages/Settings/SettingsDebug.xaml", UriKind.Relative)
            });
            #endif
        }

        void IParametrizedUriContent.OnUri(Uri uri) {
            Tab.SelectedSource = new Uri($"/Pages/Settings/{uri.GetQueryParam("Category")}.xaml", UriKind.Relative);
        }
    }
}
