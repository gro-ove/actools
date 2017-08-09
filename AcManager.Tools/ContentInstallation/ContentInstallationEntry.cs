using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Entries;
using AcManager.Tools.ContentInstallation.Implementations;
using AcManager.Tools.ContentInstallation.Installators;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
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
using JetBrains.Annotations;
using Microsoft.VisualBasic;

namespace AcManager.Tools.ContentInstallation {
    public partial class ContentInstallationEntry : NotifyPropertyChanged, IProgress<AsyncProgressEntry>, ICopyCallback, IDisposable {
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
            InformationUrl = _installationParams.InformationUrl;
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

        public DelegateCommand CancelCommand => _cancelCommand ?? (_cancelCommand = new DelegateCommand(Cancel,
                () => _cancellationTokenSource != null || Failed != null && !Cancelled));

        private void Cancel() {
            Cancelled = true;
            Failed = "Cancelled";
            _cancellationTokenSource?.Cancel();
            DisposeHelper.Dispose(ref _taskbar);
        }

        private string _failed;

        public string Failed {
            get => _failed;
            set {
                if (Equals(value, _failed)) return;
                _failed = value;
                OnPropertyChanged();
                _cancelCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _cancelled;

        public bool Cancelled {
            get => _cancelled;
            set {
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
            get => _displayName;
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
                _viewInExplorerCommand?.RaiseCanExecuteChanged();
            }
        }

        private DelegateCommand _viewInExplorerCommand;

        public DelegateCommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new DelegateCommand(() => {
            KeepLoaded = true;
            WindowsHelper.ViewFile(LoadedFilename);
        }, () => LoadedFilename != null && File.Exists(LoadedFilename)));

        private string GetLoaderDestination() {
            var directory = SettingsHolder.Content.TemporaryFilesLocationValue;
            var fileName = FileUtils.EnsureFileNameIsValid(
                    Source.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).Last().Split('?')[0]).Trim();

            if (string.IsNullOrWhiteSpace(fileName)) {
                fileName = FileUtils.EnsureFileNameIsValid(Source).Trim();
                if (string.IsNullOrWhiteSpace(fileName)) {
                    fileName = "download";
                }
            }

            fileName = Regex.Replace(fileName, @"\.(?:asp|cgi|p(?:hp3?|l)|s?html?)$", "", RegexOptions.IgnoreCase);
            return FileUtils.EnsureUnique(Path.Combine(directory, fileName));
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

        public async Task<bool> RunAsyncInner() {
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

                    string localFilename;

                    // Load remote file if it is remote
                    if (ContentInstallationManager.IsRemoteSource(Source)) {
                        progress.Report(AsyncProgressEntry.FromStringIndetermitate("Downloading…"));
                        _taskbar?.Set(TaskbarState.Indeterminate, 0d);

                        try {
                            LoadedFilename = GetLoaderDestination();
                            await FlexibleLoader.LoadAsyncTo(Source,
                                    LoadedFilename,
                                    metaInformationCallback: information => {
                                        if (information.FileName != null && information.FileName != Path.GetFileName(Source)) {
                                            FileName = information.FileName;
                                        }

                                        if (Version == null) {
                                            Version = information.Version;
                                        }
                                    },
                                    progress: new Progress<AsyncProgressEntry>(v => {
                                        var msg = string.IsNullOrWhiteSpace(v.Message) ? "Downloading…" : $"Downloading ({v.Message.ToSentenceMember()})…";
                                        if (v.Progress == 0d || v.Progress == null) {
                                            progress.Report(AsyncProgressEntry.FromStringIndetermitate(msg));
                                            _taskbar?.Set(TaskbarState.Indeterminate, 0d);
                                        } else {
                                            progress.Report(msg, v.Progress * 0.9999);
                                            _taskbar?.Set(TaskbarState.Normal, v.Progress ?? 0d);
                                        }
                                    }),
                                    cancellation: cancellation.Token);
                            localFilename = LoadedFilename;
                            if (CheckCancellation()) return false;
                        } catch (Exception e) when (e.IsCanceled()) {
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
                        _taskbar?.Set(TaskbarState.Indeterminate, 0d);

                        // Scan for content
                        using (var installator = await FromFile(localFilename, _installationParams, cancellation.Token)) {
                            if (CheckCancellation()) return false;

                            if (installator is SharpCompressContentInstallator || installator is ZipContentInstallator) {
                                SevenZipInstallatorWouldNotHurt = true;
                            }

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
                                _taskbar?.Set(TaskbarState.Paused, 0d);

                                var password = await WaitForPassword();
                                if (CheckCancellation()) return false;

                                progress.Report(AsyncProgressEntry.FromStringIndetermitate("Checking password…"));
                                _taskbar?.Set(TaskbarState.Indeterminate, 0d);

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

                            if (entries.Count == 0) {
                                Failed = "Nothing to install";
                                return false;
                            }

                            foreach (var entry in entries) {
                                entry.SingleEntry = entries.Count == 1;
                                await entry.CheckExistingAsync();
                            }

                            Entries = entries.ToArray();
                            ExtraOptions = (await GetExtraOptionsAsync(Entries)).ToArray();

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
                        }

                        return true;
                    } catch (Exception e) when (e.IsCanceled()) {
                        Cancel();
                        return false;
                    } catch (Exception e) {
                        Failed = "Can’t find content: " + e.Message.ToSentenceMember();
                        Logging.Warning(e);
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
            if (!FileUtils.IsAffected(root, destination)) return;

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

                    var files = p.Value.Item2.Where(x => FileUtils.IsAffected(root, x)).ToList();
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

        private const int ZipLeadBytes = 0x04034b50;

        private static bool IsZipArchive(string filename) {
            try {
                using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    var bytes = new byte[4];
                    fs.Read(bytes, 0, 4);
                    return BitConverter.ToInt32(bytes, 0) == ZipLeadBytes;
                }
            } catch (Exception e) {
                Logging.Warning(e);
                return false;
            }
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