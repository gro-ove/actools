using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Internal;
using AcManager.Pages.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsVideo: ILoadableContent {
        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() {}

            public VideoSettings Video => AcSettingsHolder.Video;

            public OculusSettings Oculus => AcSettingsHolder.Oculus;

            public GraphicsSettings Graphics => AcSettingsHolder.Graphics;

            public IUserPresetable Presets => AcSettingsHolder.VideoPresets;

            private ICommand _manageFiltersCommand;

            public ICommand ManageFiltersCommand => _manageFiltersCommand ?? (_manageFiltersCommand = new DelegateCommand(() => {
                (Application.Current.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Lists/PpFiltersListPage.xaml", UriKind.RelativeOrAbsolute));
            }));

            private ICommand _benchmarkCommand;

            public ICommand BenchmarkCommand => _benchmarkCommand ??
                    (_benchmarkCommand = new AsyncCommand(() => GameWrapper.StartBenchmarkAsync(new Game.StartProperties(new Game.BenchmarkProperties()))));

            private ICommand _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

            private async Task Share() {
                await SharingUiHelper.ShareAsync(SharedEntryType.VideoSettingsPreset,
                        Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(Presets.PresetableKey)), null,
                        Presets.ExportToPresetData());
            }
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return PpFiltersManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            PpFiltersManager.Instance.EnsureLoaded();
        }

        public ViewModel Model => (ViewModel)DataContext;

        public void Initialize() {
            DataContext = new ViewModel();
            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.BenchmarkCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control))
            });
        }
    }
}
