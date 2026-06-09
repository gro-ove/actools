using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AcManager.Pages.Drive;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetAdvanced {
        public ServerPresetAdvanced() {
            InitializeComponent();
        }

        [CanBeNull]
        private SelectedPage.ViewModel Model => DataContext as SelectedPage.ViewModel;

        private readonly Busy _legalTyresBusy = new Busy();

        [CanBeNull]
        private ServerPresetObject _lastSubscribedTo;

        private void AllowedTyres_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            _legalTyresBusy.Do(() => {
                var server = Model?.SelectedObject;
                if (server == null) return;
                server.LegalTyres = AllowedTyres.SelectedItems.OfType<ServerPresetObject.TyresItem>().ToList();
            });
        }

        private void AllowedTyres_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (_lastSubscribedTo != Model?.SelectedObject) {
                _lastSubscribedTo.UnsubscribeWeak(OnServerPropertyChanged);
                _lastSubscribedTo = Model?.SelectedObject;
                _lastSubscribedTo.SubscribeWeak(OnServerPropertyChanged);
            }

            SyncLegalTyres();
        }

        private void OnServerPropertyChanged(object s, PropertyChangedEventArgs ev) {
            if (ev.PropertyName == nameof(Model.SelectedObject.LegalTyres)) {
                SyncLegalTyres();
            }
        }

        private void SyncLegalTyres() {
            _legalTyresBusy.Do(() => {
                AllowedTyres.SelectedItems.Clear();
                var tyres = Model?.SelectedObject.LegalTyres;
                if (tyres != null) {
                    foreach (var tyre in tyres) {
                        AllowedTyres.SelectedItems.Add(tyre);
                    }
                }
            });
        }

        private void OnTestSetupClick(object sender, RoutedEventArgs e) {
            if ((sender as FrameworkElement)?.DataContext is ServerPresetObject.SetupItem item) {
                var car = CarsManager.Instance.GetById(item.CarId);
                if (car == null) {
                    MessageDialog.Show($"Car with ID {item.CarId} is missing");
                    return;
                }
                QuickDrive.RunAsync(CarsManager.Instance.GetById(item.CarId), track: Model?.Track, carSetupFilename: item.Filename).Ignore();
            }
        }
    }
}