using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.ContentInstallation.Entries;
using AcManager.Tools.ContentInstallation.Implementations;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using AcTools.GenericMods;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public partial class ContentInstallationEntry : NotifyPropertyErrorsChanged, IProgress<AsyncProgressEntry>, ICopyCallback, IDisposable {
        public DateTime AddedDateTime { get; }

        [NotNull]
        public string Source { get; }

        [NotNull]
        public string DisplaySource => Source.Split(new[] { "?password=" }, StringSplitOptions.None)[0];

        [CanBeNull]
        public string DisplayUpdateFor { get; }

        public bool PreferCleanInstallation { get; }

        [NotNull]
        private readonly ContentInstallationParams _installationParams;

        internal ContentInstallationEntry([NotNull] string source, [CanBeNull] ContentInstallationParams installationParams) {
            _installationParams = installationParams ?? ContentInstallationParams.Default;
            Source = source;
            AddedDateTime = DateTime.Now;
            DisplayName = _installationParams.DisplayName;
            InformationUrl = _installationParams.InformationUrl;
            Version = _installationParams.Version;

            if (_installationParams.CupType.HasValue) {
                var manager = CupClient.Instance.GetAssociatedManager(_installationParams.CupType.Value);
                DisplayUpdateFor = _installationParams.IdsToUpdate?.Select(x => manager?.GetObjectById(x)?.ToString()).JoinToReadableString();
                if (string.IsNullOrWhiteSpace(DisplayUpdateFor)) {
                    DisplayUpdateFor = null;
                }

                PreferCleanInstallation = _installationParams.PreferCleanInstallation;
            }
        }

        internal static ContentInstallationEntry ReadyExample => new ContentInstallationEntry("input.bin", null) {
            FileName = "input.bin",
            LocalFilename = @"U:\dump.bin",
            Progress = AsyncProgressEntry.Ready
        };

        public ContentInstallationEntryState State => _progress.IsReady ? ContentInstallationEntryState.Finished :
                _isPasswordRequired ? ContentInstallationEntryState.PasswordRequired :
                        _waitingForConfirmation ? ContentInstallationEntryState.WaitingForConfirmation :
                                ContentInstallationEntryState.Loading;

        private AsyncProgressEntry _progress;

        public AsyncProgressEntry Progress {
            get => _progress;
            set {
                if (_progress.IsReady) return;

                if (Equals(value, _progress)) return;
                _progress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(IsEmpty));

                if (value.IsReady) {
                    ContentInstallationManager.Instance?.UpdateBusyDoingSomething();
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
            UserCancelled = true;
            Cancel();
        }, () => _cancellationTokenSource != null || FailedMessage != null && !Cancelled));

        private void Cancel() {
            Cancelled = true;
            FailedMessage = ContentInstallationManager.IsRemoteSource(Source) ? "Download cancelled" : "Installation cancelled";
            _cancellationTokenSource?.Cancel();
            DisposeHelper.Dispose(ref _taskbar);
        }

        public bool IsFailed => State == ContentInstallationEntryState.Finished && FailedMessage != null;
        public bool IsEmpty => State == ContentInstallationEntryState.Finished && Entries.Length == 0;

        private string _failedMessage;

        public string FailedMessage {
            get => _failedMessage;
            set {
                value = value.ToSentence();
                if (Equals(value, _failedMessage)) return;
                _failedMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFailed));
                _cancelCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _failedCommentary;

        public string FailedCommentary {
            get => _failedCommentary;
            set {
                if (Equals(value, _failedCommentary)) return;
                _failedCommentary = value;
                OnPropertyChanged();
            }
        }

        private bool _userCancelled;

        public bool UserCancelled {
            get => _userCancelled;
            private set {
                if (Equals(value, _userCancelled)) return;
                _userCancelled = value;
                OnPropertyChanged();
            }
        }

        private bool _cancelled;

        public bool Cancelled {
            get => _cancelled;
            private set {
                if (Equals(value, _cancelled)) return;
                _cancelled = value;
                OnPropertyChanged();
                _cancelCommand?.RaiseCanExecuteChanged();
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
                OnErrorsChanged(nameof(InputPassword));
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

        public override IEnumerable GetErrors(string propertyName) {
            switch (propertyName) {
                case nameof(InputPassword):
                    return PasswordIsInvalid ? new[]{ "Password is invalid" } : null;
                default:
                    return null;
            }
        }

        public override bool HasErrors => PasswordIsInvalid;

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

        #region System icon
        private ImageSource _fileIcon;

        public ImageSource FileIcon {
            get => _fileIcon;
            set {
                if (Equals(value, _fileIcon)) return;
                _fileIcon = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Some details
        private string _displayName;

        public string DisplayName {
            get => _displayName ?? FileName;
            set {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayNameWithUrl));
                Logging.Debug(DisplayNameWithUrl);
            }
        }

        private string _informationUrl;

        public string InformationUrl {
            get => _informationUrl;
            set {
                if (Equals(value, _informationUrl)) return;
                _informationUrl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayNameWithUrl));
                Logging.Debug(DisplayNameWithUrl);
            }
        }

        public string DisplayNameWithUrl => InformationUrl != null
                ? $@"[url={BbCodeBlock.EncodeAttribute(InformationUrl)}]{BbCodeBlock.Encode(DisplayName ?? "?")}[/url]" : BbCodeBlock.Encode(DisplayName);

        private string _fileName;

        public string FileName {
            get => _fileName;
            set {
                if (Equals(value, _fileName)) return;
                _fileName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(DisplayNameWithUrl));

                try {
                    FileIcon = IconManager.FindIconForFilename(value, true);
                } catch (Exception e) {
                    Logging.Warning(e);
                    FileIcon = null;
                }
            }
        }

        private string _version;

        public string Version {
            get => _version;
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

        private string _loadedFilename;

        public string LoadedFilename {
            get => _loadedFilename;
            set {
                if (Equals(value, _loadedFilename)) return;
                _loadedFilename = value;
                OnPropertyChanged();
                LocalFilename = value;
            }
        }

        private string _localFilename;

        public string LocalFilename {
            get => _localFilename;
            set {
                if (Equals(value, _localFilename)) return;
                _localFilename = value;
                OnPropertyChanged();
                _viewInExplorerCommand?.RaiseCanExecuteChanged();
            }
        }

        private DelegateCommand _viewInExplorerCommand;

        public DelegateCommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new DelegateCommand(() => {
            KeepLoaded = true;
            WindowsHelper.ViewFile(LocalFilename);
        }, () => LocalFilename != null && File.Exists(LocalFilename)));

        private static string GetFileNameFromUrl(string url) {
            var fileName = FileUtils.EnsureFileNameIsValid(
                    url.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).Last().Split('?')[0]).Trim();

            if (string.IsNullOrWhiteSpace(fileName)) {
                fileName = FileUtils.EnsureFileNameIsValid(url).Trim();
                if (string.IsNullOrWhiteSpace(fileName)) {
                    fileName = "download";
                }
            }

            return Regex.Replace(fileName, @"\.(?:asp|cgi|p(?:hp3?|l)|s?html?)$", "", RegexOptions.IgnoreCase);
        }

        private bool _canPause;

        public bool CanPause {
            get => _canPause;
            set {
                if (Equals(value, _canPause)) return;
                _canPause = value;
                OnPropertyChanged();
                if (!value) {
                    IsPaused = false;
                }
            }
        }

        private bool _isPaused;

        public bool IsPaused {
            get => _isPaused;
            set {
                if (Equals(value, _isPaused)) return;
                _isPaused = value;
                OnPropertyChanged();
            }
        }

        private bool _keepLoaded;

        public bool KeepLoaded {
            get => _keepLoaded;
            set {
                if (Equals(value, _keepLoaded)) return;
                _keepLoaded = value;
                OnPropertyChanged();
            }
        }

        private bool _sevenZipInstallatorWouldNotHurt;

        public bool SevenZipInstallatorWouldNotHurt {
            get => _sevenZipInstallatorWouldNotHurt;
            set {
                if (Equals(value, _sevenZipInstallatorWouldNotHurt)) return;
                _sevenZipInstallatorWouldNotHurt = value;
                OnPropertyChanged();
            }
        }

        public Task<bool> RunAsync() {
            var tcs = new TaskCompletionSource<bool>();
            PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(Cancelled) && Cancelled) {
                    tcs.TrySetResult(false);
                }
            };

            RunAsyncInner().ContinueWith(v => {
                if (v.IsCanceled) {
                    tcs.TrySetCanceled();
                } else if (v.Exception != null) {
                    tcs.TrySetException(v.Exception);
                } else {
                    tcs.TrySetResult(v.Result);
                }
            });

            return tcs.Task;
        }

        private TaskbarHolder _taskbar;

        private async Task<bool> RunAsyncInner() {
            IProgress<AsyncProgressEntry> progress = this;
            ContentInstallationManager.Instance.UpdateBusyDoingSomething();

            try {
                _taskbar = TaskbarService.Create(10000);

                using (var cancellation = new CancellationTokenSource()) {
                    CancellationTokenSource = cancellation;

                    bool CheckCancellation(bool force = false) {
                        if (!cancellation.IsCancellationRequested && !force) return false;
                        Cancel();
                        return false;
                    }

                    // Load remote file if it is remote
                    if (ContentInstallationManager.IsRemoteSource(Source)) {
                        progress.Report(AsyncProgressEntry.FromStringIndetermitate("Downloading…"));
                        _taskbar?.Set(TaskbarState.Indeterminate, 0d);

                        try {
                            // LoadedFilename = GetLoaderDestination();
                            // await Task.Delay(1);
                            LoadedFilename = await FlexibleLoader.LoadAsyncTo(Source,
                                    (url, information) => new FlexibleLoaderDestination(Path.Combine(SettingsHolder.Content.TemporaryFilesLocationValue,
                                            information.FileName ?? GetFileNameFromUrl(url)), true),
                                    information => {
                                        CanPause = information.CanPause;
                                        FileName = information.FileName ?? information.FileName;
                                        Version = information.Version ?? information.Version;
                                    },
                                    () => IsPaused,
                                    new Progress<AsyncProgressEntry>(v => {
                                        var msg = string.IsNullOrWhiteSpace(v.Message) ? "Downloading…" : $"Downloading ({v.Message.ToSentenceMember()})…";
                                        if (v.Progress == 0d || v.Progress == null) {
                                            progress.Report(AsyncProgressEntry.FromStringIndetermitate(msg));
                                            _taskbar?.Set(TaskbarState.Indeterminate, 0d);
                                        } else {
                                            progress.Report(new AsyncProgressEntry(msg, v.Progress * 0.9999));
                                            _taskbar?.Set(TaskbarState.Normal, v.Progress ?? 0d);
                                        }
                                    }),
                                    cancellation.Token);
                            CanPause = false;
                            if (CheckCancellation()) return false;
                        } catch (Exception e) when (e.IsCanceled()) {
                            CheckCancellation(true);
                            return false;
                        } catch (WebException e) when (e.Response is HttpWebResponse) {
                            FailedMessage = $"Can’t download file: {((HttpWebResponse)e.Response).StatusDescription.ToLower()}";
                            return false;
                        } catch (WebException) when (cancellation.IsCancellationRequested) {
                            CheckCancellation(true);
                            return false;
                        } catch (InformativeException e) {
                            Logging.Warning(e);
                            FailedMessage = e.Message;
                            FailedCommentary = e.SolutionCommentary;
                            return false;
                        } catch (Exception e) {
                            Logging.Warning(e);
                            FailedMessage = e.Message;
                            return false;
                        }
                    } else {
                        LocalFilename = Source;
                        FileName = Path.GetFileName(Source);
                    }

                    if (_installationParams.Checksum != null) {
                        using (var fs = new FileStream(LocalFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var sha1 = new SHA1Managed()) {
                            if (!string.Equals(sha1.ComputeHash(fs).ToHexString(), _installationParams.Checksum, StringComparison.OrdinalIgnoreCase)) {
                                FailedMessage = "Checksum failed";
                                return false;
                            }
                        }
                    }

                    try {
                        progress.Report(AsyncProgressEntry.FromStringIndetermitate("Searching for content…"));
                        _taskbar?.Set(TaskbarState.Indeterminate, 0d);

                        // Scan for content
                        using (var installator = await FromFile(LocalFilename, _installationParams, cancellation.Token)) {
                            if (CheckCancellation()) return false;

                            if (installator is SharpCompressContentInstallator || installator is ZipContentInstallator
                                    || installator is SevenZipContentInstallator /* to check for 7-Zip updates */) {
                                SevenZipInstallatorWouldNotHurt = true;
                            }

                            if (installator.IsNotSupported) {
                                FailedMessage = $"Not supported: {installator.NotSupportedMessage.ToSentenceMember()}";

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
                                _taskbar?.Set(TaskbarState.Paused, 0d);

                                var password = await WaitForPassword();
                                if (CheckCancellation()) return false;

                                bool[] setProgress = { true };
                                try {
                                    Task.Delay(100).ContinueWith(t => {
                                        if (setProgress[0]) {
                                            _taskbar?.Set(TaskbarState.Indeterminate, 0d);
                                            progress.Report(AsyncProgressEntry.FromStringIndetermitate("Checking password…"));
                                        }
                                    }).Forget();
                                    await installator.TrySetPasswordAsync(password, cancellation.Token);
                                    if (CheckCancellation()) return false;
                                } finally {
                                    setProgress[0] = false;
                                }

                                if (installator.IsNotSupported) {
                                    FailedMessage = $"Not supported: {installator.NotSupportedMessage.ToSentenceMember()}";
                                    return false;
                                }

                                if (installator.IsPasswordCorrect) break;
                                PasswordIsInvalid = true;
                            }

                            var entries = await installator.GetEntriesAsync(
                                    progress.Subrange(0.001, 0.999, "Searching for content ({0})…"), cancellation.Token);

                            if (installator.IsNotSupported) {
                                FailedMessage = $"Not supported: {installator.NotSupportedMessage.ToSentenceMember()}";
                                return false;
                            }

                            if (entries == null) {
                                CheckCancellation(true);
                                return false;
                            }

                            foreach (var entry in entries) {
                                entry.SingleEntry = entries.Count == 1;
                                await entry.CheckExistingAsync();
                            }

                            Entries = entries.ToArray();
                            Entries.ForEach(x => x.SetInstallationParams(_installationParams));
                            ExtraOptions = (await GetExtraOptionsAsync(Entries)).ToArray();

                            if (Entries.Length == 0) {
                                return false;
                            }

                            if (CheckCancellation()) return false;

                            _taskbar?.Set(TaskbarState.Paused, 0d);
                            await WaitForConfirmation();
                            if (CheckCancellation()) return false;

                            _taskbar?.Set(TaskbarState.Indeterminate, 0d);
                            var toInstall = (await Entries.Where(x => x.Active)
                                                          .Select(x => x.GetInstallationDetails(cancellation.Token)).WhenAll(15)).ToList();
                            if (toInstall.Count == 0 || CheckCancellation()) return false;

                            string GetToInstallName(InstallationDetails details) {
                                return details.OriginalEntry?.DisplayName;
                            }

                            foreach (var extra in _installationParams.PreInstallation.NonNull()) {
                                _taskbar?.Set(TaskbarState.Indeterminate, 0d);
                                await extra(progress, cancellation.Token);
                                if (CheckCancellation()) return false;
                            }

                            foreach (var extra in ExtraOptions.Select(x => x.PreInstallation).NonNull()) {
                                await extra(progress, cancellation.Token);
                                if (CheckCancellation()) return false;
                            }

                            await Task.Run(() => FileUtils.Recycle(toInstall.SelectMany(x => x.ToRemoval).ToArray()));
                            if (CheckCancellation()) return false;

                            try {
                                foreach (var t in toInstall) {
                                    if (t.BeforeTask == null) continue;

                                    progress.Report(AsyncProgressEntry.FromStringIndetermitate($"Preparing to install {GetToInstallName(t)}…"));
                                    await t.BeforeTask(cancellation.Token);
                                    if (CheckCancellation()) return false;
                                }

                                await InstallAsync(installator, toInstall, new Progress<AsyncProgressEntry>(p => {
                                    progress.Report(p);
                                    _taskbar?.Set(p.IsIndeterminate ? TaskbarState.Indeterminate : TaskbarState.Normal, p.Progress ?? 0d);
                                }), cancellation);
                            } finally {
                                foreach (var t in toInstall) {
                                    if (t.AfterTask == null) continue;

                                    progress.Report(AsyncProgressEntry.FromStringIndetermitate($"Finishing installation {GetToInstallName(t)}…"));
                                    _taskbar?.Set(TaskbarState.Indeterminate, 0d);

                                    await t.AfterTask(cancellation.Token);
                                    if (CheckCancellation()) break;
                                }
                            }

                            if (CheckCancellation()) return false;

                            foreach (var extra in ExtraOptions.Select(x => x.PostInstallation).NonNull()) {
                                _taskbar?.Set(TaskbarState.Indeterminate, 0d);
                                await extra(progress, cancellation.Token);
                                if (CheckCancellation()) return false;
                            }

                            foreach (var extra in _installationParams.PostInstallation.NonNull()) {
                                _taskbar?.Set(TaskbarState.Indeterminate, 0d);
                                await extra(progress, cancellation.Token);
                                if (CheckCancellation()) return false;
                            }
                        }

                        return true;
                    } catch (Exception e) when (e.IsCanceled()) {
                        Cancel();
                        return false;
                    } catch (FileNotFoundException e) {
                        Logging.Warning(e);
                        FailedMessage = e.Message;
                        FailedCommentary = "Make sure file exists and available.";
                        return false;
                    } catch (Exception e) {
                        Logging.Warning(e);
                        FailedMessage = e.Message;
                        return false;
                    }
                }
            } catch (Exception e) when (e.IsCanceled()) {
                Cancel();
                return false;
            } finally {
                CancellationTokenSource = null;
                Progress = AsyncProgressEntry.Ready;
                _viewInExplorerCommand?.RaiseCanExecuteChanged();
                DisposeHelper.Dispose(ref _taskbar);
            }
        }

        private Dictionary<string, string[]> _modsPreviousLogs;
        private Dictionary<string, Tuple<string, List<string>>> _modsToInstall;
        private List<InstallationDetails> _toInstall;

        string ICopyCallback.File(IFileInfo info) {
            if (!_installationParams.AllowExecutables && ExecutablesRegex.IsMatch(info.Key) ||
                    _toInstall == null) return null;
            return _toInstall.Select(x => {
                var destination = x.CopyCallback.File(info);
                if (destination == null) return null;

                if (x.OriginalEntry.GenericModSupported && x.OriginalEntry.InstallAsGenericMod) {
                    var modName = $"({x.OriginalEntry.GenericModTypeName}) {x.OriginalEntry.Id}";
                    if (!_modsToInstall.TryGetValue(modName, out var list)) {
                        list = _modsToInstall[modName] = Tuple.Create(x.OriginalEntry.Name, new List<string>());
                    }

                    list.Item2.Add(destination);
                    SaveModBackup(modName, destination);
                }

                return destination;
            }).FirstOrDefault(x => x != null);
        }

        string ICopyCallback.Directory(IDirectoryInfo info) {
            return _toInstall?.Select(x => x.CopyCallback.Directory(info)).FirstOrDefault(x => x != null);
        }

        private async Task InstallAsync(IAdditionalContentInstallator installator, List<InstallationDetails> toInstall, IProgress<AsyncProgressEntry> progress,
                CancellationTokenSource cancellation) {
            _modsPreviousLogs = new Dictionary<string, string[]>();
            _modsToInstall = new Dictionary<string, Tuple<string, List<string>>>();

            try {
                _toInstall = toInstall;
                await installator.InstallAsync(this, progress, cancellation.Token);
            } finally {
                _toInstall = null;
                await FinishSettingMods();
            }
        }

        private void SaveModBackup(string modName, string destination) {
            if (!File.Exists(destination)) return;

            var root = AcRootDirectory.Instance.RequireValue;
            if (!FileUtils.Affects(root, destination)) return;

            var modsDirectory = SettingsHolder.GenericMods.GetModsDirectory();

            if (!_modsPreviousLogs.TryGetValue(modName, out var list)) {
                var installationLog = GenericModsEnabler.GetInstallationLogFilename(modsDirectory, modName);
                list = _modsPreviousLogs[modName] = File.Exists(installationLog) ? File.ReadAllLines(installationLog) : new string[0];
            }

            var relative = FileUtils.GetRelativePath(destination, root);
            if (list.Contains(relative)) return;

            var backupFilename = GenericModsEnabler.GetBackupFilename(modsDirectory, modName, relative);
            if (!File.Exists(backupFilename)) {
                FileUtils.EnsureFileDirectoryExists(backupFilename);

                try {
                    File.Move(destination, backupFilename);
                } catch (Exception e) {
                    Logging.Warning(e);

                    try {
                        FileUtils.HardLinkOrCopy(destination, backupFilename);
                    } catch (Exception eSerious) {
                        Logging.Error(eSerious);
                    }
                }
            }
        }

        private async Task FinishSettingMods() {
            if (_modsToInstall == null || _modsToInstall.Count == 0) return;

            var root = AcRootDirectory.Instance.RequireValue;
            var modsDirectory = SettingsHolder.GenericMods.GetModsDirectory();

            // Copying installed files as new mods
            foreach (var p in _modsToInstall) {
                var destination = Path.Combine(modsDirectory, p.Key);
                if (Directory.Exists(destination)) {
                    try {
                        await Task.Run(() => FileUtils.Recycle(destination));
                        Directory.Delete(destination, true);
                    } catch {
                        // ignored
                    }
                }

                await Task.Run(() => {
                    Directory.CreateDirectory(destination);

                    var files = p.Value.Item2.Where(x => FileUtils.Affects(root, x)).ToList();
                    File.WriteAllText(Path.Combine(destination, "name.jsgme"), p.Value.Item1);

                    foreach (var v in files) {
                        var relative = FileUtils.GetRelativePath(v, root);
                        var modFilename = Path.Combine(destination, relative);
                        FileUtils.EnsureFileDirectoryExists(modFilename);
                        FileUtils.HardLinkOrCopy(v, modFilename);
                    }

                    // Faking installation log
                    File.WriteAllLines(GenericModsEnabler.GetInstallationLogFilename(modsDirectory, p.Key),
                            files.Select(x => FileUtils.GetRelativePath(x, root)));
                });
            }

            // Marking mods as installed
            var config = new IniFile(Path.Combine(modsDirectory, GenericModsEnabler.ConfigFileName));
            foreach (var p in _modsToInstall) {
                var dependancies = config["DEPENDANCIES"];
                var dependsOn = dependancies.GetGenericModDependancies(p.Key);
                if (dependsOn?.Length > 0) {
                    // Uh-oh
                    foreach (var d in dependsOn) {
                        var v = dependancies.GetGenericModDependancies(d);
                        if (v?.Contains(p.Key) == true) continue;
                        dependancies.SetGenericModDependancies(d, (v ?? new string[0]).Append(p.Key));
                    }
                }

                config["MODS"].Set(p.Key, int.MaxValue);
                GenericModsEnabler.UpdateApplyOrder(config);
            }

            config.Save();
        }

        #region Found entries
        private ContentEntryBase[] _entries;

        public ContentEntryBase[] Entries {
            get => _entries;
            set {
                if (Equals(value, _entries)) return;
                _entries = value;
                OnPropertyChanged();

                if (value.Length == 1) {
                    DisplayFound = value[0].DisplayName;
                } else {
                    DisplayFound = PluralizingConverter.PluralizeExt(value.Length, "Found {0} {item}");
                }
            }
        }

        private string _displayFound;

        public string DisplayFound {
            get => _displayFound;
            set {
                if (Equals(value, _displayFound)) return;
                _displayFound = value;
                OnPropertyChanged();
            }
        }

        public ExtraOption[] ExtraOptions { get; set; }
        #endregion

        public void Report(AsyncProgressEntry value) {
            Progress = value;
        }

        #region Creating installator
        private static Task<IAdditionalContentInstallator> FromFile(string filename, ContentInstallationParams installationParams,
                CancellationToken cancellation) {
            if (FileUtils.IsDirectory(filename)) {
                return DirectoryContentInstallator.Create(filename, installationParams, cancellation);
            }

            if (/*!IsZipArchive(filename) &&*/ PluginsManager.Instance.IsPluginEnabled(SevenZipContentInstallator.PluginId)) {
                try {
                    return SevenZipContentInstallator.Create(filename, installationParams, cancellation);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t use 7-Zip to unpack", e);
                }
            }

            return SharpCompressContentInstallator.Create(filename, installationParams, cancellation);
        }
        #endregion

        public void Dispose() {
            DisposeHelper.Dispose(ref _taskbar);

            if (LoadedFilename != null && !KeepLoaded) {
                try {
                    File.Delete(LoadedFilename);
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }
    }
}