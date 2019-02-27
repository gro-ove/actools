using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation.Entries;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public class PatchUpdater : BaseUpdater {
        public static PatchUpdater Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new PatchUpdater();
        }

        public PatchUpdater() : base(PatchHelper.GetInstalledBuild()) {
            DisplayInstalledVersion = PatchHelper.GetInstalledVersion();
            PatchHelper.Reloaded += OnPatchHelperReload;
        }

        private void OnPatchHelperReload(object sender, EventArgs e) {
            UpdateVersions();
        }

        private void UpdateVersions() {
            InstalledVersion = PatchHelper.GetInstalledBuild();
            DisplayInstalledVersion = PatchHelper.GetInstalledVersion();
            NothingAtAll = DisplayInstalledVersion == null || InstalledVersion == null;
            ChangeCurrentVersionWithoutForcing(Versions.FirstOrDefault(x => x.Build == InstalledVersion.As(-1)), false);
        }

        private bool _nothingAtAll;

        public bool NothingAtAll {
            get => _nothingAtAll;
            set => Apply(value, ref _nothingAtAll);
        }

        private string _displayInstalledVersion;

        public string DisplayInstalledVersion {
            get => _displayInstalledVersion;
            set => Apply(value, ref _displayInstalledVersion);
        }

        private readonly Busy _busyChangingVersions = new Busy();

        private bool ChangeCurrentVersionWithoutForcing([CanBeNull] PatchVersionInfo value, bool allowInstall) {
            var oldVersion = _installedVersionInfo;
            var isToInstall = allowInstall && _installedVersionInfo?.Version != value?.Version;
            if (_installedVersionInfo == value) return false;

            _installedVersionInfo = value;
            OnPropertyChanged(nameof(InstalledVersionInfo));
            Versions.ApartFrom(value).ForEach(x => x.IsInstalled = false);
            if (value != null) {
                if (isToInstall) {
                    InstallVersion(value, oldVersion).Forget();
                }
                value.IsInstalled = true;
            }
            return true;
        }

        private PatchVersionInfo _installedVersionInfo;

        [CanBeNull]
        public PatchVersionInfo InstalledVersionInfo {
            get => _installedVersionInfo;
            set {
                if (!_busyChangingVersions.Is && ChangeCurrentVersionWithoutForcing(value, true)) {
                    ForceVersion.Value = true;
                    _reinstallCommand?.RaiseCanExecuteChanged();
                    _deleteCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private PatchVersionInfo _latestRecommendedVersion;

        [CanBeNull]
        public PatchVersionInfo LatestRecommendedVersion {
            get => _latestRecommendedVersion;
            set => Apply(value, ref _latestRecommendedVersion);
        }

        public BetterObservableCollection<PatchVersionInfo> Versions { get; } = new BetterObservableCollection<PatchVersionInfo>();

        private AsyncProgressEntry _installationProgress = AsyncProgressEntry.Finished;

        public AsyncProgressEntry InstallationProgress {
            get => _installationProgress;
            set => Apply(value, ref _installationProgress);
        }

        private const string AutoUpdateModeDisabled = "disabled";
        private const string AutoUpdateModeTested = "tested";
        private const string AutoUpdateModeRecommended = "recommended";
        private const string AutoUpdateModeLatest = "latest";

        private SettingEntry[] _autoUpdateModes;

        public SettingEntry[] AutoUpdateModes => _autoUpdateModes ?? (_autoUpdateModes = new[] {
            new SettingEntry(AutoUpdateModeLatest, "Auto-update to latest previews"),
            new SettingEntry(AutoUpdateModeRecommended, "Auto-update to recommended versions"),
            new SettingEntry(AutoUpdateModeTested, "Auto-update to well-tested versions only"),
            new SettingEntry(AutoUpdateModeDisabled, "Do not auto-update"),
        });

        private SettingEntry _autoUpdateMode;
        private bool _allowAutoDowngrade;

        public SettingEntry AutoUpdateMode {
            get => _autoUpdateMode ?? (_autoUpdateMode = AutoUpdateModes.GetByIdOrDefault(AutoUpdateModeValue.Value)
                    ?? AutoUpdateModes.GetById(AutoUpdateModeRecommended));
            set => Apply(value.EnsureFrom(AutoUpdateModes), ref _autoUpdateMode, () => {
                AutoUpdateModeValue.Value = value.Id;
                _allowAutoDowngrade = true;
                CheckAndUpdateIfNeeded().ContinueWith(t => _allowAutoDowngrade = false).Ignore();
            });
        }

        private SettingsHolder.PeriodEntry[] _periodEntries;

        public SettingsHolder.PeriodEntry[] Periods => _periodEntries ?? (_periodEntries = new[] {
            SettingsHolder.CommonSettings.PeriodStartup,
            new SettingsHolder.PeriodEntry(TimeSpan.FromMinutes(30)),
            new SettingsHolder.PeriodEntry(TimeSpan.FromHours(3)),
            new SettingsHolder.PeriodEntry(TimeSpan.FromHours(6)),
            new SettingsHolder.PeriodEntry(TimeSpan.FromDays(1))
        });

        private SettingsHolder.PeriodEntry _autoUpdatePeriod;

        public SettingsHolder.PeriodEntry AutoUpdatePeriod {
            get => _autoUpdatePeriod ?? (_autoUpdatePeriod = Periods.FirstOrDefault(x => x.TimeSpan == AutoUpdatePeriodValue.Value) ?? Periods[1]);
            set => Apply(Periods.Contains(value) ? value : Periods[1], ref _autoUpdatePeriod, () => AutoUpdatePeriodValue.Value = value.TimeSpan);
        }

        protected override TimeSpan GetUpdatePeriod() {
            return NothingAtAll ? TimeSpan.FromDays(2) : AutoUpdatePeriodValue.Value;
        }

        public StoredValue<bool> ShowDetailedChangelog { get; } = Stored.Get("/PatchUpdater.ShowDetailedChangelog", true);
        public StoredValue<TimeSpan> AutoUpdatePeriodValue { get; } = Stored.Get("/PatchUpdater.AutoUpdatePeriodValue", TimeSpan.FromMinutes(30));
        public StoredValue<string> AutoUpdateModeValue { get; } = Stored.Get("/PatchUpdater.AutoUpdateMode", "recommended");
        public StoredValue<bool> ForceVersion { get; } = Stored.Get("/PatchUpdater.ForceVersion", true);

        protected override Task GetCheckDelay() {
            // No need for regular half a second delay: PatchVersionInfo.GetPatchManifestAsync() has its own caching
            return Task.Delay(0);
        }

        private DelegateCommand _unlockCommand;

        public DelegateCommand UnlockCommand => _unlockCommand ?? (_unlockCommand = new DelegateCommand(() => {
            ForceVersion.Value = false;
            _allowAutoDowngrade = true;
            CheckAndUpdateIfNeeded().ContinueWith(t => _allowAutoDowngrade = false).Ignore();
        }, () => ForceVersion.Value && !IsInstalling).ListenOn(ForceVersion));

        public Task<bool> InstallAsync([CanBeNull] PatchVersionInfo versionInfo, CancellationToken cancellation) {
            return InstallVersion(versionInfo, InstalledVersionInfo);
        }

        private bool _isInstalling;

        public bool IsInstalling {
            get => _isInstalling;
            set => Apply(value, ref _isInstalling, () => {
                _unlockCommand?.RaiseCanExecuteChanged();
            });
        }

        private async Task<bool> InstallVersion([CanBeNull] PatchVersionInfo versionInfo, [CanBeNull] PatchVersionInfo oldVersion) {
            Logging.Debug("Installing: " + (versionInfo?.Version ?? @"null"));
            if (versionInfo?.IsInstalled != false) {
                Logging.Warning("Already installed or null");
                return false;
            }

            if (IsInstalling || ShadersPatchEntry.IsBusy) {
                Logging.Warning("Another installation is in process");
                return false;
            }

            LatestMessage = null;
            LatestError = null;

            try {
                IsInstalling = true;
                NothingAtAll = false;

                Logging.Debug("Continuing to install patch: " + versionInfo.Version);
                Logging.Debug("Old version: " + (oldVersion?.Version ?? "none"));
                await versionInfo.InstallAsync(new Progress<AsyncProgressEntry>(e => InstallationProgress = e));
                UpdateVersions();
                if (oldVersion == null) {
                    RestartPeriodicCheck();
                }
                LatestMessage = oldVersion == null ? "Installed successfully" : oldVersion.Build == versionInfo.Build ? "Reinstalled successfully"
                        : VersionComparer.Instance.Compare(oldVersion.Version, versionInfo.Version) > 0 ? "Downgraded successfully" : "Updated successfully";
                ChangeCurrentVersionWithoutForcing(versionInfo, false);
                return true;
            } catch (InformativeException e) {
                Logging.Warning(e);
                LatestError = $"{e.Message}: {e.SolutionCommentary.ToSentenceMember()}".ToSentence();
                ChangeCurrentVersionWithoutForcing(oldVersion, false);
                return false;
            } catch (UnauthorizedAccessException e) when (e.Message.Contains(@"dwrite.dll")) {
                Logging.Warning(e);
                LatestError = $"Can’t install patch: failed to replace “dwrite.dll”, is AC running?".ToSentence();
                ChangeCurrentVersionWithoutForcing(oldVersion, false);
                return false;
            } catch (Exception e) {
                Logging.Warning(e);
                LatestError = $"Can’t install patch: {e.Message.ToSentenceMember()}".ToSentence();
                ChangeCurrentVersionWithoutForcing(oldVersion, false);
                return false;
            } finally {
                InstallationProgress = AsyncProgressEntry.Finished;
                IsInstalling = false;
            }
        }

        protected override async Task<bool> CheckAndUpdateIfNeededInner() {
            if (IsInstalling || ShadersPatchEntry.IsBusy) return false;

            var manifest = await PatchVersionInfo.GetPatchManifestAsync();
            if (IsInstalling || ShadersPatchEntry.IsBusy) return false;

            Logging.Debug("Currently installed: " + (InstalledVersion ?? @"nothing"));

            var versionsList = manifest.ToList();
            var versionInfo = versionsList.FirstOrDefault(x => x.Build == InstalledVersion.As(-1));
            if (versionInfo == null) {
                versionInfo = new PatchVersionInfo {
                    Build = InstalledVersion.As(-1),
                    Version = DisplayInstalledVersion,
                    IsInstalled = true
                };
                versionsList.Add(versionInfo);
            } else {
                versionInfo.IsInstalled = true;
            }

            versionsList.Sort((a, b) => VersionComparer.Instance.Compare(b.Version, a.Version));
            ActionExtension.InvokeInMainThreadAsync(() => {
                using (_busyChangingVersions.Set()) {
                    Versions.ReplaceEverythingBy_Direct(versionsList);
                }
                ChangeCurrentVersionWithoutForcing(versionInfo, false);
                LatestRecommendedVersion = Versions.FirstOrDefault(x => x.IsRecommended)
                        ?? Versions.FirstOrDefault(x => x.IsTested) ?? Versions.FirstOrDefault();
            });

            if (AutoUpdateModeValue.Value == AutoUpdateModeDisabled) {
                LatestMessage = "Auto-update disabled";
                return false;
            }

            var latest = (AutoUpdateModeValue.Value == AutoUpdateModeTested ? versionsList.FirstOrDefault(x => x.IsTested) : null)
                    ?? (AutoUpdateModeValue.Value == AutoUpdateModeRecommended ? versionsList.FirstOrDefault(x => x.IsRecommended) : null)
                            ?? versionsList.FirstOrDefault();
            if (latest == null) {
                LatestError = "Failed to load versions information";
                return false;
            }

            Logging.Write($"Latest version: {latest.Build}, installed: {InstalledVersion}");

            if (ForceVersion.Value) {
                LatestMessage = "Manually picked version prevents auto-update";
                return false;
            }

            if (NothingAtAll) {
                LatestMessage = "Patch is not installed at all";
                return false;
            }

            if (_allowAutoDowngrade ? latest.Build == InstalledVersion.As(-1) : latest.Build <= InstalledVersion.As(-1)) {
                if (AutoUpdateModeValue.Value == AutoUpdateModeTested && latest.Build == InstalledVersion.As(-1)) {
                    LatestMessage = "Well-tested version already installed";
                } else if (AutoUpdateModeValue.Value == AutoUpdateModeRecommended && latest.Build == InstalledVersion.As(-1)) {
                    LatestMessage = "Recommended version already installed";
                } else if (versionsList.FirstOrDefault()?.Build == InstalledVersion.As(-1)) {
                    LatestMessage = "Latest version already installed";
                } else {
                    LatestMessage = "Newer version already installed";
                }
                return false;
            }

            return await InstallVersion(latest, InstalledVersionInfo);
        }

        protected override void ForcedUpdate() {
            CmApiProvider.ResetPatchDataCache(CmApiProvider.PatchDataType.Patch, string.Empty);
        }

        private AsyncCommand _reinstallCommand;

        public AsyncCommand ReinstallCommand => _reinstallCommand ?? (_reinstallCommand = new AsyncCommand(() => {
            if (InstalledVersionInfo == null) return Task.Delay(0);
            FileUtils.TryToDelete(PatchHelper.GetInstalledLog());
            InstalledVersionInfo.IsInstalled = false;
            return InstallVersion(InstalledVersionInfo, InstalledVersionInfo);
        }, () => InstalledVersionInfo != null));

        private AsyncCommand _deleteCommand;

        public AsyncCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new AsyncCommand(async () => {
            try {
                File.Delete(PatchHelper.GetMainFilename());
                await PatchVersionInfo.RemovePatch(true);
            } catch (UnauthorizedAccessException e) when (e.Message.Contains(@"dwrite.dll")) {
                Logging.Warning(e);
                LatestError = $"Can’t remove patch: failed to delete “dwrite.dll”, is AC running?".ToSentence();
            } catch (Exception e) {
                Logging.Warning(e);
                LatestError = $"Can’t remove patch: {e.Message.ToSentenceMember()}".ToSentence();
            }
        }, () => InstalledVersionInfo != null));

        protected override void OnUpdated() {
            base.OnUpdated();
            PatchHelper.Reload();
        }
    }
}