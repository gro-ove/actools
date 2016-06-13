using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AcManager.Controls.Helpers;
using AcManager.Controls.UserControls;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.About;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
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
            _cancelled = false;

            if (AppArguments.Values.Any()) {
                ProcessArguments();
            }

            if (_testGameDialog != null) {
                Logging.Write("[MAINWINDOW] Testing mode");
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
                Logging.Write("[MAINWINDOW] Input: " + arg);

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
                Logging.Write("[MAINWINDOW] Cancelled");
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

        private void MainWindow_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Drive.QuickSwitches) return;

            if (!Popup.IsOpen && Popup.Child == null) {
                Popup.Child = new QuickSwitchesBlock();
                AcSettingsHolder.Controls.PresetLoading += Controls_PresetLoading;
            }

            Popup.IsOpen = !Popup.IsOpen;
            if (Popup.IsOpen) {
                Popup.Focus();
                e.Handled = true;
            }
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

        private void MainWindow_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            if (Popup.IsOpen) {
                Popup.IsOpen = false;
                e.Handled = true;
            }
        }
    }
}
