using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Controls.Helpers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;

namespace AcManager.Pages.Settings {
    public partial class SettingsShadersPatchPopup : ILocalKeyBindings, IContentLoader {
        public SettingsShadersPatchPopup() {
            KeyBindingsController = new LocalKeyBindingsController(this);
            InitializeComponent();
            DataContext = PatchSettingsModel.Create().SetupWatcher();
            Model.PropertyChanged += OnModelPropertyChanged;
            SetKeyboardInputs();
            UpdateConfigsTabs();
            Tabs.ContentLoader = this;
            this.OnActualUnload(() => {
                Model?.Dispose();
                ResetConfigListeners();
            });
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Model.Configs)) {
                UpdateConfigsTabs();
            }
        }

        public Task<object> LoadContentAsync(Uri uri, CancellationToken cancellationToken) {
            return Task.FromResult(LoadContent(uri));
        }

        public object LoadContent(Uri uri) {
            var config = Model.Configs?.FirstOrDefault(x => x.Id == uri.OriginalString);
            var ret = new ContentControl {
                ContentTemplate = (DataTemplate)FindResource("PythonAppConfig.Compact.NoHeader"),
                Content = null,
                Visibility = Visibility.Collapsed
            };
            Task.Delay(50).ContinueWith(r => ActionExtension.InvokeInMainThreadAsync(() => {
                ret.Content = config;
                ret.Visibility = Visibility.Visible;
            }));
            return ret;
        }

        private void OnConfigPropertyChanged(object sender, PropertyChangedEventArgs args) {
            foreach (var link in Tabs.Links) {
                if (link.DisplayName == ((PythonAppConfig)sender).DisplayName) {
                    link.Tag = ((PythonAppConfig)sender).IsActive ? "" : null;
                }
            }
        }

        private List<PythonAppConfig> _configs;

        private void ResetConfigListeners() {
            if (_configs != null) {
                foreach (var config in _configs) {
                    config.UnsubscribeWeak(OnConfigPropertyChanged);
                }
                _configs.Clear();
            }
        }

        private void UpdateConfigsTabs() {
            try {
                ResetConfigListeners();
                if (_configs == null) {
                    _configs = new List<PythonAppConfig>();
                }

                var links = Tabs.Links;
                links.Clear();
                Tabs.SelectedSource = null;

                if (Model.Configs != null) {
                    foreach (var config in Model.Configs) {
                        links.Add(new Link {
                            DisplayName = config.DisplayName,
                            Key = config.Id,
                            Tag = config.IsActive ? "" : null
                        });
                        _configs.Add(config);
                        config.SubscribeWeak(OnConfigPropertyChanged);
                    }
                    Tabs.SelectedSource = Tabs.Links.FirstOrDefault()?.Source;
                } else {
                    Tabs.SelectedSource = null;
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public LocalKeyBindingsController KeyBindingsController { get; }

        private void SetKeyboardInputs() {
            KeyBindingsController.Set(Model.SelectedPage?.Config?.SectionsOwn.SelectMany().OfType<PythonAppConfigKeyValue>());
        }

        private PatchSettingsModel Model => (PatchSettingsModel)DataContext;

        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.Key == Key.Tab) {
                var selected = Tabs.SelectedSource;
                Tabs.SelectedSource = (
                        User32.IsKeyPressed(Keys.LShiftKey) || User32.IsKeyPressed(Keys.RShiftKey) ?
                                Tabs.Links.Concat(Tabs.Links.TakeWhile(x => x.Source != selected)).LastOrDefault() :
                                Tabs.Links.SkipWhile(x => x.Source != selected).Skip(1).Concat(Tabs.Links).FirstOrDefault()
                        )?.Source ?? selected;
                e.Handled = true;
            } else if (e.Key >= Key.D1 && e.Key <= Key.D9 &&
                    (User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey))) {
                Tabs.SelectedSource = Tabs.Links.ElementAtOrDefault(e.Key - Key.D1)?.Source ?? Tabs.SelectedSource;
                e.Handled = true;
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e) {
            ((Button)sender).GetParent<Window>()?.Close();
        }
    }
}