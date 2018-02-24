using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Dialogs {
    public partial class InstallAdditionalContentDialog {
        private static InstallAdditionalContentDialog _dialog;

        public static void Initialize() {
            // ContentInstallationManager.Instance.TaskAdded += OnTaskAdded;
            // ContentInstallationManager.Instance.DownloadList.CollectionChanged += OnQueueChanged;
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

                if (Application.Current?.MainWindow is MainWindow m && m.IsVisible) {
                    try {
                        _dialog.Owner = m;
                    } catch (InvalidOperationException) { }
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

        private InstallAdditionalContentDialog() {
            DataContext = this;
            InitializeComponent();
            ArgumentsHandler.HandlePasteEvent(this);
            Buttons = new[] {
                CreateExtraDialogButton("Remove completed", ContentInstallationManager.Instance.RemoveCompletedCommand),
                IsAlone ? CloseButton : CreateCloseDialogButton(UiStrings.Toolbar_Hide, true, false, MessageBoxResult.None)
            };
        }

        private void OnClosed(object sender, EventArgs e) { }

        private void OnDrop(object sender, DragEventArgs e) {
            ArgumentsHandler.OnDrop(sender, e);
        }

        private void OnDragEnter(object sender, DragEventArgs e) {
            ArgumentsHandler.OnDragEnter(sender, e);
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;
            }
        }
    }
}