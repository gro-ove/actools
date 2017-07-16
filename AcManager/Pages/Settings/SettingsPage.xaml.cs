using System;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

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
            var paramUri = uri.GetQueryParam("Uri");
            if (paramUri != null) {
                Tab.SavePolicy = SavePolicy.SkipLoading;
                Tab.SelectedSource = new Uri(paramUri, UriKind.RelativeOrAbsolute);
                return;
            }

            var category = uri.GetQueryParam("Category");
            if (category != null) {
                Tab.SavePolicy = SavePolicy.SkipLoading;
                Tab.SelectedSource = new Uri($"/Pages/Settings/{category}.xaml", UriKind.Relative);
            }
        }
    }
}
