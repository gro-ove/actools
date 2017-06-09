using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers.Plugins;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public partial class ContentInstallationEntry : NotifyPropertyChanged, IProgress<AsyncProgressEntry> {
        [NotNull]
        public string Source { get; }

        [NotNull]
        public string DisplaySource => Source.Split(new[] { "?password=" }, StringSplitOptions.None)[0];

        [NotNull]
        private readonly ContentInstallationParams _installationParams;

        internal ContentInstallationEntry([NotNull] string source, [CanBeNull] ContentInstallationParams installationParams) {
            Source = source;
            _installationParams = installationParams ?? ContentInstallationParams.Default;
            DisplayName = _installationParams.DisplayName;
            Version = _installationParams.DisplayVersion;
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

                if (value.IsReady) {
                    ContentInstallationManager.Instance.UpdateBusyDoingSomething();
                }
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
                ContentInstallationManager.Instance.UpdateBusyDoingSomething();
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
                ContentInstallationManager.Instance.UpdateBusyDoingSomething();
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

        #region Some details
        private string _displayName;

        public string DisplayName {
            get { return _displayName; }
            set {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }

        private string _fileName;

        public string FileName {
            get { return _fileName; }
            set {
                if (Equals(value, _fileName)) return;
                _fileName = value;
                OnPropertyChanged();
            }
        }

        private string _version;

        public string Version {
            get { return _version; }
            set {
                if (Equals(value, _version)) return;
                _version = value;
                OnPropertyChanged();
            }
        }
        #endregion

        private static readonly Regex ExecutablesRegex = new Regex(@"\.(?:exe|bat|cmd|py|vbs|js|ps1|sh|zsh|bash|pl|hta)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool _sevenZipWarning;

        public async Task<bool> RunAsync() {
            IProgress<AsyncProgressEntry> progress = this;
            ContentInstallationManager.Instance.UpdateBusyDoingSomething();

            try {
                using (var cancellation = new CancellationTokenSource()) {
                    CancellationTokenSource = cancellation;

                    bool CheckCancellation(bool force = false) {
                        if (!cancellation.IsCancellationRequested && !force) return false;
                        Failed = "Cancelled";
                        return false;
                    }

                    string localFilename;

                    // Load remote file if it is remote
                    if (ContentInstallationManager.IsRemoteSource(Source)) {
                        progress.Report(AsyncProgressEntry.FromStringIndetermitate("Downloading…"));

                        try {
                            localFilename = await FlexibleLoader.LoadAsync(Source,
                                    metaInformationCallback: information => {
                                        if (information.FileName != null && information.FileName != Path.GetFileName(Source)) {
                                            FileName = information.FileName;
                                        }

                                        if (Version == null) {
                                            Version = information.Version;
                                        }
                                    },
                                    progress: progress.Subrange(0.001, 0.999, "Downloading ({0})…"),
                                    cancellation: cancellation.Token);
                            if (CheckCancellation()) return false;
                        } catch (OperationCanceledException) {
                            CheckCancellation(true);
                            return false;
                        } catch (WebException e) when (e.Response is HttpWebResponse) {
                            Failed = $"Can’t download file: {((HttpWebResponse)e.Response).StatusDescription.ToLower()}";
                            return false;
                        } catch (WebException) when (cancellation.IsCancellationRequested) {
                            CheckCancellation(true);
                            return false;
                        } catch (Exception e) {
                            Logging.Warning(e);
                            Failed = $"Can’t download file: {e.Message.ToSentenceMember()}";
                            return false;
                        }
                    } else {
                        localFilename = Source;
                    }

                    if (_installationParams.Checksum != null) {
                        using (var fs = new FileStream(localFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var sha1 = new SHA1Managed()) {
                            if (!string.Equals(sha1.ComputeHash(fs).ToHexString(), _installationParams.Checksum, StringComparison.OrdinalIgnoreCase)) {
                                Failed = "Checksum failed";
                                return false;
                            }
                        }
                    }

                    try {
                        progress.Report(AsyncProgressEntry.FromStringIndetermitate("Searching for content…"));

                        // Scan for content
                        using (var installator = await ContentInstallation.FromFile(localFilename, _installationParams, cancellation.Token)) {
                            if (CheckCancellation()) return false;

                            if (installator.IsNotSupported) {
                                Failed = $"Not supported: {installator.NotSupportedMessage.ToSentenceMember()}";

                                if (!_sevenZipWarning && installator is SharpCompressContentInstallator &&
                                        PluginsManager.Instance.GetById(SevenZipContentInstallator.PluginId)?.IsInstalled != true) {
                                    Toast.Show("Try 7-Zip",
                                            "Have some unusual archive you want to install content from? Try 7-Zip plugin, you can find it in Settings",
                                            ContentInstallationManager.PluginsNavigator == null ? (Action)null : () => {
                                                ContentInstallationManager.PluginsNavigator?.ShowPluginsList();
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
                            foreach (var entry in entries) {
                                wrappers.Add(new EntryWrapper(entry, await entry.GetExistingAcCommonObjectAsync()));
                                entry.SingleEntry = entries.Count == 1;
                            }

                            if (wrappers.Count == 0) {
                                Failed = "Nothing to install";
                                return false;
                            }

                            Entries = wrappers.ToArray();
                            ExtraOptions = (await GetExtraOptionsAsync(Entries)).ToArray();

                            if (CheckCancellation()) return false;

                            await WaitForConfirmation();
                            if (CheckCancellation()) return false;

                            var toInstall = (await Entries.Where(x => x.Active)
                                                          .Select(x => x.Entry.GetInstallationDetails(cancellation.Token)).WhenAll(15)).ToList();
                            if (toInstall.Count == 0 || CheckCancellation()) return false;

                            foreach (var extra in ExtraOptions.Select(x => x.PreInstallation).NonNull()) {
                                await extra(progress, cancellation.Token);
                                if (CheckCancellation()) return false;
                            }

                            await Task.Run(() => FileUtils.Recycle(toInstall.SelectMany(x => x.ToRemoval).ToArray()));
                            if (CheckCancellation()) return false;

                            var preventExecutables = !_installationParams.AllowExecutables;
                            await installator.InstallEntryToAsync(info => {
                                if (preventExecutables && ExecutablesRegex.IsMatch(info.Key)) return null;
                                return toInstall.Select(x => x.CopyCallback(info)).FirstOrDefault(x => x != null);
                            }, progress, cancellation.Token);
                            if (CheckCancellation()) return false;

                            foreach (var extra in ExtraOptions.Select(x => x.PostInstallation).NonNull()) {
                                await extra(progress, cancellation.Token);
                                if (CheckCancellation()) return false;
                            }
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
            [NotNull]
            public ContentEntryBase Entry { get; }

            [CanBeNull]
            public AcCommonObject Existing { get; }

            private bool _active;

            public bool Active {
                get => _active;
                set {
                    if (Equals(value, _active)) return;
                    _active = value;
                    OnPropertyChanged();
                }
            }

            public EntryWrapper([NotNull] ContentEntryBase entry, [CanBeNull] AcCommonObject existing) {
                Entry = entry;
                Existing = existing;
                IsNew = existing == null;
                ExistingVersion = (existing as IAcObjectVersionInformation)?.Version;
                Active = true;
                IsNewer = entry.Version.IsVersionNewerThan(ExistingVersion);
                IsOlder = entry.Version.IsVersionOlderThan(ExistingVersion);
            }

            public bool IsNew { get; set; }

            [CanBeNull]
            public string ExistingVersion { get; }

            public bool IsNewer { get; set; }
            public bool IsOlder { get; set; }

            public string DisplayName => IsNew ? Entry.GetNew(Entry.Name) : Entry.GetExisting(Existing?.DisplayName ?? Entry.Name);
        }

        private EntryWrapper[] _entries;

        public EntryWrapper[] Entries {
            get => _entries;
            set {
                if (Equals(value, _entries)) return;
                _entries = value;
                OnPropertyChanged();
            }
        }

        public ExtraOption[] ExtraOptions { get; set; }
        #endregion

        public void Report(AsyncProgressEntry value) {
            Progress = value;
        }
    }
}