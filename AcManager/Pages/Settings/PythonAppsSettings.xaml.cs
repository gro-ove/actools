using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Settings {
    public partial class PythonAppsSettings : ILoadableContent, ILocalKeyBindings, IContentLoader {
        public PythonAppsSettings() {
            KeyBindingsController = new LocalKeyBindingsController(this);
            InputBindings.Add(new InputBinding(new DelegateCommand(() => {
                Model.SelectedApp?.ViewInExplorerCommand.Execute(null);
            }), new KeyGesture(Key.F, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(new DelegateCommand(() => {
                Model.SelectedApp?.ReloadCommand.Execute(null);
            }), new KeyGesture(Key.R, ModifierKeys.Control)));
        }

        public async Task LoadAsync(CancellationToken cancellationToken) {
            await PythonAppsManager.Instance.EnsureLoadedAsync();
            foreach (var appObject in PythonAppsManager.Instance.EnabledOnly) {
                if (cancellationToken.IsCancellationRequested) break;
                await Task.Run(() => appObject.GetAppConfigs());
            }
        }

        public void Load() {
            PythonAppsManager.Instance.EnsureLoaded();
        }

        public void Initialize() {
            InitializeComponent();
            DataContext = new ViewModel();
            Model.PropertyChanged += OnModelPropertyChanged;
            ConfigsTab.ContentLoader = this;
            SetKeyboardInputs();
            UpdateConfigsTabs();
        }

        private void SetKeyboardInputs() {
            KeyBindingsController.Set(Model.SelectedAppConfigs.SelectMany(x => x.Sections).SelectMany().OfType<PythonAppConfigKeyValue>());
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Model.SelectedAppConfigs)) {
                SetKeyboardInputs();
                UpdateConfigsTabs();
            }
        }

        private void UpdateConfigsTabs() {
            var links = ConfigsTab.Links;
            links.Clear();

            if (Model.SelectedAppConfigs.Count > 1) {
                foreach (var config in Model.SelectedAppConfigs) {
                    links.Add(new Link {
                        DisplayName = config.DisplayName,
                        Key = config.DisplayName
                    });
                }
                ConfigsTab.LinksMargin = new Thickness(0, 0, 0, 4);
                ConfigsTab.SelectedSource = ConfigsTab.Links.FirstOrDefault()?.Source;
            } else {
                ConfigsTab.LinksMargin = new Thickness(0, 0, 0, -16);
                ConfigsTab.SelectedSource = new Uri(Model.SelectedAppConfigs.First().DisplayName, UriKind.Relative);
            }
        }

        public Task<object> LoadContentAsync(Uri uri, CancellationToken cancellationToken) {
            return Task.FromResult(LoadContent(uri));
        }

        public object LoadContent(Uri uri) {
            var config = Model.SelectedAppConfigs.FirstOrDefault(x => x.DisplayName == uri.OriginalString);
            return config?.IsSingleSection == true
                    ? new ContentControl {
                        ContentTemplate = (DataTemplate)FindResource("PythonAppConfig.SectionTemplate.NoHeader"),
                        Content = config
                    }
                    : new ContentControl {
                        ContentTemplate = (DataTemplate)FindResource("PythonAppConfig.NoHeader"),
                        Content = config
                    };
        }

        private ViewModel Model => (ViewModel)DataContext;

        private void OnLoaded(object sender, RoutedEventArgs e) {}

        public class ViewModel : NotifyPropertyChanged {
            public ConfigurableAppsCollection Apps { get; }

            private StoredValue _selectedAppId = Stored.Get("__PythonAppsSettingsPage.Selected");

            public ViewModel() {
                Apps = new ConfigurableAppsCollection(PythonAppsManager.Instance.WrappersList);
                SelectedApp = Apps.GetByIdOrDefault(_selectedAppId.Value) ?? Apps.FirstOrDefault();
            }

            private PythonAppObject _selectedApp;

            [CanBeNull]
            public PythonAppObject SelectedApp {
                get => _selectedApp;
                set {
                    if (Equals(value, _selectedApp)) return;
                    _selectedApp = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedAppConfigs));
                    _selectedAppId.Value = value?.Id;
                }
            }

            public PythonAppConfigs SelectedAppConfigs => _selectedApp.GetAppConfigs();
        }

        public class ConfigurableAppsCollection : WrappedFilteredCollection<AcItemWrapper, PythonAppObject> {
            public ConfigurableAppsCollection([NotNull] IAcWrapperObservableCollection collection) : base((IReadOnlyList<AcItemWrapper>)collection) {
                collection.WrappedValueChanged += OnWrappedValueChanged;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            private void OnWrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
                Refresh((AcItemWrapper)sender);
            }

            protected override PythonAppObject Wrap(AcItemWrapper source) {
                return (PythonAppObject)source.Value;
            }

            protected override bool Test(AcItemWrapper source) {
                return source.IsLoaded && source.Value.Enabled && ((PythonAppObject)source.Value).GetAppConfigs().Count > 0;
            }
        }

        public LocalKeyBindingsController KeyBindingsController { get; }
    }
}
