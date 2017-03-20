using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Commands;

namespace AcManager.UserControls {
    public partial class TrackStateEditor {
        public TrackStateEditor() {
            InitializeComponent();
        }

        private ICommand _shareCommand;

        public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

        private async Task Share() {
            var model = DataContext as TrackStateViewModel;
            var data = model?.ExportToPresetData();
            if (data == null) return;

            await SharingUiHelper.ShareAsync(SharedEntryType.TrackStatePreset,
                    Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(model.PresetableKey)), null,
                    data);
        }
    }
}
