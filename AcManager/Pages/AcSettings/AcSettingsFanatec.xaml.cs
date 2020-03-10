using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsFanatec {
        public AcSettingsFanatec() {
            InitializeComponent();
            DataContext = new ViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() { }

            public FanatecSettings Fanatec => AcSettingsHolder.Fanatec;
        }
    }
}
