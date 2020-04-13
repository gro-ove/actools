using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Internal;
using AcManager.Pages.Windows;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcManager.Tools.SharedMemory;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using Application = System.Windows.Application;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsVideo : ILoadableContent {
        public partial class ViewModel : NotifyPropertyChanged {
            public SettingEntry[] Screens { get; }

            private SettingEntry _forceScreen;
            private Busy _forceScreenSave = new Busy();

            [CanBeNull]
            public SettingEntry ForceScreen {
                get => _forceScreen;
                set {
                    if (Equals(value, _forceScreen)) return;
                    _forceScreen = value;
                    OnPropertyChanged();
                    _forceScreenSave.DoDelay(() => {
                        var filename = PatchHelper.GetWindowPositionConfig();
                        FileUtils.EnsureFileDirectoryExists(filename);
                        var acWindowCfg = new IniFile(filename);
                        var window = Screen.AllScreens.FirstOrDefault(x => x.DeviceName == value?.Id);
                        if (window == null) {
                            acWindowCfg.Clear();
                        } else {
                            acWindowCfg["AC_WINDOW_POSITION"].Set("REAL_X", window.Bounds.Left);
                            acWindowCfg["AC_WINDOW_POSITION"].Set("REAL_Y", window.Bounds.Top);
                            acWindowCfg["AC_WINDOW_POSITION"].Set("X", window.Bounds.Left + 8);
                            acWindowCfg["AC_WINDOW_POSITION"].Set("Y", window.Bounds.Top + 31);
                        }
                        acWindowCfg.Save();
                    }, 1000);
                }
            }

            private readonly AcSettingsVideo _uiParent;

            internal ViewModel(AcSettingsVideo uiParent) {
                _uiParent = uiParent;

                if (PatchHelper.IsFeatureSupported(PatchHelper.FeatureWindowPosition)) {
                    var acWindowCfg = new IniFile(PatchHelper.GetWindowPositionConfig());
                    var windowX = acWindowCfg["AC_WINDOW_POSITION"].GetIntNullable("REAL_X");
                    var windowY = acWindowCfg["AC_WINDOW_POSITION"].GetIntNullable("REAL_Y");
                    var friendlyNames = ScreenInterrogatory.GetFriendlyNames();
                    Screens = Screen.AllScreens.Select(x => new SettingEntry(x.DeviceName, GetScreenName(x, friendlyNames?.GetValueOrDefault(x)))).ToArray();
                    if (windowX.HasValue && windowY.HasValue) {
                        var screen = Screen.AllScreens.FirstOrDefault(x => x.Bounds.Left == windowX.Value && x.Bounds.Top == windowY.Value);
                        _forceScreen = Screens.GetByIdOrDefault(screen?.DeviceName);
                    } else {
                        _forceScreen = null;
                    }
                }
            }

            private static string GetScreenName(Screen x, [CanBeNull] string friendlyName) {
                return $@"{friendlyName ?? x.DeviceName} ({x.Bounds.ToString().Replace(@",", @", ").TrimStart('{').TrimEnd('}')})";
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

            if (PatchHelper.IsFeatureSupported(PatchHelper.FeatureDynamicShadowResolution)) {
                PatchAcToDisableShadows.Visibility = Visibility.Collapsed;
            }
        }
    }
}