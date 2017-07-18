using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.ContentInstallation.Entries;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Pages.Dialogs {
    public class AdditionalContentEntryTemplateSelectorInner : DataTemplateSelector {
        public DataTemplate BasicTemplate { get; set; }

        public DataTemplate TrackTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is TrackContentEntry ? TrackTemplate : BasicTemplate;
        }
    }

    public partial class InstallAdditionalContentDialog {
        private static InstallAdditionalContentDialog _dialog;

        public static void Initialize() {
            ContentInstallationManager.Instance.TaskAdded += OnTaskAdded;
            ContentInstallationManager.Instance.Queue.CollectionChanged += OnQueueChanged;
        }

        private static readonly Busy TaskAddedBusy = new Busy();
        private static readonly Busy QueueChangedBusy = new Busy();

        private static void OnTaskAdded(object sender, EventArgs e) {
            TaskAddedBusy.DoDelay(ShowInstallDialog, 100);
        }

        private static void OnQueueChanged(object o, NotifyCollectionChangedEventArgs a) {
            QueueChangedBusy.DoDelay(() => {
                if (ContentInstallationManager.Instance.Queue.Count == 0 && _dialog?.IsActive != true) {
                    CloseInstallDialog();
                }
            }, 100);
        }

        public static void ShowInstallDialog() {
            if (_dialog == null) {
                _dialog = new InstallAdditionalContentDialog();
                _dialog.Show();
                _dialog.Closed += (sender, args) => {
                    if (IsAlone) {
                        ContentInstallationManager.Instance.Cancel();
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
            Buttons = new[] { IsAlone ? CloseButton : CreateCloseDialogButton(UiStrings.Toolbar_Hide, true, false, MessageBoxResult.None) };
        }

        private void OnClosed(object sender, EventArgs e) {}

        private void OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(DataFormats.UnicodeText)) return;

            Focus();

            var data = e.Data.GetData(DataFormats.FileDrop) as string[] ??
                    (e.Data.GetData(DataFormats.UnicodeText) as string)?.Split('\n')
                                                                        .Select(x => x.Trim())
                                                                        .Select(x => x.Length > 1 && x.StartsWith(@"""") && x.EndsWith(@"""")
                                                                                ? x.Substring(1, x.Length - 2) : x);
            Dispatcher.InvokeAsync(() => ProcessDroppedFiles(data));
        }

        private void OnDragEnter(object sender, DragEventArgs e) {
            if (e.AllowedEffects.HasFlag(DragDropEffects.All) &&
                    (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText))) {
                e.Effects = DragDropEffects.All;
            }
        }

        private static async void ProcessDroppedFiles(IEnumerable<string> files) {
            if (files == null) return;
            await ArgumentsHandler.ProcessArguments(files);
        }
    }
}
