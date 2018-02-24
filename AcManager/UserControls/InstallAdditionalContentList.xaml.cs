using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.UserControls {
    public partial class InstallAdditionalContentList {
        public ListCollectionView DownloadListView { get; }

        public InstallAdditionalContentList() {
            DownloadListView = new BetterListCollectionView(ContentInstallationManager.Instance.DownloadList);
            DownloadListView.SortDescriptions.Add(new SortDescription(nameof(ContentInstallationEntry.AddedDateTime), ListSortDirection.Descending));

            DataContext = this;
            InitializeComponent();
            // ArgumentsHandler.HandlePasteEvent(this);
            /*Buttons = new[] {
                CreateExtraDialogButton("Remove completed", ContentInstallationManager.Instance.RemoveCompletedCommand),
                IsAlone ? VisualStyleElement.Window.CloseButton : CreateCloseDialogButton(UiStrings.Toolbar_Hide, true, false, MessageBoxResult.None)
            };*/

            this.AddWidthCondition(x => (x - 104).Clamp(120, 240)).Add(x => Resources[@"ButtonsRowWidth"] = x);
            Plugins.Ready += OnPluginsReady;
        }

        private async void OnPluginsReady(object sender, EventArgs e) {
            var list = ContentInstallationManager.Instance.DownloadList.Where(
                    x => x.RetryCommand.IsAbleToExecute && (x.State == ContentInstallationEntryState.WaitingForConfirmation || x.IsFailed)).ToList();
            Logging.Debug(list.Count);

            await Task.Yield();
            foreach (var entry in list) {
                Logging.Debug(entry.DisplayName);
                entry.RetryCommand.Execute();
            }
        }

        public PluginsRequirement Plugins { get; } = new PluginsRequirement(KnownPlugins.SevenZip);

        private void OnPasswordBoxKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
                (((FrameworkElement)sender).DataContext as ContentInstallationEntry)?.ApplyPasswordCommand.Execute();
            }
        }

        private void OnItemMouseUp(object sender, MouseButtonEventArgs e) {
            if (((FrameworkElement)sender).DataContext is ContentInstallationEntry item && !ReferenceEquals(ItemsListBox.SelectedItem, item)) {
                var control = ItemsListBox.GetItemVisual(item);
                if (control != null) {
                    var button = control.FindVisualChildren<Button>().FirstOrDefault(x => x.IsMouseOverElement());
                    button?.Command?.Execute(button.CommandParameter);
                }

                ItemsListBox.SelectedItem = item;
            }
        }

        private void OnItemMouseDown(object sender, MouseButtonEventArgs e) { }
    }
}