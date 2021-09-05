using System;
using System.Windows;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsLive : IParametrizedUriContent {
        public SettingsLive() {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.LiveSettings LiveSettings => SettingsHolder.Live;
        }

        public void OnUri(Uri uri) {
            if (uri.GetQueryParamBool("Separate")) {
                ContentRoot.Margin = new Thickness(20d);
            }
        }
    }
}
