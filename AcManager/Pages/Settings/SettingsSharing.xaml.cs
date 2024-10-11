using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Settings {
    public partial class SettingsSharing {
        public class SharingViewModel : NotifyPropertyChanged {
            public SettingsHolder.SharingSettings Sharing => SettingsHolder.Sharing;
            public BetterObservableCollection<SharedEntry> History => SharingHelper.Instance.History;
        }

        public SettingsSharing() {
            InitializeComponent();
            DataContext = new SharingViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        public SharingViewModel Model => (SharingViewModel)DataContext;

        private void OnScrollMouseWheel(object sender, MouseWheelEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void OnHistoryContextMenu(object sender, MouseButtonEventArgs e) {
            if (HistoryDataGrid.SelectedValue is SharedEntry value && !string.IsNullOrEmpty(value.RemovalKey)) {
                e.Handled = true;
                new ContextMenu {
                    Items = {
                        new MenuItem {
                            Header = "Delete shared…",
                            Command = new AsyncCommand(async () => {
                                if (ModernDialog.ShowMessage($"Are you sure you want to remove {value.Id}?", "Remove shared entry?", MessageBoxButton.YesNo)
                                        == MessageBoxResult.Yes) {
                                    try {
                                        using (WaitingDialog.Create("Removing…")) {
                                            await InternalUtils.DeleteSharedEntryAsync(value.Id, value.RemovalKey, CmApiProvider.UserAgent);
                                        }
                                        SharingHelper.Instance.History.Remove(value);
                                        SharingHelper.Instance.SaveHistory();
                                    } catch (Exception ex) {
                                        NonfatalError.Notify("Failed to remove shared entry", ex);
                                    }
                                }
                            })
                        }
                    }
                }.IsOpen = true;
            }
        }

        private void OnHistoryDoubleClick(object sender, MouseButtonEventArgs e) {
            if (HistoryDataGrid.SelectedValue is SharedEntry value) {
                Process.Start(value.Url + "#noauto");
            }
        }
    }
}