using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Helpers;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.About;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Starters;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using Newtonsoft.Json;
using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using Path = System.IO.Path;

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
            LoadSize();

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

        private async void ProcessArguments() {
            if (OptionLiteModeSupported) {
                Visibility = Visibility.Hidden;
            }

            var cancelled = true;
            foreach (var arg in AppArguments.Values) {
                Logging.Write("[MAINWINDOW] Input: " + arg);
                if (await ProcessArgument(arg)) {
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
                Logging.Warning("Can't add one-instance hook");
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
                Logging.Warning("Can't remove one-instance hook");
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
                await ProcessArgument(filename);
                await Task.Delay(1);
            }
        }

        private void LoadSize() {
            Top = MathUtils.Clamp(ValuesStorage.GetDouble("MainWindow.Top", Top), 0.0, Screen.PrimaryScreen.Bounds.Height);
            Left = MathUtils.Clamp(ValuesStorage.GetDouble("MainWindow.Left", Top), 0.0, Screen.PrimaryScreen.Bounds.Width);
            Height = MathUtils.Clamp(ValuesStorage.GetDouble("MainWindow.Height", Top), MinHeight, Screen.PrimaryScreen.Bounds.Height);
            Width = MathUtils.Clamp(ValuesStorage.GetDouble("MainWindow.Width", Top), MinWidth, Screen.PrimaryScreen.Bounds.Width);
            WindowState = ValuesStorage.GetBool("MainWindow.Maximized") ? WindowState.Maximized : WindowState.Normal;
        }

        private void SaveSize() {
            if (WindowState == WindowState.Minimized) return;
            ValuesStorage.Set("MainWindow.Top", Top);
            ValuesStorage.Set("MainWindow.Left", Left);
            ValuesStorage.Set("MainWindow.Height", Height);
            ValuesStorage.Set("MainWindow.Width", Width);
            ValuesStorage.Set("MainWindow.Maximized", WindowState == WindowState.Maximized);
        }

        private async Task<string> LoadRemoveFile(string argument, string name) {
            using (var waiting = new WaitingDialog("Loading…")) {
                return await FlexibleLoader.LoadAsync(argument, name, waiting, waiting.CancellationToken);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="argument"></param>
        /// <returns>True if form should be shown</returns>
        private async Task<bool> ProcessArgument(string argument) {
            if (string.IsNullOrWhiteSpace(argument)) return true;

            if (argument.StartsWith(CustomUriSchemeHelper.UriScheme)) {
                return await ProcessUriRequest(argument);
            }

            if (argument.StartsWith("http", StringComparison.OrdinalIgnoreCase) || argument.StartsWith("https", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("ftp", StringComparison.OrdinalIgnoreCase)) {
                argument = await LoadRemoveFile(argument, null);
                if (string.IsNullOrWhiteSpace(argument)) return true;
            }

            try {
                if (!FileUtils.Exists(argument)) return true;
            } catch (Exception) {
                return true;
            }

            return await ProcessInputFile(argument);
        }

        /// <summary>
        /// </summary>
        /// <param name="uri"></param>
        /// <returns>True if form should be shown</returns>
        private async Task<bool> ProcessUriRequest(string uri) {
            if (!uri.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.OrdinalIgnoreCase)) return true;

            var request = uri.SubstringExt(CustomUriSchemeHelper.UriScheme.Length);
            Logging.Write("[MAINWINDOW] URI Request: " + request);

            string key, param;
            NameValueCollection query;

            {
                var splitted = request.Split(new[] { '/' }, 2);
                if (splitted.Length != 2) return false;

                key = splitted[0];
                param = splitted[1];

                var index = param.IndexOf('?');
                if (index != -1) {
                    query = HttpUtility.ParseQueryString(param.SubstringExt(index + 1));
                    param = param.Substring(0, index);
                } else {
                    query = null;
                }
            }

            switch (key) {
                case "quickdrive":
                    var preset = Convert.FromBase64String(param).ToUtf8String();
                    if (!QuickDrive.RunSerializedPreset(preset)) {
                        NonfatalError.Notify("Can't start race", "Make sure required car & track are installed and available.");
                    }
                    break;

                case "race":
                    ModernDialog.ShowMessage("Here!");
                    var raceIni = Convert.FromBase64String(param).ToUtf8String();
                    await GameWrapper.StartAsync(new Game.StartProperties {
                        PreparedConfig = IniFile.Parse(raceIni)
                    });
                    break;

                case "open":
                case "install":
                    var address = Convert.FromBase64String(param).ToUtf8String();
                    var path = await LoadRemoveFile(address, query?.Get("name"));
                    if (string.IsNullOrWhiteSpace(path)) return true;

                    try {
                        if (!FileUtils.Exists(path)) return true;
                    } catch (Exception) {
                        return true;
                    }

                    return await ProcessInputFile(path);
            }

            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>True if form should be shown</returns>
        private async Task<bool> ProcessInputFile(string filename) {
            var isDirectory = FileUtils.IsDirectory(filename);
            if (!isDirectory && filename.EndsWith(".acreplay", StringComparison.OrdinalIgnoreCase) ||
                Path.GetDirectoryName(filename)?.Equals(FileUtils.GetReplaysDirectory(), StringComparison.OrdinalIgnoreCase) == true) {
                Game.Start(AcsStarterFactory.Create(),
                        new Game.StartProperties(new Game.ReplayProperties {
                            Filename = filename
                        }));
            } else if (!isDirectory && filename.EndsWith(".kn5", StringComparison.OrdinalIgnoreCase)) {
                await CustomShowroomWrapper.StartAsync(filename);
            } else {
                try {
                    new InstallAdditionalContentDialog(filename).ShowDialog();
                } catch (Exception e) {
                    NonfatalError.Notify("Can't install additional content", e);
                }
            }

            return false;
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
                await ProcessArgument(filename);
            }
        }

        private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e) {
            SaveSize();
        }

        private void MainWindow_OnStateChanged(object sender, EventArgs e) {
            SaveSize();
        }

        private void MainWindow_OnClosed(object sender, EventArgs e) {
            Application.Current.Shutdown();
        }
    }
}
