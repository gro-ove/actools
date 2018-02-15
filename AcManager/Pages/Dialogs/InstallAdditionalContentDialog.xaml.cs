using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.ContentInstallation.Entries;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;

namespace AcManager.Pages.Dialogs {
    public class AdditionalContentEntryTemplateSelectorInner : DataTemplateSelector {
        public DataTemplate BasicTemplate { get; set; }
        public DataTemplate TrackTemplate { get; set; }
        public DataTemplate FontTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            switch (item) {
                case TrackContentEntry _:
                    return TrackTemplate;
                case FontContentEntry _:
                    return FontTemplate;
                default:
                    return BasicTemplate;
            }
        }
    }

    public partial class InstallAdditionalContentDialog {
        private static InstallAdditionalContentDialog _dialog;

        public static void Initialize() {
            ContentInstallationManager.Instance.TaskAdded += OnTaskAdded;
            ContentInstallationManager.Instance.DownloadList.CollectionChanged += OnQueueChanged;
        }

        private static readonly Busy TaskAddedBusy = new Busy();
        private static readonly Busy QueueChangedBusy = new Busy();

        private static void OnTaskAdded(object sender, EventArgs e) {
            TaskAddedBusy.DoDelay(ShowInstallDialog, 100);
        }

        private static void OnQueueChanged(object o, NotifyCollectionChangedEventArgs a) {
            QueueChangedBusy.DoDelay(() => {
                if (ContentInstallationManager.Instance.DownloadList.Count == 0 && _dialog?.IsActive != true) {
                    CloseInstallDialog();
                }
            }, 100);
        }

        public static void ShowInstallDialog() {
            if (_dialog == null) {
                _dialog = new InstallAdditionalContentDialog();

                if (Application.Current?.MainWindow is MainWindow) {
                    _dialog.Owner = Application.Current?.MainWindow;
                    _dialog.ShowInTaskbar = false;
                    _dialog.WindowStyle = WindowStyle.ToolWindow;
                }

                _dialog.Show();
                _dialog.Closed += (sender, args) => {
                    if (IsAlone) {
                        // ContentInstallationManager.Instance.Cancel();
                    }

                    _dialog = null;
                };
            }
        }

        private static void CloseInstallDialog() {
            _dialog?.Close();
        }

        private static bool IsAlone => Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault()?.IsVisible != true;

        public ListCollectionView DownloadListView { get; }

        private InstallAdditionalContentDialog() {
            DownloadListView = new BetterListCollectionView(ContentInstallationManager.Instance.DownloadList);
            DownloadListView.SortDescriptions.Add(new SortDescription(nameof(ContentInstallationEntry.AddedDateTime), ListSortDirection.Descending));

            DataContext = this;
            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(new DelegateCommand(ArgumentsHandler.OnPaste), new KeyGesture(Key.V, ModifierKeys.Control)),
            });
            Buttons = new[] {
                CreateExtraDialogButton("Remove completed", ContentInstallationManager.Instance.RemoveCompletedCommand),
                IsAlone ? CloseButton : CreateCloseDialogButton(UiStrings.Toolbar_Hide, true, false, MessageBoxResult.None)
            };

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

        private void OnClosed(object sender, EventArgs e) {}

        private void OnDrop(object sender, DragEventArgs e) {
            ArgumentsHandler.OnDrop(sender, e);
        }

        private void OnDragEnter(object sender, DragEventArgs e) {
            ArgumentsHandler.OnDragEnter(sender, e);
        }

        private void OnPasswordBoxKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
                (((FrameworkElement)sender).DataContext as ContentInstallationEntry)?.ApplyPasswordCommand.Execute();
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
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

        private void OnItemMouseDown(object sender, MouseButtonEventArgs e) {}
    }
}
