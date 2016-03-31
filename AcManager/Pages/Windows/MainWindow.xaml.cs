using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Tools.About;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Starters;
using AcTools.Kn5Render.Kn5Render;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;
using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using Path = System.IO.Path;

namespace AcManager.Pages.Windows {
    public partial class MainWindow : IFancyBackgroundListener {
        public static bool OptionEnableManagerTabs = false;

        private readonly bool _cancelled;
        private readonly string _testGameDialog = null;

        public MainWindow() {
            var args = Environment.GetCommandLineArgs();

            _cancelled = false;
            foreach (var arg in args.Skip(1).Where(x => !x.StartsWith("-"))
                    .Union(args.SkipWhile(x => x != "-").Skip(1).Where(x => x.StartsWith("-")))) {
                Logging.Write("[MAINWINDOW] Input file: " + arg);
                ProcessInputFile(arg);
                _cancelled = true;
            }

            if (_testGameDialog != null) {
                Logging.Write("[MAINWINDOW] Testing mode");
                var ui = new GameDialog();
                ui.ShowDialogWithoutBlocking();
                ((IGameUi)ui).OnResult(JsonConvert.DeserializeObject<Game.Result>(FileUtils.ReadAllText(_testGameDialog)));
                _cancelled = true;
            }

            if (_cancelled) {
                Close();
                return;
            }

            InitializeComponent();
            DataContext = new MainWindowViewModel();
            LoadSize();

            if (OptionEnableManagerTabs) {
                foreach (var menuLinkGroup in TitleLinks) {
                    menuLinkGroup.IsEnabled = true;
                }
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
            public MainWindowViewModel() { }

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
            var aboutLink = TitleLinks.FirstOrDefault(x => x.DisplayName == "about") as TitleLink;
            if (aboutLink != null) {
                aboutLink.IsNew = AboutHelper.Instance.HasSomethingNew;
            }
        }

        private void About_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(AboutHelper.HasSomethingNew)) return;
            UpdateAboutIsNew();
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
            Task.Run(() => {
                foreach (var filename in data) {
                    ProcessInputFile(filename);
                }
            }).Forget();

            BringToFront();
            return IntPtr.Zero;
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

        private void ProcessInputFile(string filename) {
            if (filename == null || !File.Exists(filename)) return;

            if (filename.EndsWith(".acreplay", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetDirectoryName(filename)?.Equals(FileUtils.GetReplaysDirectory(), StringComparison.OrdinalIgnoreCase) == true) {
                Game.Start(AcsStarterFactory.Create(),
                        new Game.StartProperties(new Game.ReplayProperties {
                            Filename = filename
                        }));
            } else if (filename.EndsWith(".kn5", StringComparison.OrdinalIgnoreCase)) {
                using (var render = new Render(filename, 0, Render.VisualMode.BRIGHT_ROOM)) {
                    render.Form(1280, 720);
                }
            } else {
                try {
                    new InstallAdditionalContentDialog(filename).ShowDialog();
                } catch (Exception e) {
                    NonfatalError.Notify("Can't install additional content", e);
                }
            }
        }

        private void MainWindow_OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            Focus();
            foreach (var filename in (string[])e.Data.GetData(DataFormats.FileDrop)) {
                ProcessInputFile(filename);
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
