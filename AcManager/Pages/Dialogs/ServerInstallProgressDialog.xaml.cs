using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.ContentInstallation;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Dialogs {
    /// <summary>
    /// Modal popup shown to the player while a server-driven content install
    /// (auto-confirm and/or auto-rejoin) runs. Mirrors the per-entry rows from
    /// the downloads panel but in a small focused window so the player can see
    /// what's happening without hunting through CM. Disappears as soon as the
    /// installs finish so the rejoin handoff isn't blocked by a leftover dialog.
    /// </summary>
    public partial class ServerInstallProgressDialog : ModernDialog {
        public enum StatusKindValue {
            None,
            Success,
            Failure
        }

        public class ViewModel : NotifyPropertyChanged {
            public BetterObservableCollection<ContentInstallationEntry> Entries { get; }
                    = new BetterObservableCollection<ContentInstallationEntry>();

            private string _headerTitle;
            public string HeaderTitle {
                get => _headerTitle;
                set => Apply(value, ref _headerTitle);
            }

            private string _headerMessage;
            public string HeaderMessage {
                get => _headerMessage;
                set => Apply(value, ref _headerMessage);
            }

            private string _statusMessage;
            public string StatusMessage {
                get => _statusMessage;
                set => Apply(value, ref _statusMessage);
            }

            private StatusKindValue _statusKind = StatusKindValue.None;
            public StatusKindValue StatusKind {
                get => _statusKind;
                set => Apply(value, ref _statusKind);
            }

            private bool _isRingActive = true;
            public bool IsRingActive {
                get => _isRingActive;
                set => Apply(value, ref _isRingActive);
            }
        }

        private readonly ViewModel _model;
        private readonly HashSet<string> _trackedSources;
        private readonly DateTime _trackedEntriesCreatedAfter;
        private readonly bool _rejoinRequested;
        private readonly ContentInstallationManager _installationManager = ContentInstallationManager.Instance;
        private readonly HashSet<ContentInstallationEntry> _subscribed = new HashSet<ContentInstallationEntry>();
        private bool _autoCloseScheduled;
        private bool _userClosed;

        public ServerInstallProgressDialog(IEnumerable<string> sources, bool rejoinRequested, bool autoConfirm, DateTime trackedEntriesCreatedAfter) {
            _trackedSources = new HashSet<string>(sources ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _trackedEntriesCreatedAfter = trackedEntriesCreatedAfter;
            _rejoinRequested = rejoinRequested;
            _model = new ViewModel {
                HeaderTitle = rejoinRequested ? AppStrings.ServerInstallProgress_HeaderTitle : AppStrings.ServerInstallProgress_HeaderTitleInstalling,
                HeaderMessage = rejoinRequested
                        ? AppStrings.ServerInstallProgress_HeaderMessageRejoin
                        : autoConfirm
                                ? AppStrings.ServerInstallProgress_HeaderMessageAutoClose
                                : AppStrings.ServerInstallProgress_HeaderMessageDownloading
            };
            DataContext = _model;
            InitializeComponent();

            Buttons = CreateWorkingButtons();

            if (Application.Current?.MainWindow is Window owner && owner.IsLoaded && owner != this) {
                Owner = owner;
            }

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _installationManager.DownloadList.CollectionChanged += OnDownloadListChanged;
            _installationManager.PropertyChanged += OnInstallationManagerPropertyChanged;

            foreach (var entry in _installationManager.DownloadList) {
                TryAttach(entry);
            }
            ReevaluateState();
        }

        private void OnClosed(object sender, EventArgs e) {
            _userClosed = true;
            _installationManager.DownloadList.CollectionChanged -= OnDownloadListChanged;
            _installationManager.PropertyChanged -= OnInstallationManagerPropertyChanged;
            foreach (var entry in _subscribed) {
                entry.PropertyChanged -= OnEntryPropertyChanged;
            }
            _subscribed.Clear();
        }

        private void OnDownloadListChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.NewItems != null) {
                foreach (ContentInstallationEntry entry in e.NewItems) {
                    TryAttach(entry);
                }
            }
            if (e.OldItems != null) {
                foreach (ContentInstallationEntry entry in e.OldItems) {
                    if (_subscribed.Remove(entry)) {
                        entry.PropertyChanged -= OnEntryPropertyChanged;
                    }
                    _model.Entries.Remove(entry);
                }
            }
            ReevaluateState();
        }

        private bool MatchesEntry(ContentInstallationEntry entry) {
            return entry != null
                    && _trackedSources.Contains(entry.Source)
                    && entry.AddedDateTime >= _trackedEntriesCreatedAfter;
        }

        private void TryAttach(ContentInstallationEntry entry) {
            if (!MatchesEntry(entry)) return;
            if (!_model.Entries.Contains(entry)) {
                _model.Entries.Add(entry);
            }
            if (_subscribed.Add(entry)) {
                entry.PropertyChanged += OnEntryPropertyChanged;
            }
        }

        private Control[] CreateWorkingButtons() {
            return new Control[] {
                    CreateExtraDialogButton(UiStrings.Toolbar_Hide, Close),
                    CreateExtraDialogButton(AppStrings.DownloadList_Cancel, () => {
                        foreach (var entry in _installationManager.DownloadList.Where(MatchesEntry).ToList()) {
                            entry.CancelCommand.Execute();
                        }
                        Close();
                    })
            };
        }

        private void OnEntryPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(ContentInstallationEntry.State)
                    || e.PropertyName == nameof(ContentInstallationEntry.Progress)
                    || e.PropertyName == nameof(ContentInstallationEntry.FailedMessage)) {
                ReevaluateState();
            }
        }

        private void OnInstallationManagerPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(ContentInstallationManager.PostInstallAutoJoinActive)
                    || e.PropertyName == nameof(ContentInstallationManager.PostInstallAutoJoinFailed)
                    || e.PropertyName == nameof(ContentInstallationManager.PostInstallAutoJoinMessage)) {
                ReevaluateState();
            }
        }

        private void ReevaluateState() {
            if (_userClosed) return;

            var entries = _model.Entries;
            var anyActive = entries.Count == 0 || entries.Any(x => x.State != ContentInstallationEntryState.Finished);
            var anyFailed = entries.Any(x => x.State == ContentInstallationEntryState.Finished && !string.IsNullOrEmpty(x.FailedMessage));

            if (anyActive) {
                _model.IsRingActive = true;
                _model.StatusKind = StatusKindValue.None;
                _model.StatusMessage = null;
                Buttons = CreateWorkingButtons();
                return;
            }

            if (anyFailed) {
                // Leave the dialog open so the player can see what went wrong; rejoin
                // (if any) is already cancelled by ArgumentsHandler in this case.
                _model.IsRingActive = false;
                _model.StatusKind = StatusKindValue.Failure;
                _model.StatusMessage = _installationManager.PostInstallAutoJoinFailed && !string.IsNullOrEmpty(_installationManager.PostInstallAutoJoinMessage)
                        ? _installationManager.PostInstallAutoJoinMessage
                        : AppStrings.ServerInstallProgress_FailureSummary;
                Buttons = new[] { CloseButton };
                return;
            }

            // Success: brief "Done" flash so the green check is actually visible, then
            // close immediately. We DON'T wait for the rejoin chain — once installs
            // finish, the player wants the dialog out of the way so the auto-rejoin
            // handoff isn't visually blocked. The downloads panel still shows the
            // "Reconnecting…" banner via PostInstallAutoJoinMessage.
            _model.IsRingActive = false;
            _model.StatusKind = StatusKindValue.Success;
            _model.StatusMessage = _rejoinRequested ? AppStrings.ServerInstallProgress_DoneReconnecting : AppStrings.ServerInstallProgress_Done;
            Buttons = Array.Empty<Control>();
            ScheduleAutoClose();
        }

        private void ScheduleAutoClose() {
            if (_autoCloseScheduled) return;
            _autoCloseScheduled = true;

            // Brief flash so the player actually sees the green check rather than the
            // dialog snapping shut at the same instant the last byte lands.
            Task.Delay(TimeSpan.FromMilliseconds(700)).ContinueWith(_ => {
                Dispatcher.BeginInvoke(new Action(() => {
                    if (_userClosed) return;
                    try {
                        Close();
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                }));
            });
        }
    }
}
