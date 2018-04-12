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
using AcManager.Tools.SharedMemory;
using AcTools.Processes;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsVideo : ILoadableContent {
        public partial class ViewModel : NotifyPropertyChanged {
            private readonly AcSettingsVideo _uiParent;

            internal ViewModel(AcSettingsVideo uiParent) {
                _uiParent = uiParent;
            }

            public VideoSettings Video => AcSettingsHolder.Video;
            public OculusSettings Oculus => AcSettingsHolder.Oculus;
            public GraphicsSettings Graphics => AcSettingsHolder.Graphics;
            public IUserPresetable Presets => AcSettingsHolder.VideoPresets;

            private DelegateCommand _manageFiltersCommand;

            public DelegateCommand ManageFiltersCommand => _manageFiltersCommand ?? (_manageFiltersCommand = new DelegateCommand(() => {
                (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Lists/PpFiltersListPage.xaml",
                        UriKind.RelativeOrAbsolute));
            }));

            private AsyncCommand _benchmarkCommand;

            public AsyncCommand BenchmarkCommand => _benchmarkCommand ?? (_benchmarkCommand = new AsyncCommand(() => {
                if (Keyboard.Modifiers == ModifierKeys.Shift) {
                    return BenchmarkFastCommand.ExecuteAsync();
                }

                return GameWrapper.StartBenchmarkAsync(new Game.StartProperties(new Game.BenchmarkProperties()));
            }));

            private AsyncCommand _benchmarkFastCommand;

            public AsyncCommand BenchmarkFastCommand => _benchmarkFastCommand ?? (_benchmarkFastCommand = new AsyncCommand(() => {
                Task[] task = { null };

                if (SettingsHolder.Drive.WatchForSharedMemory) {
                    AcSharedMemory.Instance.MonitorFramesPerSecondBegin += OnMonitorFramesPerSecondBegin;
                } else {
                    DelayAndShutdown(true);
                }

                task[0] = GameWrapper.StartBenchmarkAsync(new Game.StartProperties(new Game.BenchmarkProperties()));
                return task[0];

                void DelayAndShutdown(bool extraDelay) {
                    Task.Delay(TimeSpan.FromSeconds(extraDelay ? 60 : 30)).ContinueWith(t => {
                        if (task[0] == null || task[0].IsCompleted || task[0].IsCanceled || task[0].IsFaulted) return;
                        InternalUtils.AcControlPointExecute(InternalUtils.AcControlPointCommand.Shutdown);
                    });
                }

                void OnMonitorFramesPerSecondBegin(object sender, EventArgs eventArgs) {
                    AcSharedMemory.Instance.MonitorFramesPerSecondBegin -= OnMonitorFramesPerSecondBegin;
                    DelayAndShutdown(false);
                }
            }));

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

            private async Task Share() {
                var data = Presets.ExportToPresetData();
                if (data == null) return;
                await SharingUiHelper.ShareAsync(SharedEntryType.VideoSettingsPreset,
                        Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(Presets.PresetableKey)), null,
                        data);
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
            DataContext = new ViewModel(this);
            InitializeComponent();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
            InputBindings.AddRange(new[] {
                new InputBinding(Model.BenchmarkCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(Model.BenchmarkFastCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control))
            });
        }
    }
}