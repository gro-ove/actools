using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Settings {
    public partial class SettingsAppearance {
        public SettingsAppearance() {
            InitializeComponent();
            DataContext = new AppearanceViewModel();
        }

        public class AppearanceViewModel : NotifyPropertyChanged {
            public FancyBackgroundManager FancyBackgroundManager => FancyBackgroundManager.Instance;

            public AppAppearanceManager AppAppearanceManager => AppAppearanceManager.Instance;
        }
    }
}
