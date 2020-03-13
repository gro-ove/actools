using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers.AcSettings;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsControls_Wheel_Patch : INotifyPropertyChanged, ILoadableContent {
        public void Initialize() {
            InitializeComponent();
            this.AddWidthCondition(1200).Add(x =>
                    MainGrid.FindVisualChild<SpacingUniformGrid>()?.SetValue(SpacingUniformGrid.ColumnsProperty, x ? 2 : 1));
        }

        private AcSettingsControls.ViewModel Model => (AcSettingsControls.ViewModel)DataContext;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            var model = Model;
            if (model == null) return;

            model.IsInSystemBindingsSection = true;
            Unloaded += OnUnloaded;

            void OnUnloaded(object o, RoutedEventArgs args) {
                Unloaded -= OnUnloaded;
                model.IsInSystemBindingsSection = false;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            AcSettingsHolder.Controls.ClearWaiting();
        }

        public event PropertyChangedEventHandler PropertyChanged {
            add { }
            remove { }
        }

        public Task LoadAsync(CancellationToken cancellationToken) {
            return Task.Delay(0);
        }

        public void Load() { }
    }
}