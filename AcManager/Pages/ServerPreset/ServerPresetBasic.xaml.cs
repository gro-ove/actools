using AcManager.Tools.Helpers;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetBasic {
        public ServerPresetBasic() {
            InitializeComponent();
            if (SettingsHolder.Online.ServerPresetsFitInFewerTabs) {
                WelcomeMessageTextArea.Height = 80;
            }
        }
    }
}
