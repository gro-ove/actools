using System;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsPage : IParametrizedUriContent {
        public AcSettingsPage() {
            InitializeComponent();
        }

        void IParametrizedUriContent.OnUri(Uri uri) {
            var paramUri = uri.GetQueryParam("Uri");
            if (paramUri != null) {
                Tab.SavePolicy = SavePolicy.SkipLoading;
                Tab.SelectedSource = new Uri(paramUri, UriKind.RelativeOrAbsolute);
                Logging.Debug(Tab.SelectedSource);
                return;
            }

            var category = uri.GetQueryParam("Category");
            if (category != null) {
                Tab.SavePolicy = SavePolicy.SkipLoading;
                Tab.SelectedSource = new Uri($"/Pages/AcSettings/{category}.xaml", UriKind.Relative);
            }
        }
    }
}
