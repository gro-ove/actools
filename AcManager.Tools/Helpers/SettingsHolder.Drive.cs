using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Starters;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;

// ReSharper disable RedundantArgumentDefaultValue

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public class DriveSettings : NotifyPropertyChanged {
            internal DriveSettings() {
                if (PlayerName == null) {
                    PlayerName = new IniFile(AcPaths.GetRaceIniFilename())["CAR_0"].GetNonEmpty("DRIVER_NAME") ?? ToolsStrings.Settings_DefaultPlayerName;
                    PlayerNameOnline = PlayerName;
                }

                if (PlayerNationality == null) {
                    PlayerNationality = new IniFile(AcPaths.GetRaceIniFilename())["CAR_0"].GetPossiblyEmpty("NATIONALITY");
                }
            }

            public sealed class StarterType : Displayable, IWithId {
                internal readonly string RequiredAddonId;

                public string Id { get; }

                public string Description { get; }

                public bool RequiresSteam { get; }

                public bool IsAvailable => RequiredAddonId == null || PluginsManager.Instance.IsPluginEnabled(RequiredAddonId);

                private readonly bool _nonSelectable;

                public bool IsSelectable => !_nonSelectable && IsAvailable;

                internal StarterType([Localizable(false)] string id, string displayName, string description, string requiredAddonId = null,
                        bool nonSelectable = false, bool requiresSteam = true) {
                    Id = id;
                    DisplayName = displayName;
                    Description = description;
                    RequiresSteam = requiresSteam;

                    RequiredAddonId = requiredAddonId;
                    _nonSelectable = nonSelectable;
                }
            }

            public static StarterType DefaultStarterType => OfficialStarterType;

            public static readonly StarterType OfficialStarterType = new StarterType(
                    "Official",
                    string.Format(ToolsStrings.Common_Recommended, ToolsStrings.Settings_Starter_Official),
                    ToolsStrings.Settings_Starter_Official_Description);

            public static readonly StarterType AppIdStarterType = new StarterType(
                    "AppID",
                    "AppID",
                    "Adds “steam_appid.txt” with AC’s Steam ID to AC root folder thus allowing to run “acs.exe” directly. Thanks to [url=\"http://www.assettocorsa.net/forum/index.php?members/zkirtaem.135368/\"]@Zkirtaem[/url] for the idea.");

            public static readonly StarterType SidePassageStarterType = new StarterType(
                    "AC Service",
                    "AC Service",
                    "Replaces original launcher by a small service. Fast and reliable. Original launcher still can be used — take a look at service’s icon in system tray.\n\nJust as a reminder (press “[?]” to read complete description): original launcher is renamed as “AssettoCorsa_original.exe”.");

            public static readonly StarterType SteamStarterType = new StarterType(
                    "Steam",
                    "Replacement",
                    "For this starter, you have to replace the original “AssettoCorsa.exe” with “Content Manager.exe”. This way, CM will get an access to Steam as if it is the original launcher.",
                    nonSelectable: true, requiresSteam: false /* because it is Steam! sort of */);

            public static readonly StarterType TrickyStarterType = new StarterType(
                    "Tricky",
                    ToolsStrings.Settings_Starter_Tricky,
                    ToolsStrings.Settings_Starter_Tricky_Description);

            public static readonly StarterType UiModuleStarterType = new StarterType(
                    "UI Module",
                    ToolsStrings.Settings_Starter_UiModule,
                    ToolsStrings.Settings_Starter_UiModule_Description);

            public static readonly StarterType StarterPlusType = new StarterType(
                    "Starter+",
                    ToolsStrings.Settings_Starter_StarterPlus,
                    ToolsStrings.Settings_Starter_StarterPlus_Description,
                    StarterPlus.AddonId);

            public static readonly StarterType SseStarterType = new StarterType(
                    "SSE",
                    ToolsStrings.Settings_Starter_Sse,
                    ToolsStrings.Settings_Starter_Sse_Description,
                    SseStarter.AddonId, requiresSteam: false);

            public static readonly StarterType NaiveStarterType = new StarterType(
                    "Naive",
                    ToolsStrings.Settings_Starter_Naive,
                    ToolsStrings.Settings_Starter_Naive_Description, requiresSteam: false);

            public static readonly StarterType DeveloperStarterType = new StarterType(
                    "Developer",
                    "Developer",
                    "Doesn’t run “acs.exe” expecting you to run it manually, only prepares “race.ini” and waits for “race_out.json” to change.",
                    requiresSteam: false);

            private StarterType _selectedStarterType;

            [NotNull]
            public StarterType SelectedStarterType {
                get => _selectedStarterType ??
                        (_selectedStarterType = StarterTypes.GetByIdOrDefault(ValuesStorage.GetString("Settings.DriveSettings.SelectedStarterType")) ??
                                DefaultStarterType);
                set {
                    if (Equals(value, _selectedStarterType)) return;
                    _selectedStarterType = value;
                    ValuesStorage.Set("Settings.DriveSettings.SelectedStarterType", value.Id);
                    OnPropertyChanged();

                    if (value == UiModuleStarterType && ModuleStarter.TryToInstallModule() && ModuleStarter.IsAssettoCorsaRunning) {
                        (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).BeginInvoke(
                                (Action)(() => { ModernDialog.ShowMessage(ToolsStrings.Settings_Starter_UiModule_JustInstalled); }));
                    }

                    if (value != SidePassageStarterType) {
                        SidePassageStarter.UninstallSidePassage();
                    }
                }
            }

            private bool? _fallbackIfNotAvailable;

            public bool StarterFallbackIfNotAvailable {
                get => _fallbackIfNotAvailable ??
                        (_fallbackIfNotAvailable = ValuesStorage.GetBool("Settings.DriveSettings.FallbackIfNotAvailable", true)).Value;
                set {
                    if (Equals(value, _fallbackIfNotAvailable)) return;
                    _fallbackIfNotAvailable = value;
                    ValuesStorage.Set("Settings.DriveSettings.FallbackIfNotAvailable", value);
                    OnPropertyChanged();
                }
            }

            private bool? _acServiceStopAtExit;

            public bool AcServiceStopAtExit {
                get => _acServiceStopAtExit ?? (_acServiceStopAtExit = ValuesStorage.GetBool("Settings.DriveSettings.AcServiceStopAtExit", true)).Value;
                set {
                    if (Equals(value, _acServiceStopAtExit)) return;
                    _acServiceStopAtExit = value;
                    ValuesStorage.Set("Settings.DriveSettings.AcServiceStopAtExit", value);
                    OnPropertyChanged();
                }
            }

            private StarterType[] _starterTypes;

            public StarterType[] StarterTypes => _starterTypes ?? (_starterTypes = new[] {
                OfficialStarterType,
                AppIdStarterType,
                SidePassageStarterType,
                SteamStarterType,
                TrickyStarterType,
                UiModuleStarterType,
                // StarterPlusType,
                SseStarterType,
                NaiveStarterType,
                DeveloperStarterType
            });

            private bool? _presetsPerModeAutoUpdate;

            public bool PresetsPerModeAutoUpdate {
                get => _presetsPerModeAutoUpdate ??
                        (_presetsPerModeAutoUpdate = ValuesStorage.GetBool("Settings.DriveSettings.PresetsPerModeAutoUpdate", true)).Value;
                set {
                    if (Equals(value, _presetsPerModeAutoUpdate)) return;
                    _presetsPerModeAutoUpdate = value;
                    ValuesStorage.Set("Settings.DriveSettings.PresetsPerModeAutoUpdate", value);
                    OnPropertyChanged();
                }
            }

            private bool? _watchForSharedMemory;

            public bool WatchForSharedMemory {
                get => _watchForSharedMemory ?? (_watchForSharedMemory = ValuesStorage.GetBool("Settings.DriveSettings.WatchForSharedMemory", true)).Value;
                set {
                    if (Equals(value, _watchForSharedMemory)) return;
                    _watchForSharedMemory = value;
                    ValuesStorage.Set("Settings.DriveSettings.WatchForSharedMemory", value);
                    OnPropertyChanged();
                }
            }

            private List<string> _ignoredInterfaces;

            public IEnumerable<string> IgnoredInterfaces {
                get => _ignoredInterfaces ?? (_ignoredInterfaces = ValuesStorage.GetStringList("Settings.OnlineSettings.IgnoredInterfaces").ToList());
                set {
                    if (Equals(value, _ignoredInterfaces)) return;
                    _ignoredInterfaces = value.ToList();
                    ValuesStorage.Set("Settings.OnlineSettings.IgnoredInterfaces", value);
                    OnPropertyChanged();
                }
            }

            private bool? _hideWhileRacing;

            public bool HideWhileRacing {
                get => _hideWhileRacing ?? (_hideWhileRacing = ValuesStorage.GetBool("Settings.DriveSettings.HideWhileRacing", true)).Value;
                set {
                    if (Equals(value, _hideWhileRacing)) return;
                    _hideWhileRacing = value;
                    ValuesStorage.Set("Settings.DriveSettings.HideWhileRacing", value);
                    OnPropertyChanged();
                }
            }

            private bool? _saveDevAppsInAppsPresets;

            public bool SaveDevAppsInAppsPresets {
                get => _saveDevAppsInAppsPresets ??
                        (_saveDevAppsInAppsPresets = ValuesStorage.GetBool("Settings.DriveSettings.SaveDevAppsInAppsPresets", false)).Value;
                set {
                    if (Equals(value, _saveDevAppsInAppsPresets)) return;
                    _saveDevAppsInAppsPresets = value;
                    ValuesStorage.Set("Settings.DriveSettings.SaveDevAppsInAppsPresets", value);
                    OnPropertyChanged();
                }
            }

            private bool? _copyFilterToSystemForOculus;

            public bool CopyFilterToSystemForOculus {
                get => _copyFilterToSystemForOculus ??
                        (_copyFilterToSystemForOculus = ValuesStorage.GetBool("Settings.DriveSettings.CopyFilterToSystemForOculus", true)).Value;
                set {
                    if (Equals(value, _copyFilterToSystemForOculus)) return;
                    _copyFilterToSystemForOculus = value;
                    ValuesStorage.Set("Settings.DriveSettings.CopyFilterToSystemForOculus", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickIntegration;

            public bool SidekickIntegration {
                get => _sidekickIntegration ?? (_sidekickIntegration = ValuesStorage.GetBool("Settings.DriveSettings.SidekickIntegration", true)).Value;
                set {
                    if (Equals(value, _sidekickIntegration)) return;
                    _sidekickIntegration = value;
                    ValuesStorage.Set("Settings.DriveSettings.SidekickIntegration", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickUpdateExistingKunos;

            public bool SidekickUpdateExistingKunos {
                get => _sidekickUpdateExistingKunos ??
                        (_sidekickUpdateExistingKunos = ValuesStorage.GetBool("Settings.DriveSettings.SidekickUpdateExistingKunos", false)).Value;
                set {
                    if (Equals(value, _sidekickUpdateExistingKunos)) return;
                    _sidekickUpdateExistingKunos = value;
                    ValuesStorage.Set("Settings.DriveSettings.SidekickUpdateExistingKunos", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickUpdateExistingMods;

            public bool SidekickUpdateExistingMods {
                get => _sidekickUpdateExistingMods ??
                        (_sidekickUpdateExistingMods = ValuesStorage.GetBool("Settings.DriveSettings.SidekickUpdateExistingMods", true)).Value;
                set {
                    if (Equals(value, _sidekickUpdateExistingMods)) return;
                    _sidekickUpdateExistingMods = value;
                    ValuesStorage.Set("Settings.DriveSettings.SidekickUpdateExistingMods", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickOdometerImportValues;

            public bool SidekickOdometerImportValues {
                get => _sidekickOdometerImportValues ??
                        (_sidekickOdometerImportValues = ValuesStorage.GetBool("Settings.DriveSettings.sidekickOdometerImportValues", true)).Value;
                set {
                    if (Equals(value, _sidekickOdometerImportValues)) return;
                    _sidekickOdometerImportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.sidekickOdometerImportValues", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickOdometerExportValues;

            public bool SidekickOdometerExportValues {
                get => _sidekickOdometerExportValues ??
                        (_sidekickOdometerExportValues = ValuesStorage.GetBool("Settings.DriveSettings.sidekickOdometerExportValues", true)).Value;
                set {
                    if (Equals(value, _sidekickOdometerExportValues)) return;
                    _sidekickOdometerExportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.sidekickOdometerExportValues", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceEssentialsIntegration;

            public bool RaceEssentialsIntegration {
                get => _raceEssentialsIntegration ??
                        (_raceEssentialsIntegration = ValuesStorage.GetBool("Settings.DriveSettings.RaceEssentialsIntegration", true)).Value;
                set {
                    if (Equals(value, _raceEssentialsIntegration)) return;
                    _raceEssentialsIntegration = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceEssentialsIntegration", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceEssentialsUpdateExistingKunos;

            public bool RaceEssentialsUpdateExistingKunos {
                get => _raceEssentialsUpdateExistingKunos ??
                        (_raceEssentialsUpdateExistingKunos = ValuesStorage.GetBool("Settings.DriveSettings.RaceEssentialsUpdateExistingKunos", false)).Value;
                set {
                    if (Equals(value, _raceEssentialsUpdateExistingKunos)) return;
                    _raceEssentialsUpdateExistingKunos = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceEssentialsUpdateExistingKunos", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceEssentialsUpdateExistingMods;

            public bool RaceEssentialsUpdateExistingMods {
                get => _raceEssentialsUpdateExistingMods ??
                        (_raceEssentialsUpdateExistingMods = ValuesStorage.GetBool("Settings.DriveSettings.RaceEssentialsUpdateExistingMods", true)).Value;
                set {
                    if (Equals(value, _raceEssentialsUpdateExistingMods)) return;
                    _raceEssentialsUpdateExistingMods = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceEssentialsUpdateExistingMods", value);
                    OnPropertyChanged();
                }
            }

            private bool? _stereoOdometerImportValues;

            public bool StereoOdometerImportValues {
                get => _stereoOdometerImportValues ??
                        (_stereoOdometerImportValues = ValuesStorage.GetBool("Settings.DriveSettings.stereoOdometerImportValues", true)).Value;
                set {
                    if (Equals(value, _stereoOdometerImportValues)) return;
                    _stereoOdometerImportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.stereoOdometerImportValues", value);
                    OnPropertyChanged();
                }
            }

            private bool? _stereoOdometerExportValues;

            public bool StereoOdometerExportValues {
                get => _stereoOdometerExportValues ??
                        (_stereoOdometerExportValues = ValuesStorage.GetBool("Settings.DriveSettings.stereoOdometerExportValues", true)).Value;
                set {
                    if (Equals(value, _stereoOdometerExportValues)) return;
                    _stereoOdometerExportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.stereoOdometerExportValues", value);
                    OnPropertyChanged();
                }
            }

            private string _preCommand;

            public string PreCommand {
                get => _preCommand ?? (_preCommand = ValuesStorage.GetString("Settings.DriveSettings.PreCommand", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _preCommand)) return;
                    _preCommand = value;
                    ValuesStorage.Set("Settings.DriveSettings.PreCommand", value);
                    OnPropertyChanged();
                }
            }

            private string _postCommand;

            public string PostCommand {
                get => _postCommand ?? (_postCommand = ValuesStorage.GetString("Settings.DriveSettings.PostCommand", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _postCommand)) return;
                    _postCommand = value;
                    ValuesStorage.Set("Settings.DriveSettings.PostCommand", value);
                    OnPropertyChanged();
                }
            }

            private string _preReplayCommand;

            public string PreReplayCommand {
                get => _preReplayCommand ?? (_preReplayCommand = ValuesStorage.GetString("Settings.DriveSettings.PreReplayCommand", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _preReplayCommand)) return;
                    _preReplayCommand = value;
                    ValuesStorage.Set("Settings.DriveSettings.PreReplayCommand", value);
                    OnPropertyChanged();
                }
            }

            private string _postReplayCommand;

            public string PostReplayCommand {
                get => _postReplayCommand ?? (_postReplayCommand = ValuesStorage.GetString("Settings.DriveSettings.PostReplayCommand", ""));
                set {
                    value = value.Trim();
                    if (Equals(value, _postReplayCommand)) return;
                    _postReplayCommand = value;
                    ValuesStorage.Set("Settings.DriveSettings.PostReplayCommand", value);
                    OnPropertyChanged();
                }
            }

            private bool? _immediateStart;

            public bool ImmediateStart {
                get => _immediateStart ?? (_immediateStart = ValuesStorage.GetBool("Settings.DriveSettings.ImmediateStart", false)).Value;
                set {
                    if (Equals(value, _immediateStart)) return;
                    _immediateStart = value;
                    ValuesStorage.Set("Settings.DriveSettings.ImmediateStart", value);
                    OnPropertyChanged();
                }
            }

            private bool? _immediateCancel;

            public bool ImmediateCancel {
                get => _immediateCancel ?? (_immediateCancel = ValuesStorage.GetBool("Settings.DriveSettings.ImmediateCancel", false)).Value;
                set {
                    if (Equals(value, _immediateCancel)) return;
                    _immediateCancel = value;
                    ValuesStorage.Set("Settings.DriveSettings.ImmediateCancel", value);
                    OnPropertyChanged();
                }
            }

            private bool? _continueOnEscape;

            public bool ContinueOnEscape {
                get => _continueOnEscape ?? (_continueOnEscape = ValuesStorage.GetBool("Settings.DriveSettings.ContinueOnEscape", false)).Value;
                set {
                    if (Equals(value, _continueOnEscape)) return;
                    _continueOnEscape = value;
                    ValuesStorage.Set("Settings.DriveSettings.ContinueOnEscape", value);
                    OnPropertyChanged();
                }
            }

            private bool? _skipPracticeResults;

            public bool SkipPracticeResults {
                get => _skipPracticeResults ?? (_skipPracticeResults = ValuesStorage.GetBool("Settings.DriveSettings.SkipPracticeResults", false)).Value;
                set {
                    if (Equals(value, _skipPracticeResults)) return;
                    _skipPracticeResults = value;
                    ValuesStorage.Set("Settings.DriveSettings.SkipPracticeResults", value);
                    OnPropertyChanged();
                }
            }

            private int? _raceResultsLimit;

            public int RaceResultsLimit {
                get => _raceResultsLimit ?? (_raceResultsLimit = ValuesStorage.GetInt("Settings.DriveSettings.RaceResultsLimit", 1000)).Value;
                set {
                    value = value.Round(10);
                    if (Equals(value, _raceResultsLimit)) return;
                    _raceResultsLimit = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceResultsLimit", value);
                    OnPropertyChanged();
                }
            }

            private bool? _tryToLoadReplays;

            public bool TryToLoadReplays {
                get => _tryToLoadReplays ?? (_tryToLoadReplays = ValuesStorage.GetBool("Settings.DriveSettings.TryToLoadReplays", true)).Value;
                set {
                    if (Equals(value, _tryToLoadReplays)) return;
                    _tryToLoadReplays = value;
                    ValuesStorage.Set("Settings.DriveSettings.TryToLoadReplays", value);
                    OnPropertyChanged();
                }
            }

            private bool? _autoSaveReplays;

            public bool AutoSaveReplays {
                get => _autoSaveReplays ?? (_autoSaveReplays = ValuesStorage.GetBool("Settings.DriveSettings.AutoSaveReplays", false)).Value;
                set {
                    if (Equals(value, _autoSaveReplays)) return;
                    _autoSaveReplays = value;
                    ValuesStorage.Set("Settings.DriveSettings.AutoSaveReplays", value);
                    OnPropertyChanged();
                }
            }

            private bool? _autoAddReplaysExtension;

            public bool AutoAddReplaysExtension {
                get => _autoAddReplaysExtension ??
                        (_autoAddReplaysExtension = ValuesStorage.GetBool("Settings.DriveSettings.AutoAddReplaysExtension", true)).Value;
                set {
                    if (Equals(value, _autoAddReplaysExtension)) return;
                    _autoAddReplaysExtension = value;
                    ValuesStorage.Set("Settings.DriveSettings.AutoAddReplaysExtension", value);
                    OnPropertyChanged();
                }
            }

            public string DefaultReplaysNameFormat => @"_autosave_{car.id}_{track.id}_{date_ac}.acreplay";

            private string _replaysNameFormat;

            public string ReplaysNameFormat {
                get
                        =>
                                _replaysNameFormat ??
                                        (_replaysNameFormat = ValuesStorage.GetString("Settings.DriveSettings.ReplaysNameFormat", DefaultReplaysNameFormat));
                set {
                    value = value?.Trim();
                    if (Equals(value, _replaysNameFormat)) return;
                    _replaysNameFormat = value;
                    ValuesStorage.Set("Settings.DriveSettings.ReplaysNameFormat", value);
                    OnPropertyChanged();
                }
            }

            private bool? _use32BitVersion;

            public bool Use32BitVersion {
                get => _use32BitVersion ?? (_use32BitVersion = ValuesStorage.GetBool("Settings.DriveSettings.Use32BitVersion", false)).Value;
                set {
                    if (Equals(value, _use32BitVersion)) return;
                    _use32BitVersion = value;
                    ValuesStorage.Set("Settings.DriveSettings.Use32BitVersion", value);
                    OnPropertyChanged();
                }
            }

            private bool? _runSteamIfNeeded;

            public bool RunSteamIfNeeded {
                get => _runSteamIfNeeded ?? (_runSteamIfNeeded = ValuesStorage.GetBool("Settings.DriveSettings.RunSteamIfNeeded", true)).Value;
                set {
                    if (Equals(value, _runSteamIfNeeded)) return;
                    _runSteamIfNeeded = value;
                    ValuesStorage.Set("Settings.DriveSettings.RunSteamIfNeeded", value);
                    OnPropertyChanged();
                }
            }

            private string _playerName;

            public string PlayerName {
                get => _playerName ?? (_playerName = ValuesStorage.GetString("Settings.DriveSettings.PlayerName", null));
                set {
                    value = value?.Trim();
                    if (Equals(value, _playerName)) return;
                    _playerName = value;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerName", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PlayerNameOnline));
                }
            }

            private string _playerNationality;

            public string PlayerNationality {
                get => _playerNationality ?? (_playerNationality = ValuesStorage.GetString("Settings.DriveSettings.PlayerNationality", null));
                set {
                    value = value?.Trim();
                    if (Equals(value, _playerNationality)) return;
                    _playerNationality = value;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerNationality", value);
                    OnPropertyChanged();
                }
            }

            private bool? _differentPlayerNameOnline;

            public bool DifferentPlayerNameOnline {
                get => _differentPlayerNameOnline ??
                        (_differentPlayerNameOnline = ValuesStorage.GetBool("Settings.DriveSettings.DifferentPlayerNameOnline", false)).Value;
                set {
                    if (Equals(value, _differentPlayerNameOnline)) return;
                    _differentPlayerNameOnline = value;
                    ValuesStorage.Set("Settings.DriveSettings.DifferentPlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            private string _playerNameOnline;

            public string PlayerNameOnline {
                get => _playerNameOnline ?? (_playerNameOnline = ValuesStorage.GetString("Settings.DriveSettings.PlayerNameOnline", PlayerName));
                set {
                    value = value.Trim();
                    if (Equals(value, _playerNameOnline)) return;
                    _playerNameOnline = value;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveTrackDayViaPractice;

            public bool QuickDriveTrackDayViaPractice {
                get => _quickDriveTrackDayViaPractice ??
                        (_quickDriveTrackDayViaPractice = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveTrackDayViaPractice", true)).Value;
                set {
                    if (Equals(value, _quickDriveTrackDayViaPractice)) return;
                    _quickDriveTrackDayViaPractice = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveTrackDayViaPractice", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveAllowCustomData;

            public bool QuickDriveAllowCustomData {
                get
                        =>
                                _quickDriveAllowCustomData
                                        ?? (_quickDriveAllowCustomData = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveAllowCustomData", false)).Value
                        ;
                set {
                    if (Equals(value, _quickDriveAllowCustomData)) return;
                    _quickDriveAllowCustomData = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveAllowCustomData", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveExpandBounds;

            public bool QuickDriveExpandBounds {
                get => _quickDriveExpandBounds ?? (_quickDriveExpandBounds = ValuesStorage.GetBool("Settings.DriveSettings.ExpandBounds", false)).Value;
                set {
                    if (Equals(value, _quickDriveExpandBounds)) return;
                    _quickDriveExpandBounds = value;
                    ValuesStorage.Set("Settings.DriveSettings.ExpandBounds", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AiLevelMinimum));
                }
            }

            private bool? _quickDriveAiLimitations;

            public bool QuickDriveAiLimitations {
                get
                        =>
                                _quickDriveAiLimitations
                                        ?? (_quickDriveAiLimitations = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveAiLimitations", false)).Value;
                set {
                    if (Equals(value, _quickDriveAiLimitations)) return;
                    _quickDriveAiLimitations = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveAiLimitations", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveAiLevelInName;

            public bool QuickDriveAiLevelInName {
                get => _quickDriveAiLevelInName ?? (_quickDriveAiLevelInName = ValuesStorage.GetBool("RaceGrid.AiLevelInDriverName", false)).Value;
                set {
                    if (Equals(value, _quickDriveAiLevelInName)) return;
                    _quickDriveAiLevelInName = value;
                    ValuesStorage.Set("RaceGrid.AiLevelInDriverName", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveAiAggressionInName;

            public bool QuickDriveAiAggressionInName {
                get
                        =>
                                _quickDriveAiAggressionInName ??
                                        (_quickDriveAiAggressionInName = ValuesStorage.GetBool("RaceGrid.AiAggressionInDriverName", false)).Value;
                set {
                    if (Equals(value, _quickDriveAiAggressionInName)) return;
                    _quickDriveAiAggressionInName = value;
                    ValuesStorage.Set("RaceGrid.AiAggressionInDriverName", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveUseSkinNames;

            public bool QuickDriveUseSkinNames {
                get => _quickDriveUseSkinNames ?? (_quickDriveUseSkinNames = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveUseSkinNames", true)).Value
                        ;
                set {
                    if (Equals(value, _quickDriveUseSkinNames)) return;
                    _quickDriveUseSkinNames = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveUseSkinNames", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveCheckTrack;

            public bool QuickDriveCheckTrack {
                get => _quickDriveCheckTrack ?? (_quickDriveCheckTrack = ValuesStorage.GetBool("Settings.DriveSettings.QuickDriveCheckTrack", true)).Value;
                set {
                    if (Equals(value, _quickDriveCheckTrack)) return;
                    _quickDriveCheckTrack = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveCheckTrack", value);
                    OnPropertyChanged();
                }
            }

            private bool? _alwaysRecordGhost;

            public bool AlwaysRecordGhost {
                get => _alwaysRecordGhost ?? (_alwaysRecordGhost = ValuesStorage.GetBool("Settings.DriveSettings.AlwaysRecordGhost", false)).Value;
                set {
                    if (Equals(value, _alwaysRecordGhost)) return;
                    _alwaysRecordGhost = value;
                    ValuesStorage.Set("Settings.DriveSettings.AlwaysRecordGhost", value);
                    OnPropertyChanged();
                }
            }

            public int AiLevelMinimum => QuickDriveExpandBounds ? 30 : 70;

            private bool? _kunosCareerUserAiLevel;

            public bool KunosCareerUserAiLevel {
                get => _kunosCareerUserAiLevel ?? (_kunosCareerUserAiLevel = ValuesStorage.GetBool("Settings.DriveSettings.KunosCareerUserAiLevel", true)).Value
                        ;
                set {
                    if (Equals(value, _kunosCareerUserAiLevel)) return;
                    _kunosCareerUserAiLevel = value;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserAiLevel", value);
                    OnPropertyChanged();
                }
            }

            private bool? _kunosCareerUserSkin;

            public bool KunosCareerUserSkin {
                get => _kunosCareerUserSkin ?? (_kunosCareerUserSkin = ValuesStorage.GetBool("Settings.DriveSettings.KunosCareerUserSkin", true)).Value;
                set {
                    if (Equals(value, _kunosCareerUserSkin)) return;
                    _kunosCareerUserSkin = value;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserSkin", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickSwitches;

            public bool QuickSwitches {
                get => _quickSwitches ?? (_quickSwitches = ValuesStorage.GetBool("Settings.DriveSettings.QuickSwitches", true)).Value;
                set {
                    if (Equals(value, _quickSwitches)) return;
                    _quickSwitches = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickSwitches", value);
                    OnPropertyChanged();
                }
            }

            private string[] _quickSwitchesList;

            public string[] QuickSwitchesList {
                get => _quickSwitchesList ??
                        (_quickSwitchesList = ValuesStorage.GetStringList("Settings.DriveSettings.QuickSwitchesList", new[] {
                            @"WidgetExposure",
                            @"WidgetUiPresets",
                            @"WidgetHideDriveArms",
                            @"WidgetHideSteeringWheel"
                        }).ToArray());
                set {
                    if (Equals(value, _quickSwitchesList)) return;
                    ValuesStorage.Set("Settings.DriveSettings.QuickSwitchesList", value);
                    _quickSwitchesList = value;
                    OnPropertyChanged();
                }
            }

            private bool? _automaticallyConvertBmpToJpg;

            public bool AutomaticallyConvertBmpToJpg {
                get => _automaticallyConvertBmpToJpg ??
                        (_automaticallyConvertBmpToJpg = ValuesStorage.GetBool("Settings.DriveSettings.AutomaticallyConvertBmpToJpg", false)).Value;
                set {
                    if (Equals(value, _automaticallyConvertBmpToJpg)) return;
                    _automaticallyConvertBmpToJpg = value;
                    ValuesStorage.Set("Settings.DriveSettings.AutomaticallyConvertBmpToJpg", value);
                    OnPropertyChanged();
                }
            }

            private string _localAddress;

            [CanBeNull]
            public string LocalAddress {
                get => _localAddress ?? (_localAddress = ValuesStorage.GetString("Settings.DriveSettings.LocalAddress", null));
                set {
                    value = value?.Trim();
                    if (Equals(value, _localAddress)) return;
                    _localAddress = value;
                    ValuesStorage.Set("Settings.DriveSettings.LocalAddress", value);
                    OnPropertyChanged();
                }
            }

            private bool? _weatherSpecificClouds;

            public bool WeatherSpecificClouds {
                get => _weatherSpecificClouds ?? (_weatherSpecificClouds = ValuesStorage.GetBool("Settings.DriveSettings.WeatherSpecificClouds", true)).Value;
                set {
                    if (Equals(value, _weatherSpecificClouds)) return;
                    _weatherSpecificClouds = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificClouds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _weatherSpecificPpFilter;

            public bool WeatherSpecificPpFilter {
                get
                        =>
                                _weatherSpecificPpFilter ??
                                        (_weatherSpecificPpFilter = ValuesStorage.GetBool("Settings.DriveSettings.WeatherSpecificPpFilter", true)).Value;
                set {
                    if (Equals(value, _weatherSpecificPpFilter)) return;
                    _weatherSpecificPpFilter = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificPpFilter", value);
                    OnPropertyChanged();
                }
            }

            private bool? _weatherSpecificTyreSmoke;

            public bool WeatherSpecificTyreSmoke {
                get => _weatherSpecificTyreSmoke ??
                        (_weatherSpecificTyreSmoke = ValuesStorage.GetBool("Settings.DriveSettings.WeatherSpecificTyreSmoke", true)).Value;
                set {
                    if (Equals(value, _weatherSpecificTyreSmoke)) return;
                    _weatherSpecificTyreSmoke = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificTyreSmoke", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rhmIntegration;

            public bool RhmIntegration {
                get => _rhmIntegration ??
                        (_rhmIntegration = ValuesStorage.GetBool("Settings.DriveSettings.RhmIntegration", false)).Value;
                set {
                    if (Equals(value, _rhmIntegration)) return;
                    _rhmIntegration = value;
                    ValuesStorage.Set("Settings.DriveSettings.RhmIntegration", value);
                    OnPropertyChanged();
                }
            }

            private string _rhmLocation;

            [CanBeNull]
            public string RhmLocation {
                get => _rhmLocation ?? (_rhmLocation = ValuesStorage.GetString("Settings.DriveSettings.RhmLocation", null));
                set {
                    value = value?.Trim();
                    if (Equals(value, _rhmLocation)) return;
                    _rhmLocation = value;
                    ValuesStorage.Set("Settings.DriveSettings.RhmLocation", value);
                    OnPropertyChanged();
                }
            }

            private string _rhmSettingsLocation;

            public string RhmSettingsLocation {
                get => _rhmSettingsLocation ?? (_rhmSettingsLocation = ValuesStorage.GetString("Settings.DriveSettings.RhmSettingsLocation",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RealHeadMotion", "Settings.xml")));
                set {
                    value = value.Trim();
                    if (Equals(value, _rhmSettingsLocation)) return;
                    _rhmSettingsLocation = value;
                    ValuesStorage.Set("Settings.DriveSettings.RhmSettingsLocation", value);
                    OnPropertyChanged();
                }
            }

            private DelayEntry[] _rhmKeepAlivePeriods;

            public DelayEntry[] RhmKeepAlivePeriods => _rhmKeepAlivePeriods ?? (_rhmKeepAlivePeriods = new[] {
                new DelayEntry(TimeSpan.Zero),
                new DelayEntry(TimeSpan.FromMinutes(2)),
                new DelayEntry(TimeSpan.FromMinutes(5)),
                new DelayEntry(TimeSpan.FromMinutes(15)),
                new DelayEntry(TimeSpan.FromMinutes(30)),
                new DelayEntry(TimeSpan.FromHours(1)),
                new DelayEntry(TimeSpan.FromHours(3))
            });

            private DelayEntry _rhmKeepAlivePeriod;

            public DelayEntry RhmKeepAlivePeriod {
                get {
                    var saved = ValuesStorage.GetTimeSpan("Settings.DriveSettings.RhmKeepAlivePeriod");
                    return _rhmKeepAlivePeriod ?? (_rhmKeepAlivePeriod = RhmKeepAlivePeriods.FirstOrDefault(x => x.TimeSpan == saved) ??
                            RhmKeepAlivePeriods.ElementAt(4));
                }
                set {
                    if (Equals(value, _rhmKeepAlivePeriod)) return;
                    _rhmKeepAlivePeriod = value;
                    ValuesStorage.Set("Settings.DriveSettings.RhmKeepAlivePeriod", value.TimeSpan);
                    OnPropertyChanged();
                }
            }

            private DelegateCommand _selectRhmLocationCommand;

            public DelegateCommand SelectRhmLocationCommand => _selectRhmLocationCommand ?? (_selectRhmLocationCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = "Real Head Motion|RealHeadMotionAssettoCorsa.exe|Applications (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select Real Head Motion application",
                    InitialDirectory = Path.GetDirectoryName(RhmLocation) ?? "",
                    FileName = Path.GetFileName(RhmLocation) ?? ""
                };

                if (dialog.ShowDialog() == true) {
                    RhmLocation = dialog.FileName;
                }
            }));

            private DelegateCommand _selectRhmSettingsLocationCommand;

            public DelegateCommand SelectRhmSettingsLocationCommand
                => _selectRhmSettingsLocationCommand ?? (_selectRhmSettingsLocationCommand = new DelegateCommand(() => {
                    var dialog = new OpenFileDialog {
                        Filter = "Real Head Motion Settings|Settings.xml|XML Files (*.xml)|*.xml|All files (*.*)|*.*",
                        Title = "Select Real Head Motion settings",
                        InitialDirectory = Path.GetDirectoryName(RhmSettingsLocation) ?? "",
                        FileName = Path.GetFileName(RhmSettingsLocation) ?? ""
                    };

                    if (dialog.ShowDialog() == true) {
                        RhmSettingsLocation = dialog.FileName;
                    }
                }));

            private bool? _checkAndFixControlsOrder;

            public bool CheckAndFixControlsOrder {
                get => _checkAndFixControlsOrder
                        ?? (_checkAndFixControlsOrder = ValuesStorage.GetBool("Settings.DriveSettings.CheckAndFixControlsOrder", false)).Value;
                set {
                    if (Equals(value, _checkAndFixControlsOrder)) return;
                    _checkAndFixControlsOrder = value;
                    ValuesStorage.Set("Settings.DriveSettings.CheckAndFixControlsOrder", value);
                    OnPropertyChanged();
                }
            }
        }

        private static DriveSettings _drive;
        public static DriveSettings Drive => _drive ?? (_drive = new DriveSettings());
    }
}