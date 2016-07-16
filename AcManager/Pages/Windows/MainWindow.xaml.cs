using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using AcManager.Controls.Helpers;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.About;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using Newtonsoft.Json;
using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using QuickSwitchesBlock = AcManager.Controls.QuickSwitches.QuickSwitchesBlock;

namespace AcManager.Pages.Windows {
    public partial class MainWindow : IFancyBackgroundListener {
        public static bool OptionEnableManagerTabs = true;
        public static bool OptionLiteModeSupported = false;

        private readonly bool _cancelled;
        private readonly string _testGameDialog = null;

        public MainWindow() {
            Application.Current.MainWindow = this;

            _cancelled = false;

            if (AppArguments.Values.Any()) {
                ProcessArguments();
            }

            if (_testGameDialog != null) {
                Logging.Write("[MainWindow] Testing mode");
                var ui = new GameDialog();
                ui.ShowDialogWithoutBlocking();
                ((IGameUi)ui).OnResult(JsonConvert.DeserializeObject<Game.Result>(FileUtils.ReadAllText(_testGameDialog)), null);
                _cancelled = true;
            }

            if (_cancelled) {
                Close();
                return;
            }

            DataContext = new MainWindowViewModel();
            InputBindings.AddRange(new[] {
                new InputBinding(new NavigateCommand(this, "about"), new KeyGesture(Key.F1, ModifierKeys.Alt)),
                new InputBinding(new NavigateCommand(this, "drive"), new KeyGesture(Key.F1)),
                new InputBinding(new NavigateCommand(this, "media"), new KeyGesture(Key.F2)),
                new InputBinding(new NavigateCommand(this, "content"), new KeyGesture(Key.F3)),
                new InputBinding(new NavigateCommand(this, "settings"), new KeyGesture(Key.F4))
            });
            InitializeComponent();

            LinkNavigator.Commands.Add(new Uri("cmd://enterkey"), Model.EnterKeyCommand);
            AppKeyHolder.ProceedMainWindow(this);
            
            foreach (var result in MenuLinkGroups.OfType<LinkGroupFilterable>()
                                                 .Where(x => x.Source.OriginalString.Contains("/online.xaml", StringComparison.OrdinalIgnoreCase))) {
                result.LinkChanged += OnlineLinkChanged;
            }

            foreach (var result in MenuLinkGroups.OfType<LinkGroupFilterable>()
                                                 .Where(x => string.Equals(x.GroupKey, "content", StringComparison.OrdinalIgnoreCase))) {
                result.LinkChanged += ContentLinkChanged;
            }

            UpdateLiveTabs();
            SettingsHolder.Live.PropertyChanged += Live_PropertyChanged;

            UpdateServerTab();
            SettingsHolder.Online.PropertyChanged += Online_PropertyChanged;

            if (!OfficialStarterNotification() && PluginsManager.Instance.HasAnyNew()) {
                Toast.Show("Don’t forget to install some plugins!", "");
            }
        }

        private void Online_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.OnlineSettings.ServerPresetsManaging)) {
                UpdateServerTab();
            }
        }

        private void UpdateServerTab() {
            ServerGroup.IsShown = SettingsHolder.Online.ServerPresetsManaging;
        }

        private const string KeyOfficialStarterNotification = "mw.osn";

        private bool OfficialStarterNotification() {
            if (ValuesStorage.GetBool(KeyOfficialStarterNotification)) return false;

            if (SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.OfficialStarterType) {
                ValuesStorage.Set(KeyOfficialStarterNotification, true);
                return false;
            }

            var launcher = FileUtils.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);
            if (FileVersionInfo.GetVersionInfo(launcher).FileVersion.IsVersionOlderThan("0.16.714")) {
                return false;
            }

            Toast.Show("Now With Official Support!", "New starter is ready, now without any patching at all", () => {
                if (ModernDialog.ShowMessage(
                        "Since 1.7 Kunos added an official support for custom launchers. Basically, it works like Starter+, but now CM doesn't have to replace AssettoCorsa.exe, and it's great! Would you like to switch to a new Official Starter?",
                        "Good news!", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    SettingsHolder.Drive.SelectedStarterType = SettingsHolder.DriveSettings.OfficialStarterType;
                }

                ValuesStorage.Set(KeyOfficialStarterNotification, true);
            });
            return true;
        }

        private void Live_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.LiveSettings.RsrEnabled) ||
                    e.PropertyName == nameof(SettingsHolder.LiveSettings.SrsEnabled)) {
                UpdateLiveTabs();
            }
        }

        private void UpdateLiveTabs() {
            RsrLink.IsShown = SettingsHolder.Live.RsrEnabled;
            SrsLink.IsShown = SettingsHolder.Live.SrsEnabled;
            LiveGroup.IsShown = LiveGroup.Links.Any(x => x.IsShown);
        }

        /// <summary>
        /// Temporary fix.
        /// </summary>
        private static void OnlineLinkChanged(object sender, LinkChangedEventArgs e) {
            var group = (LinkGroupFilterable)sender;
            var type = group.Source.GetQueryParamEnum<OnlineManagerType>("Mode");

            var oldKey = type + "_" + typeof(ServerEntry).Name + "_" + e.OldValue;
            var newKey = type + "_" + typeof(ServerEntry).Name + "_" + e.NewValue;
            LimitedStorage.Move(LimitedSpace.SelectedEntry, oldKey, newKey);
            LimitedStorage.Move(LimitedSpace.OnlineSorting, oldKey, newKey);
            LimitedStorage.Move(LimitedSpace.OnlineQuickFilter, oldKey, newKey);
        }

        /// <summary>
        /// Temporary fix to keep selected object while editing current filter.
        /// </summary>
        private static void ContentLinkChanged(object sender, LinkChangedEventArgs e) {
            var group = (LinkGroupFilterable)sender;

            Type type;
            switch (group.DisplayName) {
                case "cars":
                    type = typeof(CarObject);
                    break;
                case "tracks":
                    type = typeof(TrackObject);
                    break;
                case "showrooms":
                    type = typeof(ShowroomObject);
                    break;
                default:
                    return;
            }

            var oldKey = "Content_" + type.Name + "_" + e.OldValue;
            var newKey = "Content_" + type.Name + "_" + e.NewValue;
            LimitedStorage.Move(LimitedSpace.SelectedEntry, oldKey, newKey);
        }

        private class NavigateCommand : CommandBase {
            private readonly MainWindow _window;
            private readonly string _key;

            public NavigateCommand(MainWindow window, string key) {
                _window = window;
                _key = key;
            }

            protected override void OnExecute(object parameter) {
                var link = _window.TitleLinks.OfType<TitleLink>().FirstOrDefault(x => x.GroupKey == _key);
                if (link == null || !link.IsEnabled || link.NonSelectable) return;
                _window.NavigateTo(link.Source);
            }
        }

        private readonly ArgumentsHandler _argumentsHandler = new ArgumentsHandler();

        private async void ProcessArguments() {
            if (OptionLiteModeSupported) {
                Visibility = Visibility.Hidden;
            }

            var cancelled = true;
            foreach (var arg in AppArguments.Values) {
                Logging.Write("[MainWindow] Input: " + arg);

                var result = await _argumentsHandler.ProcessArgument(arg);
                if (result == ArgumentHandleResult.FailedShow) {
                    NonfatalError.Notify("Can’t process argument", "Make sure it’s in valid format.");
                }

                if (result == ArgumentHandleResult.SuccessfulShow || result == ArgumentHandleResult.FailedShow) {
                    Visibility = Visibility.Visible;
                    cancelled = false;
                }
            }

            if (OptionLiteModeSupported && cancelled) {
                Close();
            }
        }

        public new void Show() {
            if (_cancelled) {
                Logging.Write("[MainWindow] Cancelled");
                return;
            }

            base.Show();
        }

        private MainWindowViewModel Model => (MainWindowViewModel)DataContext;

        public class MainWindowViewModel : NotifyPropertyChanged {
            private RelayCommand _enterKeyCommand;

            public RelayCommand EnterKeyCommand => _enterKeyCommand ?? (_enterKeyCommand = new RelayCommand(o => {
                new AppKeyDialog().ShowDialog();
            }));

            public AppUpdater AppUpdater => AppUpdater.Instance;
        }

        void IFancyBackgroundListener.ChangeBackground(string filename) {
            var backgroundContent = BackgroundContent;
            FancyBackgroundManager.UpdateBackground(this, ref backgroundContent);
            if (!ReferenceEquals(backgroundContent, BackgroundContent)) {
                BackgroundContent = backgroundContent;
            }
        }

        private HwndSourceHook _hook;
        private bool _loaded;

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            FancyBackgroundManager.Instance.AddListener(this);
            AboutHelper.Instance.PropertyChanged += About_PropertyChanged;
            UpdateAboutIsNew();

            try {
                _hook = HandleMessages;
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(_hook);
            } catch (Exception) {
                Logging.Warning("Can’t add one-instance hook");
                _hook = null;
            }
        }

        private void UpdateAboutIsNew() {
            TitleLinks.FirstOrDefault(x => x.DisplayName == "about")?
                      .SetNew(AboutHelper.Instance.HasNewImportantTips || AboutHelper.Instance.HasNewReleaseNotes);
            MenuLinkGroups.SelectMany(x => x.Links)
                          .FirstOrDefault(x => x.DisplayName == "Release Notes")?
                          .SetNew(AboutHelper.Instance.HasNewReleaseNotes);
            MenuLinkGroups.SelectMany(x => x.Links)
                          .FirstOrDefault(x => x.DisplayName == "Important Tips")?
                          .SetNew(AboutHelper.Instance.HasNewImportantTips);
        }

        private void About_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(AboutHelper.HasNewReleaseNotes) || e.PropertyName == nameof(AboutHelper.HasNewImportantTips)) {
                UpdateAboutIsNew();
            }
        }

        private void MainWindow_OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            FancyBackgroundManager.Instance.RemoveListener(this);
            AboutHelper.Instance.PropertyChanged -= About_PropertyChanged;

            if (_hook == null) return;

            try {
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.RemoveHook(_hook);
            } catch (Exception) {
                Logging.Warning("Can’t remove one-instance hook");
            }

            _hook = null;
        }

        private IntPtr HandleMessages(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (message != EntryPoint.SecondInstanceMessage) return IntPtr.Zero;

            var data = EntryPoint.ReceiveSomeData();
            HandleMessagesAsync(data);

            BringToFront();
            return IntPtr.Zero;
        }

        private async void HandleMessagesAsync(IEnumerable<string> data) {
            await Task.Delay(1);
            foreach (var filename in data) {
                await _argumentsHandler.ProcessArgument(filename);
                await Task.Delay(1);
            }
        }

        private void MainWindow_OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(DataFormats.UnicodeText)) return;

            Focus();

            var data = e.Data.GetData(DataFormats.FileDrop) as string[] ??
                       (e.Data.GetData(DataFormats.UnicodeText) as string)?.Split('\n')
                                                                           .Select(x => x.Trim())
                                                                           .Select(x => x.Length > 1 && x.StartsWith("\"") && x.EndsWith("\"")
                                                                                   ? x.Substring(1, x.Length - 2) : x);
            Dispatcher.InvokeAsync(() => ProcessDroppedFiles(data));
        }

        private void MainWindow_OnDragEnter(object sender, DragEventArgs e) {
            if (e.AllowedEffects.HasFlag(DragDropEffects.All) &&
                (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText))) {
                e.Effects = DragDropEffects.All;
            }
        }

        private async void ProcessDroppedFiles(IEnumerable<string> files) {
            if (files == null) return;
            foreach (var filename in files) {
                await _argumentsHandler.ProcessArgument(filename);
            }
        }

        private void MainWindow_OnClosed(object sender, EventArgs e) {
            Application.Current.Shutdown();
        }

        private async void MainWindow_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Drive.QuickSwitches) return;

            await Task.Delay(10);
            if (e.Handled) return;

            if (Popup.IsOpen) {
                Popup.IsOpen = false;
            } else if (_openOnNext) {
                if (Popup.Child == null) {
                    Popup.Child = new QuickSwitchesBlock();
                    AcSettingsHolder.ControlsPresetLoading += Controls_PresetLoading;
                }

                Popup.IsOpen = true;
                Popup.Focus();
            }

            e.Handled = true;
        }

        private async void Controls_PresetLoading(object sender, EventArgs e) {
            // shitty fix, but it works (?)
            Popup.StaysOpen = true;
            await Task.Delay(1);
            Popup.StaysOpen = false;
        }

        private void MainWindow_OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            if (Popup.IsOpen) {
                e.Handled = true;
            }
        }

        private bool _openOnNext;

        private void MainWindow_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            _openOnNext = !Popup.IsOpen;
            if (Popup.IsOpen) {
                // Popup.IsOpen = false;
                // e.Handled = true;
            }
        }

        private class InnerPopupHeightConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value.AsDouble() / OptionScale - 2d;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter PopupHeightConverter { get; } = new InnerPopupHeightConverter();
    }
}
