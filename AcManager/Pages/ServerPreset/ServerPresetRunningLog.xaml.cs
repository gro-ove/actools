using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AcManager.Controls;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using ICSharpCode.AvalonEdit.Document;
using JetBrains.Annotations;

namespace AcManager.Pages.ServerPreset {
    public partial class ServerPresetRunningLog {
        public ServerPresetRunningLog() {
            InitializeComponent();
            if (TryFindResource(@"TextEditorSyntaxLog") is string data) {
                Text.SyntaxHighlighting = HighlighterHolder.Get(data);
            }
            Loaded += (sender, args) => EnsureSet();
            DataContextChanged += (sender, args) => EnsureSet();
            this.OnActualUnload(EnsureSet);
        }

        [CanBeNull]
        private SelectedPage.ViewModel Model => DataContext as SelectedPage.ViewModel;

        private ServerPresetObject _previousServer;
        private BetterObservableCollection<string> _previousServerLog;

        private void EnsureSet() {
            var server = IsLoaded ? Model?.SelectedObject : null;
            var serverLog = server?.RunningLog;

            if (server != _previousServer) {
                if (_previousServer != null) {
                    _previousServer.PropertyChanged -= OnServerChanged;
                }
                if (server != null) {
                    server.PropertyChanged += OnServerChanged;
                }
                _previousServer = server;
            }

            if (serverLog != _previousServerLog) {
                if (_previousServerLog != null) {
                    _previousServerLog.CollectionChanged -= OnLogChanged;
                }

                if (serverLog != null) {
                    serverLog.CollectionChanged += OnLogChanged;
                    Text.Document = new TextDocument(serverLog.JoinToString("\n")) { UndoStack = { SizeLimit = 0 } };
                    Text.ScrollToEnd();
                } else {
                    Text.Document = new TextDocument { UndoStack = { SizeLimit = 0 } };
                }

                _previousServerLog = serverLog;
            }
        }

        private void OnServerChanged(object sender, PropertyChangedEventArgs e) {
            EnsureSet();
        }

        private void OnLogChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (_previousServerLog == null) return;

            var textView = Text.TextArea.TextView;
            var needsToScroll = textView.VerticalOffset + textView.ActualHeight + 1 >= textView.DocumentHeight;
            if (e.Action == NotifyCollectionChangedAction.Add) {
                Text.Document.BeginUpdate();
                foreach (var item in e.NewItems.OfType<string>()) {
                    Text.Document.Insert(Text.Document.TextLength, "\n");
                    Text.Document.Insert(Text.Document.TextLength, item);
                }
                Text.Document.EndUpdate();
            } else {
                Text.Document.Text = _previousServerLog.JoinToString("\n");
            }
            if (needsToScroll) {
                Text.ScrollToEnd();
            }
        }
    }
}