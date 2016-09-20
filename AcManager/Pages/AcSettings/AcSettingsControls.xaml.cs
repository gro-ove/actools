using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using Microsoft.Win32;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls : IAcControlsConflictResolver {
        private ViewModel Model => (ViewModel)DataContext;

        public AcSettingsControls() {
            AcSettingsHolder.Controls.ConflictResolver = this;

            DataContext = new ViewModel();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(Model.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control))
            });
            InitializeComponent();

            ResizingStuff();
        }

        private void ResizingStuff() {
            DetectedControllers.Visibility = ActualWidth > 640 ? Visibility.Visible : Visibility.Collapsed;
        }

        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() {}

            private CommandBase _saveCommand;

            public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand<string>(o => {
                var dialog = new SaveFileDialog {
                    InitialDirectory = Controls.UserPresetsDirectory,
                    FileName = Path.GetFileNameWithoutExtension(Controls.CurrentPresetFilename),
                    Filter = string.Format(ToolsStrings.Presets_FileFilter, @".ini"),
                    DefaultExt = @".ini",
                    OverwritePrompt = true
                };

                var filename = Controls.CurrentPresetFilename;
                if (filename != null && FileUtils.IsAffected(Controls.UserPresetsDirectory, filename)) {
                    dialog.InitialDirectory = Path.GetDirectoryName(Path.Combine(Controls.PresetsDirectory, filename));
                    dialog.FileName = Path.GetFileNameWithoutExtension(filename);
                }

                if (o != null) {
                    dialog.FileName = o;
                }

                if (dialog.ShowDialog() != true) {
                    return;
                }

                filename = dialog.FileName;
                if (!FileUtils.IsAffected(Controls.UserPresetsDirectory, filename)) {
                    if (ModernDialog.ShowMessage(AppStrings.Controls_InvalidDirectory_Commentary,
                                                 ToolsStrings.Common_CannotDo_Title, MessageBoxButton.OKCancel) == MessageBoxResult.OK) {
                        SaveCommand?.Execute(Path.GetFileName(filename));
                    }

                    return;
                }

                Controls.SavePreset(filename);
            }));

            private CommandBase _testCommand;

            public ICommand TestCommand => _testCommand ?? (_testCommand = new DelegateCommand(() => {
                QuickDrive.Run();
            }));

            private CommandBase _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand<string>(Share));

            private async Task Share(string o) {
                switch (o) {
                    case @"FFBOnly":
                        var iniFile = new IniFile();
                        AcSettingsHolder.Controls.SaveFfbToIni(iniFile);
                        AcSettingsHolder.System.SaveFfbToIni(iniFile);

                        await SharingUiHelper.ShareAsync(SharedEntryType.ForceFeedbackPreset,
                                string.Format(AppStrings.Controls_SharedFfbOnly, Path.GetFileName(Controls.CurrentPresetName)), null, iniFile.Stringify());
                        break;
                    case @"Basic":
                        var target = Controls.InputMethod.Id == "KEYBOARD" ? AppStrings.Controls_SharedFor_Keyboard :
                                Controls.InputMethod.Id == "X360" ? AppStrings.Controls_SharedFor_XboxController :
                                        Controls.WheelAxleEntries.FirstOrDefault()?.Input?.Device?.DisplayName;

                        await SharingUiHelper.ShareAsync(SharedEntryType.ControlsPreset, Path.GetFileName(Controls.CurrentPresetName), target,
                                File.ReadAllBytes(Controls.Filename));
                        break;
                }
            }

            public ControlsSettings Controls => AcSettingsHolder.Controls;

            public SystemSettings System => AcSettingsHolder.System;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
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

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            AcSettingsHolder.Controls.Used++;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            AcSettingsHolder.Controls.Used--;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
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
                    ? string.Format(AppStrings.Controls_AlreadyUsed_MultipleMessage, inputDisplayName,
                            list.SkipLast(1).JoinToString(@", "), list.Last())
                    : string.Format(AppStrings.Controls_AlreadyUsed_Message, inputDisplayName, list.First());

            var dlg = new ModernDialog {
                Title = AppStrings.Controls_AlreadyUsed,
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
                dlg.CreateCloseDialogButton(AppStrings.Controls_RemoveOld, true, false, MessageBoxResult.Yes),
                dlg.CreateCloseDialogButton(AppStrings.Controls_ApplyToAll, false, false, MessageBoxResult.No),
                dlg.CreateCloseDialogButton(AppStrings.Controls_SwapUsings, false, false, MessageBoxResult.OK),
                dlg.CreateCloseDialogButton(UiStrings.Cancel, false, true, MessageBoxResult.Cancel),
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
            e.Handled = true;
            ShareContextMenu.DataContext = DataContext;
            ShareContextMenu.IsOpen = true;
        }
    }
}
