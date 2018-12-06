using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
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
        private readonly Busy _defaultSetupItemBusy = new Busy();

        private void AllowedTyres_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            _legalTyresBusy.Do(() => {
                var server = Model?.SelectedObject;
                if (server == null) return;
                server.LegalTyres = AllowedTyres.SelectedItems.OfType<ServerPresetObject.TyresItem>().ToList();
            });
        }

        private void FixedSetups_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            _defaultSetupItemBusy.Do(() => {
                var server = Model?.SelectedObject;
                if (server == null) return;
                var item = e.AddedItems.OfType<ServerPresetObject.SetupItem>().FirstOrDefault();
                server.DefaultSetupItem = item;
                if (SetupItems.SelectedItems.Count > 1) {
                    SetupItems.SelectedItems.Clear();
                    SetupItems.SelectedItems.Add(item);
                }
            });
        }

        [CanBeNull]
        private ServerPresetObject _lastSubscribedTo;

        private void AllowedTyres_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (_lastSubscribedTo != Model?.SelectedObject) {
                _lastSubscribedTo.UnsubscribeWeak(OnServerPropertyChanged);
                _lastSubscribedTo = Model?.SelectedObject;
                _lastSubscribedTo.SubscribeWeak(OnServerPropertyChanged);
            }

            SyncLegalTyres();
        }

        private void FixedSetups_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            SyncDefaultSetupItem();
        }

        private void OnServerPropertyChanged(object s, PropertyChangedEventArgs ev) {
            if (ev.PropertyName == nameof(Model.SelectedObject.LegalTyres)) {
                SyncLegalTyres();
            }

            if (ev.PropertyName == nameof(Model.SelectedObject.DefaultSetupItem)) {
                SyncDefaultSetupItem();
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

        private void SyncDefaultSetupItem() {
            _defaultSetupItemBusy.Do(() => {
                SetupItems.SelectedItems.Clear();
                var item = Model?.SelectedObject.DefaultSetupItem;
                if (item != null) {
                    SetupItems.SelectedItems.Add(item);
                }
            });
        }
    }
}