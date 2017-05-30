using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;

namespace AcManager.Pages.Dialogs {
    public partial class InstallAdditionalContentDialog {
        private static InstallAdditionalContentDialog _dialog;

        public static void Initialize() {
            ContentInstallationManager.Instance.Queue.CollectionChanged += OnQueueChanged;
        }

        private static bool _waiting;
        private static async void OnQueueChanged(object o, NotifyCollectionChangedEventArgs a) {
            if (_waiting) return;

            var added = a.Action == NotifyCollectionChangedAction.Add;
            if (added != (_dialog != null)) {
                _waiting = true;
                await Task.Delay(100);
                _waiting = false;

                if (added) {
                    if (_dialog == null) {
                        _dialog = new InstallAdditionalContentDialog();
                        _dialog.Show();
                        _dialog.Closed += (sender, args) => {
                            foreach (var entry in ContentInstallationManager.Instance.Queue.ToList()) {
                                entry.CancelCommand.Execute();
                            }

                            _dialog = null;
                        };
                    }
                } else if (ContentInstallationManager.Instance.Queue.Count == 0 && _dialog?.IsActive != true) {
                    _dialog?.Close();
                }
            }
        }

        public InstallAdditionalContentDialog() {
            DataContext = this;
            InitializeComponent();

            Buttons = new [] {
                // TODO: rename “close” to “hide”?
                CloseButton
            };
        }

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
