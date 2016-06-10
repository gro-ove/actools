using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsPython : ILoadableContent {
        public class AcPythonViewModel : NotifyPropertyChanged {
            internal AcPythonViewModel() { }

            public AcSettingsHolder.PythonSettings Python => AcSettingsHolder.Python;

            public AcLoadedOnlyCollection<PythonAppObject> Apps => PythonAppsManager.Instance.LoadedOnlyCollection;
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return PythonAppsManager.Instance.EnsureLoadedAsync();
        }

        public void Load() {
            PythonAppsManager.Instance.EnsureLoaded();
        }

        private AcPythonViewModel Model => (AcPythonViewModel)DataContext;
        private bool _ignore;

        public void Initialize() {
            InitializeComponent();
            DataContext = new AcPythonViewModel();

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
            if (e.PropertyName == nameof(AcSettingsHolder.PythonSettings.Apps)) {
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
