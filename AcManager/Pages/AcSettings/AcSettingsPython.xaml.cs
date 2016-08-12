using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsPython : ILoadableContent {
        public class ViewModel : NotifyPropertyChanged {
            internal ViewModel() { }

            public PythonSettings Python => AcSettingsHolder.Python;

            public FormsSettings Forms => AcSettingsHolder.Forms;

            public IUserPresetable Presets => AcSettingsHolder.AppsPresets;

            public AcEnabledOnlyCollection<PythonAppObject> Apps => PythonAppsManager.Instance.EnabledOnlyCollection;
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return PythonAppsManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            PythonAppsManager.Instance.EnsureLoaded();
        }

        private ViewModel Model => (ViewModel)DataContext;
        private bool _ignore;

        public void Initialize() {
            DataContext = new ViewModel();
            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control))
            });

            UpdateListBox();
            Model.Python.PropertyChanged += Python_PropertyChanged;
        }

        private void UpdateListBox() {
            _ignore = true;
            EnabledAppsListBox.SelectedItems.Clear();
            foreach (var item in Model.Apps.Where(x => Model.Python.IsActivated(x.Id)).ToList()) {
                EnabledAppsListBox.SelectedItems.Add(item);
            }
            _ignore = false;
        }

        private void Python_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(PythonSettings.Apps)) {
                UpdateListBox();
            }
        }

        private void EnabledAppsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_ignore) return;
            foreach (var item in Model.Apps.Where(x => x.Enabled)) {
                Model.Python.SetActivated(item.Id, EnabledAppsListBox.SelectedItems.Contains(item));
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Python.PropertyChanged -= Python_PropertyChanged;
        }
    }
}
