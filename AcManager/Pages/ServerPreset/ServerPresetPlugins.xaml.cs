using JetBrains.Annotations;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetPlugins {
        public ServerPresetPlugins() {
            InitializeComponent();
        }

        [CanBeNull]
        private SelectedPage.ViewModel Model => DataContext as SelectedPage.ViewModel;
    }
}