using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Settings {
    public partial class SettingsShadersPatch : ILocalKeyBindings {
        public static bool IsCustomShadersPatchInstalled() {
            return Directory.Exists(Path.Combine(AcRootDirectory.Instance.RequireValue, "extension", "config"));
        }

        public SettingsShadersPatch() {
            KeyBindingsController = new LocalKeyBindingsController(this);
            /*InputBindings.Add(new InputBinding(new DelegateCommand(() => {
                Model.SelectedApp?.ViewInExplorerCommand.Execute(null);
            }), new KeyGesture(Key.F, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(new DelegateCommand(() => {
                Model.SelectedApp?.ReloadCommand.Execute(null);
            }), new KeyGesture(Key.R, ModifierKeys.Control)));*/

            InitializeComponent();
            DataContext = new ViewModel(false);
            Model.PropertyChanged += OnModelPropertyChanged;
            SetKeyboardInputs();
            UpdateConfigsTabs();
            this.OnActualUnload(() => { Model?.Dispose(); });
        }

        private void SetKeyboardInputs() {
            KeyBindingsController.Set(Model.SelectedConfig?.Sections.SelectMany().OfType<PythonAppConfigKeyValue>());
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Model.SelectedConfig)) {
                SetKeyboardInputs();
                UpdateConfigsTabs();
            }
        }

        private void UpdateConfigsTabs() {
            try {
                ConfigTab.Content = Model.SelectedConfig;
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private ViewModel Model => (ViewModel)DataContext;

        public enum Mode {
            NoShadersPatch,
            NoConfigs,
            NoFittingConfigs,
            EverythingIsFine
        }

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            private readonly bool _isLive;
            private StoredValue _selectedConfigId = Stored.Get("__CspAppsSettingsPage.Selected");

            public ViewModel(bool isLive) {
                _isLive = isLive;
                _dir = Path.Combine(AcRootDirectory.Instance.RequireValue, "extension", "config");
                _directoryWatcher = Directory.Exists(_dir) ? SimpleDirectoryWatcher.WatchDirectory(_dir, OnDirectoryUpdate) : null;
                CreateConfigs();
            }

            private DateTime _lastSaved;

            private void SaveConfigs() {
                if (!Directory.Exists(_dir)) return;
                _lastSaved = DateTime.Now;
                foreach (var config in _configs) {
                    config.Save();
                }
            }

            private readonly Busy _configsSaveBusy = new Busy();

            private void OnConfigsValueChanged(object sender, EventArgs e) {
                _configsSaveBusy.DoDelay(SaveConfigs, 100);
            }

            private readonly Busy _busy = new Busy(true);

            private void CreateConfigs() {
                if (Configs != null) {
                    Configs.ValueChanged -= OnConfigsValueChanged;
                    Configs.Dispose();
                }

                if (!Directory.Exists(_dir)) {
                    Mode = Mode.NoShadersPatch;
                    Configs = null;
                    return;
                }

                FileUtils.EnsureDirectoryExists(Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "extension"));
                var anyConfigFound = false;
                Configs = new PythonAppConfigs(new PythonAppConfigParams(_dir) {
                    FilesRelativeDirectory = AcRootDirectory.Instance.Value ?? _dir,
                    ScanFunc = d => Directory.GetFiles(d, "*.ini").Where(x => !Path.GetFileName(x).StartsWith(@"data_")),
                    ConfigFactory = (p, f) => {
                        var fileName = Path.GetFileName(f);
                        if (fileName == null) return null;
                        anyConfigFound = true;
                        var userEditedFile = Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "extension", fileName);

                        var cfg = PythonAppConfig.Create(p, f, true, userEditedFile);
                        if (_isLive && cfg.Sections.GetByIdOrDefault("ℹ")?.GetByIdOrDefault("LIVE_SUPPORT")?.Value == @"0") {
                            return null;
                        }

                        return string.IsNullOrWhiteSpace(cfg.ShortDescription) ? null : cfg;
                    },
                    SaveOnlyNonDefault = true,
                    Flags = new Dictionary<string, string> {
                        [@"IS_LIVE__"] = _isLive.As<string>()
                    }
                });

                if (Configs.Count > 0) {
                    Mode = Mode.EverythingIsFine;
                } else if (anyConfigFound) {
                    Mode = Mode.NoFittingConfigs;
                } else {
                    Mode = Mode.NoConfigs;
                }

                SelectedConfig = Configs.GetByIdOrDefault(_selectedConfigId.Value) ?? Configs.FirstOrDefault();
                Configs.ValueChanged += OnConfigsValueChanged;
            }

            private void OnDirectoryUpdate(string filename) {
                _busy.DoDelay(() => {
                    if ((DateTime.Now - _lastSaved).TotalSeconds < 3d) return;
                    CreateConfigs();
                }, 300);
            }

            private readonly string _dir;
            private readonly IDisposable _directoryWatcher;

            private Mode _mode;

            public Mode Mode {
                get => _mode;
                set => Apply(value, ref _mode);
            }

            private PythonAppConfigs _configs;

            [CanBeNull]
            public PythonAppConfigs Configs {
                get => _configs;
                set {
                    if (Equals(value, _configs)) return;
                    _configs = value;
                    OnPropertyChanged();
                }
            }

            private PythonAppConfig _selectedConfig;

            [CanBeNull]
            public PythonAppConfig SelectedConfig {
                get => _selectedConfig;
                set {
                    if (Equals(value, _selectedConfig)) return;
                    _selectedConfig = value;
                    if (value?.Id != null) {
                        _selectedConfigId.Value = value.Id;
                    }
                    OnPropertyChanged();
                }
            }

            public void Dispose() {
                _directoryWatcher?.Dispose();
                Configs?.Dispose();
            }
        }

        public LocalKeyBindingsController KeyBindingsController { get; }

        public static ICommand GetShowSettingsCommand() {
            return new AsyncCommand(() => {
                var dlg = new ModernDialog {
                    ShowTitle = false,
                    Content = new SettingsShadersPatchPopup(),
                    MinHeight = 400,
                    MinWidth = 450,
                    MaxHeight = 99999,
                    MaxWidth = 700,
                    Padding = new Thickness(0),
                    ButtonsMargin = new Thickness(8),
                    SizeToContent = SizeToContent.Manual,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    BlurBackground = true,
                    ShowTopBlob = false,
                    Topmost = true,
                    Title = "Custom Shaders Patch settings",
                    LocationAndSizeKey = @".CustomShadersPatchDialog",
                    Owner = null,
                    Buttons = new Control[0],
                    BorderThickness = new Thickness(0),
                    Opacity = 0.9,
                    BorderBrush = new SolidColorBrush(Colors.Transparent)
                };

                dlg.Background = new SolidColorBrush(((Color)dlg.FindResource("WindowBackgroundColor")).SetAlpha(200));

                return dlg.ShowAndWaitAsync();
            });
        }
    }
}