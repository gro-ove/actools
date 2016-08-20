using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.UserControls {
    public partial class AssistsEditor {
        public AssistsEditor() {
            InitializeComponent();
        }

        private ICommand _shareCommand;

        public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

        private async Task Share(object o) {
            var model = DataContext as AssistsViewModel;
            if (model == null) return;

            await SharingUiHelper.ShareAsync(SharedEntryType.AssistsSetupPreset,
                    Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(model.PresetableKey)), null,
                    model.ExportToPresetData());
        }
    }
}
