using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public class ContentInstallationManager : NotifyPropertyChanged {
        public static ContentInstallationManager Instance { get; } = new ContentInstallationManager();

        public static IPluginsSusanin PluginsSusanin { get; set; }

        private ContentInstallationManager() {
            Queue = new BetterObservableCollection<ContentInstallationEntry>();
        }

        public BetterObservableCollection<ContentInstallationEntry> Queue { get; }

        private readonly Dictionary<string, Task<bool>> _tasks = new Dictionary<string, Task<bool>>();

        private async Task<bool> InstallAsyncInternal([NotNull] string source, ContentInstallationParams installationParams) {
            var entry = new ContentInstallationEntry(source, installationParams);
            ActionExtension.InvokeInMainThread(() => Queue.Add(entry));
            var result = await entry.RunAsync();
            await Task.Delay(1);
            RemoveLater(entry);
            _tasks.Remove(source);
            return result;
        }

        public Task<bool> InstallAsync([NotNull] string source, ContentInstallationParams installationParams = null) {
            return _tasks.TryGetValue(source, out Task<bool> task) ? task : (_tasks[source] = InstallAsyncInternal(source, installationParams));
        }

        private async void RemoveLater(ContentInstallationEntry entry) {
            await Task.Delay(TimeSpan.FromSeconds(entry.Failed != null ? 7d : 3d));
            ActionExtension.InvokeInMainThread(() => Queue.Remove(entry));
        }

        public static bool IsRemoteSource(string source) {
            return source.StartsWith(@"http:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"https:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"ftp:", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAdditionalContent(string filename) {
            // TODO: or PP-filter, or …?

            try {
                return FileUtils.IsDirectory(filename) ||
                        !filename.EndsWith(@".kn5") && !filename.EndsWith(@".acreplay") && !FileUtils.IsAffected(FileUtils.GetReplaysDirectory(), filename);
            } catch (Exception) {
                return false;
            }
        }
    }

    public class ContentInstallationParams {
        public static readonly ContentInstallationParams Default = new ContentInstallationParams();

        public string CarId { get; set; }
    }

    public enum ContentInstallationEntryState {
        Loading, PasswordRequired, WaitingForConfirmation, Finished
    }

    public interface IPluginsSusanin {
        void ShowPluginsList();
    }

    public class ContentInstallationEntry : NotifyPropertyChanged, IProgress<AsyncProgressEntry> {
        [NotNull]
        public string Source { get; }

        [NotNull]
        private readonly ContentInstallationParams _installationParams;

        internal ContentInstallationEntry([NotNull] string source, [CanBeNull] ContentInstallationParams installationParams) {
            Source = source;
            _installationParams = installationParams ?? ContentInstallationParams.Default;
        }

        public ContentInstallationEntryState State => _progress.IsReady ? ContentInstallationEntryState.Finished :
                _isPasswordRequired ? ContentInstallationEntryState.PasswordRequired :
                        _waitingForConfirmation ? ContentInstallationEntryState.WaitingForConfirmation :
                                ContentInstallationEntryState.Loading;

        private AsyncProgressEntry _progress;

        public AsyncProgressEntry Progress {
            get => _progress;
            set {
                if (Equals(value, _progress)) return;
                _progress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(State));
            }
        }

        [CanBeNull]
        private CancellationTokenSource _cancellationTokenSource;

        [CanBeNull]
        public CancellationTokenSource CancellationTokenSource {
            set {
                if (Equals(value, _cancellationTokenSource)) return;
                _cancellationTokenSource = value;
                OnPropertyChanged();
                _cancelCommand?.RaiseCanExecuteChanged();
            }
        }

        private DelegateCommand _cancelCommand;

        public DelegateCommand CancelCommand => _cancelCommand ?? (_cancelCommand = new DelegateCommand(() => {
            _cancellationTokenSource?.Cancel();
        }, () => _cancellationTokenSource != null));

        private string _failed;

        public string Failed {
            get => _failed;
            set {
                if (Equals(value, _failed)) return;
                _failed = value;
                OnPropertyChanged();
            }
        }

        #region Password
        private bool _isPasswordRequired;

        public bool IsPasswordRequired {
            get => _isPasswordRequired;
            set {
                if (Equals(value, _isPasswordRequired)) return;
                _isPasswordRequired = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(State));
                _applyPasswordCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _passwordIsInvalid;
        private string _invalidPassword;

        public bool PasswordIsInvalid {
            get => _passwordIsInvalid;
            set {
                if (Equals(value, _passwordIsInvalid)) return;
                _passwordIsInvalid = value;
                _invalidPassword = _inputPassword;
                OnPropertyChanged();
            }
        }

        private string _inputPassword;

        public string InputPassword {
            get => _inputPassword;
            set {
                if (Equals(value, _inputPassword)) return;
                _inputPassword = value;
                PasswordIsInvalid = _invalidPassword != null && _invalidPassword == value;
                OnPropertyChanged();
            }
        }

        private event EventHandler PasswordEnter;

        private DelegateCommand _applyPasswordCommand;

        public DelegateCommand ApplyPasswordCommand => _applyPasswordCommand ?? (_applyPasswordCommand = new DelegateCommand(() => {
            PasswordEnter?.Invoke(this, EventArgs.Empty);
        }, () => IsPasswordRequired));

        private Task<string> WaitForPassword() {
            var tcs = new TaskCompletionSource<string>();
            _cancellationTokenSource?.Token.Register(() => tcs.TrySetCanceled());

            void OnPasswordEnter(object sender, EventArgs args) {
                IsPasswordRequired = false;
                PasswordEnter -= OnPasswordEnter;
                tcs.SetResult(InputPassword);
            }

            PasswordEnter += OnPasswordEnter;
            IsPasswordRequired = true;
            return tcs.Task;
        }
        #endregion

        #region Waiting for confirmation
        private bool _waitingForConfirmation;

        public bool WaitingForConfirmation {
            get => _waitingForConfirmation;
            set {
                if (Equals(value, _waitingForConfirmation)) return;
                _waitingForConfirmation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(State));
                _confirmCommand?.RaiseCanExecuteChanged();
            }
        }

        private event EventHandler Confirm;

        private DelegateCommand _confirmCommand;

        public DelegateCommand ConfirmCommand => _confirmCommand ?? (_confirmCommand = new DelegateCommand(() => {
            Confirm?.Invoke(this, EventArgs.Empty);
        }, () => WaitingForConfirmation));

        private Task WaitForConfirmation() {
            var tcs = new TaskCompletionSource<bool>();
            _cancellationTokenSource?.Token.Register(() => tcs.TrySetCanceled());

            void OnConfirm(object sender, EventArgs args) {
                WaitingForConfirmation = false;
                Confirm -= OnConfirm;
                tcs.SetResult(true);
            }

            Confirm += OnConfirm;
            WaitingForConfirmation = true;
            return tcs.Task;
        }
        #endregion

        private static bool _sevenZipWarning;

        public async Task<bool> RunAsync() {
            IProgress<AsyncProgressEntry> progress = this;

            try {
                using (var cancellation = new CancellationTokenSource()) {
                    CancellationTokenSource = cancellation;

                    bool CheckCancellation(bool force = false) {
                        if (!cancellation.IsCancellationRequested && !force) return false;
                        Failed = "Cancelled";
                        return false;
                    }

                    string localFilename;

                    // load remote file if it is remote
                    if (ContentInstallationManager.IsRemoteSource(Source)) {
                        progress.Report(AsyncProgressEntry.FromStringIndetermitate("Downloading…"));

                        try {
                            localFilename = await FlexibleLoader.LoadAsync(Source,
                                    progress: progress.Subrange(0.001, 0.999, "Downloading ({0})…"),
                                    cancellation: cancellation.Token);
                            if (CheckCancellation()) return false;
                        } catch (OperationCanceledException e) {
                            CheckCancellation(true);
                            return false;
                        } catch (WebException e) when (e.Response is HttpWebResponse) {
                            Failed = $"Can’t download file: {((HttpWebResponse)e.Response).StatusDescription.ToLower()}";
                            return false;
                        } catch (Exception e) {
                            Logging.Warning(e);
                            Failed = $"Can’t download file: {e.Message.ToSentenceMember()}";
                            return false;
                        }
                    } else {
                        localFilename = Source;
                    }

                    try {
                        progress.Report(AsyncProgressEntry.FromStringIndetermitate("Searching for content…"));

                        // scan for content
                        using (var installator = await ContentInstallation.FromFile(localFilename, _installationParams, cancellation.Token)) {
                            if (CheckCancellation()) return false;

                            if (installator.IsNotSupported) {
                                Failed = $"Not supported: {installator.NotSupportedMessage.ToSentenceMember()}";

                                if (!_sevenZipWarning && installator is SharpCompressContentInstallator &&
                                        PluginsManager.Instance.GetById(SevenZipContentInstallator.PluginId)?.IsInstalled != true) {
                                    Toast.Show("Try 7-Zip",
                                            "Have some unusual archive you want to install content from? Try 7-Zip plugin, you can find it in Settings",
                                            ContentInstallationManager.PluginsSusanin == null ? (Action)null : () => {
                                                ContentInstallationManager.PluginsSusanin?.ShowPluginsList();
                                            });
                                    _sevenZipWarning = true;
                                }

                                return false;
                            }

                            while (installator.IsPasswordRequired) {
                                var password = await WaitForPassword();
                                if (CheckCancellation()) return false;

                                progress.Report(AsyncProgressEntry.FromStringIndetermitate("Checking password…"));
                                await installator.TrySetPasswordAsync(password, cancellation.Token);
                                if (CheckCancellation()) return false;

                                if (installator.IsNotSupported) {
                                    Failed = $"Not supported: {installator.NotSupportedMessage.ToSentenceMember()}";
                                    return false;
                                }

                                if (installator.IsPasswordCorrect) break;

                                PasswordIsInvalid = true;
                            }

                            var entries = await installator.GetEntriesAsync(
                                    progress.Subrange(0.001, 0.999, "Searching for content ({0})…"), cancellation.Token);

                            if (installator.IsNotSupported) {
                                Failed = $"Not supported: {installator.NotSupportedMessage.ToSentenceMember()}";
                                return false;
                            }

                            if (entries == null) {
                                CheckCancellation(true);
                                return false;
                            }

                            var wrappers = new List<EntryWrapper>();
                            foreach (var entryWrapper in entries) {
                                var manager = entryWrapper.GetManager();
                                if (manager == null) continue;

                                var existed = await manager.GetObjectByIdAsync(entryWrapper.Id);
                                wrappers.Add(new EntryWrapper(entryWrapper, existed == null, (existed as AcJsonObjectNew)?.Version));
                            }

                            if (wrappers.Count == 0) {
                                Failed = "Nothing to install";
                                return false;
                            }

                            Entries = wrappers.ToArray();

                            if (CheckCancellation()) return false;

                            await WaitForConfirmation();
                            if (CheckCancellation()) return false;

                            var toInstall = (await Entries.Where(x => x.InstallEntry)
                                                          .Select(x => x.Entry.GetCopyCallback(cancellation.Token)).WhenAll(15)).ToList();
                            if (toInstall.Count == 0 || CheckCancellation()) return false;

                            await installator.InstallEntryToAsync(info => toInstall.Select(x => x(info)).FirstOrDefault(x => x != null),
                                    progress, cancellation.Token);
                        }

                        return true;
                    } catch (TaskCanceledException) {
                        Failed = "Cancelled";
                        return false;
                    } catch (Exception e) {
                        Failed = "Can’t find content: " + e.Message;
                        Logging.Warning(e);
                        return false;
                    }
                }
            } catch (TaskCanceledException) {
                Failed = "Cancelled";
                return false;
            } finally {
                CancellationTokenSource = null;
                Progress = AsyncProgressEntry.Ready;
            }
        }

        #region Found entries
        public class EntryWrapper : NotifyPropertyChanged {
            public ContentEntryBase Entry { get; }

            private bool _installEntry;

            public bool InstallEntry {
                get => _installEntry;
                set {
                    if (Equals(value, _installEntry)) return;
                    _installEntry = value;
                    OnPropertyChanged();
                }
            }

            public EntryWrapper(ContentEntryBase entry, bool isNew, [CanBeNull] string existingVersion) {
                Entry = entry;
                InstallEntry = true;
                IsNew = isNew;
                ExistingVersion = existingVersion;
                IsNewer = entry.Version.IsVersionNewerThan(ExistingVersion);
                IsOlder = entry.Version.IsVersionOlderThan(ExistingVersion);
            }

            public bool IsNew { get; set; }

            [CanBeNull]
            public string ExistingVersion { get; }

            public bool IsNewer { get; set; }
            public bool IsOlder { get; set; }

            public string DisplayName => IsNew ? Entry.GetNew(Entry.Name) : Entry.GetExisting(Entry.Name);
            public string DisplayEntryId => string.IsNullOrEmpty(Entry.Id) ? "N/A" : Entry.Id;
            public string DisplayEntryPath => string.IsNullOrEmpty(Entry.EntryPath) ? "N/A" : Entry.EntryPath;

            private BetterImage.BitmapEntry? _icon;
            public BetterImage.BitmapEntry? Icon => Entry.IconData == null ? null :
                    _icon ?? (_icon = BetterImage.LoadBitmapSourceFromBytes(Entry.IconData, 32, 32));
        }

        public EntryWrapper[] Entries { get; set; }
        #endregion

        public void Report(AsyncProgressEntry value) {
            Progress = value;
        }
    }
}