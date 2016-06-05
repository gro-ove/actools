using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Pages.Drive;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using Microsoft.Win32;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls : IAcControlsConflictResolver {
        public AcSettingsControls() {
            AcSettingsHolder.Controls.ConflictResolver = this;

            InitializeComponent();
            DataContext = new AcControlsViewModel();
            ResizingStuff();
        }

        private void ResizingStuff() {
            DetectedControllers.Visibility = ActualWidth > 640 ? Visibility.Visible : Visibility.Collapsed;
        }

        public class AcControlsViewModel : NotifyPropertyChanged, IPreviewProvider {
            internal AcControlsViewModel() {
                Controls.PresetsUpdated += OnPresetsUpdated;
                RebuildPresetsList().Forget();
            }

            private bool _innerReloading;

            private async void OnPresetsUpdated(object sender, EventArgs args) {
                if (_innerReloading) return;

                _innerReloading = true;

                try {
                    await Task.Delay(200);
                    Application.Current.Dispatcher.Invoke(() => {
                        RebuildPresetsList().Forget();
                    });
                } catch (Exception e) {
                    Logging.Warning("OnPresetsUpdated() exception: " + e);
                } finally {
                    _innerReloading = false;
                }
            }

            private bool _presetsReady;

            public bool PresetsReady {
                get { return _presetsReady; }
                set {
                    if (Equals(value, _presetsReady)) return;
                    _presetsReady = value;
                    OnPropertyChanged();
                }
            }

            private bool _reloading;

            public class PresetEntry : NotifyPropertyChanged, ISavedPresetEntry {
                public PresetEntry(string filename) {
                    DisplayName = Path.GetFileNameWithoutExtension(filename);
                    Filename = filename;
                }

                public string DisplayName { get; }

                public string Filename { get; }

                public string ReadData() {
                    return FileUtils.ReadAllText(Filename);
                }
            }

            private async Task<MenuItem> RebuildAsync(string header, string sub) {
                var result = new MenuItem { Header = header };
                var directory = Path.Combine(Controls.PresetsDirectory, sub);
                var list = await Task.Run(() => FileUtils.GetFiles(directory, "*.ini").Select(x => new PresetEntry(x)).ToList());
                foreach (var item in UserPresetsControl.GroupPresets(list, directory, ClickHandler, this, ".ini")) {
                    result.Items.Add(item);
                }
                return result;
            }

            private void ClickHandler(object sender, RoutedEventArgs routedEventArgs) {
                var entry = (((MenuItem)sender).Tag as UserPresetsControl.TagHelper)?.Entry as PresetEntry;
                if (entry == null || (Controls.CurrentPresetName == null || Controls.CurrentPresetChanged) &&
                        ModernDialog.ShowMessage($"Load “{entry.DisplayName}”? Current values will be replaced (but later could be restored from Recycle Bin).",
                                "Are you sure?", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return;
                }

                Controls.LoadPreset(entry.Filename);
            }

            private async Task RebuildPresetsList() {
                if (_reloading) return;

                _reloading = true;
                PresetsReady = false;

                try {
                    Presets.ReplaceEverythingBy(new[] {
                        await RebuildAsync("Built-in Presets", "presets"),
                        await RebuildAsync("User Presets", "savedsetups")
                    });
                } catch (Exception e) {
                    Logging.Warning("RebuildPresetsList() exception: " + e);
                } finally {
                    _reloading = false;
                }
            }

            object IPreviewProvider.GetPreview(string serializedData) {
                var ini = IniFile.Parse(serializedData);
                return new BbCodeBlock {
                    BbCode = $"Input method: [b]{ini["HEADER"].GetEntry("INPUT_METHOD", Controls.InputMethods).DisplayName}[/b]"
                };
            }

            private RelayCommand _saveCommand;

            public RelayCommand SaveCommand => _saveCommand ?? (_saveCommand = new RelayCommand(o => {
                var presetsDirectory = Path.Combine(Controls.PresetsDirectory, "savedsetups");

                var dialog = new SaveFileDialog {
                    InitialDirectory = presetsDirectory,
                    Filter = string.Format(@"Presets (*{0})|*{0}", ".ini"),
                    DefaultExt = ".ini",
                    OverwritePrompt = true
                };

                var filename = Controls.Ini["__LAUNCHER_CM"].Get("PRESET_NAME");
                if (filename?.StartsWith("savedsetups", StringComparison.OrdinalIgnoreCase) != null) {
                    dialog.InitialDirectory = Path.GetDirectoryName(Path.Combine(Controls.PresetsDirectory, filename));
                    dialog.FileName = Path.GetFileNameWithoutExtension(filename);
                }

                if (o != null) {
                    dialog.FileName = o as string;
                }

                if (dialog.ShowDialog() != true) {
                    return;
                }

                filename = dialog.FileName;
                if (!filename.StartsWith(presetsDirectory)) {
                    if (ModernDialog.ShowMessage("Please, choose a file in initial directory or some subdirectory.",
                                                 "Can’t Do", MessageBoxButton.OKCancel) == MessageBoxResult.OK) {
                        SaveCommand?.Execute(Path.GetFileName(filename));
                    } else {
                        return;
                    }
                }

                Controls.SavePreset(filename);
            }));

            private RelayCommand _testCommand;

            public RelayCommand TestCommand => _testCommand ?? (_testCommand = new RelayCommand(o => {
                QuickDrive.Run();
            }));

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Execute));

            private async Task Execute(object o) {
                if (o as string == "FFBOnly") {
                    var iniFile = new IniFile();
                    AcSettingsHolder.Controls.SaveFfbToIni(iniFile);
                    AcSettingsHolder.System.SaveFfbToIni(iniFile);

                    await SharingUiHelper.ShareAsync(SharingHelper.EntryType.ForceFeedbackPreset, Path.GetFileName(Controls.CurrentPresetName) + " (FFB Only)", null, iniFile.Stringify());
                } else if (o as string == "Basic") {
                    var target = Controls.InputMethod.Id == "KEYBOARD" ? "keyboard" :
                            Controls.InputMethod.Id == "X360" ? "Xbox 360 controller" :
                                    Controls.WheelAxleEntries.FirstOrDefault()?.Input?.Device?.DisplayName;

                    await SharingUiHelper.ShareAsync(SharingHelper.EntryType.ControlsPreset, Path.GetFileName(Controls.CurrentPresetName), target,
                            File.ReadAllBytes(Controls.Filename));
                }
            }

            public BetterObservableCollection<MenuItem> Presets { get; } = new BetterObservableCollection<MenuItem>();

            public AcSettingsHolder.ControlsSettings Controls => AcSettingsHolder.Controls;

            public AcSettingsHolder.SystemSettings System => AcSettingsHolder.System;
        }

        private void AcSettingsControls_OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ResizingStuff();
        }

        public static IValueConverter ModeToUriConverter { get; } = new ModeToUriConverterInner();

        private class ModeToUriConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                switch (value?.ToString()) {
                    case "WHEEL":
                        return new Uri("/Pages/AcSettings/AcSettingsControls_Wheel.xaml", UriKind.Relative);

                    case "X360":
                        // TODO
                        return new Uri("/Pages/Miscellaneous/WorkInProgress.xaml?xbox360", UriKind.Relative);

                    case "KEYBOARD":
                        return new Uri("/Pages/AcSettings/AcSettingsControls_Keyboard.xaml", UriKind.Relative);

                    default:
                        return null;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        private bool _loaded;

        private void AcSettingsControls_OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            AcSettingsHolder.Controls.Used++;
        }

        private void AcSettingsControls_OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            AcSettingsHolder.Controls.Used--;
        }

        private void AcSettingsControls_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Escape:
                case Key.Back:
                case Key.Enter:
                    if (AcSettingsHolder.Controls.StopWaiting()) {
                        e.Handled = true;
                    }
                    break;

                case Key.Delete:
                    if (AcSettingsHolder.Controls.ClearWaiting()) {
                        e.Handled = true;
                    }
                    break;

                default:
                    if (AcSettingsHolder.Controls.AssignKey(e.Key)) {
                        e.Handled = true;
                    }
                    break;
            }
        }

        public AcControlsConflictSolution Resolve(string inputDisplayName, IEnumerable<string> existingAssignments) {
            var list = existingAssignments.Select(x => $"“{x}”").ToList();
            var message = list.Count > 1
                    ? $"“{inputDisplayName}” is already used for {list.SkipLast(1).JoinToString(", ")} and {list.Last()}. Do you want to remove old usings first?"
                    : $"“{inputDisplayName}” is already used for {list.First()}. Do you want to remove old using first?";

            var dlg = new ModernDialog {
                Title = "Already used",
                Content = new ScrollViewer {
                    Content = new BbCodeBlock { BbCode = message, Margin = new Thickness(0, 0, 0, 8) },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 480,
                MaxWidth = 640
            };

            dlg.Buttons = new[] {
                dlg.CreateCloseDialogButton("Yes, Remove Old", true, false, MessageBoxResult.Yes),
                dlg.CreateCloseDialogButton("No, Apply to All", false, false, MessageBoxResult.No),
                dlg.CreateCloseDialogButton("Flip Usings", false, false, MessageBoxResult.OK),
                dlg.CreateCloseDialogButton("Cancel", false, true, MessageBoxResult.Cancel),
            };
            dlg.ShowDialog();

            switch (dlg.MessageBoxResult) {
                case MessageBoxResult.Yes:
                    return AcControlsConflictSolution.ClearPrevious;
                case MessageBoxResult.No:
                    return AcControlsConflictSolution.KeepEverything;
                case MessageBoxResult.OK:
                    return AcControlsConflictSolution.Flip;
                case MessageBoxResult.Cancel:
                    return AcControlsConflictSolution.Cancel;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ShareButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            ShareContextMenu.DataContext = DataContext;
            ShareContextMenu.IsOpen = true;
        }
    }
}
