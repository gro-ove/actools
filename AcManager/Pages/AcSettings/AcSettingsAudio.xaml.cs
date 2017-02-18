using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsAudio {
        public AcSettingsAudio() {
            InitializeComponent();
            DataContext = new ViewModel();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control))
            });
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() { }

            public AudioSettings Audio => AcSettingsHolder.Audio;

            public IUserPresetable Presets => AcSettingsHolder.AudioPresets;

            private ICommand _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

            private async Task Share() {
                await SharingUiHelper.ShareAsync(SharedEntryType.AudioSettingsPreset,
                        Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(Presets.PresetableKey)), null,
                        Presets.ExportToPresetData());
            }
        }
    }
}
