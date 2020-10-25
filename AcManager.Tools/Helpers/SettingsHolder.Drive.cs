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
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

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

            public static StarterType DefaultStarterType => AppIdStarterType;

            public static readonly StarterType OfficialStarterType = new StarterType(
                    "Official",
                    ToolsStrings.Settings_Starter_Official,
                    ToolsStrings.Settings_Starter_Official_Description);

            public static readonly StarterType AppIdStarterType = new StarterType(
                    "AppID",
                    // string.Format(ToolsStrings.Common_Recommended, "AppID"),
                    "AppID",
                    "Adds “steam_appid.txt” with AC’s Steam ID to AC root folder thus allowing to run “acs.exe” directly. Thanks to [url=\"http://www.assettocorsa.net/forum/index.php?members/zkirtaem.135368/\"]@Zkirtaem[/url] for the idea.");

            public static readonly StarterType SidePassageStarterType = new StarterType(
                    "AC Service",
                    "AC Service",
                    "Replaces original launcher by a small service. Fast and reliable. Original launcher still can be used — take a look at service’s icon in system tray.\n\nJust as a reminder (press “?” to read complete description): original launcher is renamed as “AssettoCorsa_original.exe”.");

            public static readonly StarterType SteamStarterType = new StarterType(
                    "Steam",
                    "Steam",
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
                get => _selectedStarterType
                        ?? (_selectedStarterType = StarterTypes.GetByIdOrDefault(ValuesStorage.Get<string>("Settings.DriveSettings.SelectedStarterType")) ??
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
                get => _fallbackIfNotAvailable
                        ?? (_fallbackIfNotAvailable = ValuesStorage.Get("Settings.DriveSettings.FallbackIfNotAvailable", true)).Value;
                set {
                    if (Equals(value, _fallbackIfNotAvailable)) return;
                    _fallbackIfNotAvailable = value;
                    ValuesStorage.Set("Settings.DriveSettings.FallbackIfNotAvailable", value);
                    OnPropertyChanged();
                }
            }

            private bool? _acServiceStopAtExit;

            public bool AcServiceStopAtExit {
                get => _acServiceStopAtExit
                        ?? (_acServiceStopAtExit = ValuesStorage.Get("Settings.DriveSettings.AcServiceStopAtExit", true)).Value;
                set {
                    if (Equals(value, _acServiceStopAtExit)) return;
                    _acServiceStopAtExit = value;
                    ValuesStorage.Set("Settings.DriveSettings.AcServiceStopAtExit", value);
                    OnPropertyChanged();
                }
            }

            private StarterType[] _starterTypes;

            public StarterType[] StarterTypes => _starterTypes
                    ?? (_starterTypes = new[] {
                        AppIdStarterType,
                        OfficialStarterType,
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
                get => _presetsPerModeAutoUpdate
                        ?? (_presetsPerModeAutoUpdate = ValuesStorage.Get("Settings.DriveSettings.PresetsPerModeAutoUpdate", true)).Value;
                set {
                    if (Equals(value, _presetsPerModeAutoUpdate)) return;
                    _presetsPerModeAutoUpdate = value;
                    ValuesStorage.Set("Settings.DriveSettings.PresetsPerModeAutoUpdate", value);
                    OnPropertyChanged();
                }
            }

            private List<string> _ignoredInterfaces;

            public IEnumerable<string> IgnoredInterfaces {
                get => _ignoredInterfaces
                        ?? (_ignoredInterfaces = ValuesStorage.GetStringList("Settings.OnlineSettings.IgnoredInterfaces").ToList());
                set {
                    if (Equals(value, _ignoredInterfaces)) return;
                    _ignoredInterfaces = value.ToList();
                    ValuesStorage.Storage.SetStringList("Settings.OnlineSettings.IgnoredInterfaces", value);
                    OnPropertyChanged();
                }
            }

            private bool? _hideWhileRacing;

            public bool HideWhileRacing {
                get => _hideWhileRacing
                        ?? (_hideWhileRacing = ValuesStorage.Get("Settings.DriveSettings.HideWhileRacing", true)).Value;
                set {
                    if (Equals(value, _hideWhileRacing)) return;
                    _hideWhileRacing = value;
                    ValuesStorage.Set("Settings.DriveSettings.HideWhileRacing", value);
                    OnPropertyChanged();
                }
            }

            private bool? _saveDevAppsInAppsPresets;

            public bool SaveDevAppsInAppsPresets {
                get => _saveDevAppsInAppsPresets
                        ?? (_saveDevAppsInAppsPresets = ValuesStorage.Get("Settings.DriveSettings.SaveDevAppsInAppsPresets", false)).Value;
                set {
                    if (Equals(value, _saveDevAppsInAppsPresets)) return;
                    _saveDevAppsInAppsPresets = value;
                    ValuesStorage.Set("Settings.DriveSettings.SaveDevAppsInAppsPresets", value);
                    OnPropertyChanged();
                }
            }

            private bool? _copyFilterToSystemForOculus;

            public bool CopyFilterToSystemForOculus {
                get => _copyFilterToSystemForOculus
                        ?? (_copyFilterToSystemForOculus = ValuesStorage.Get("Settings.DriveSettings.CopyFilterToSystemForOculus", true)).Value;
                set {
                    if (Equals(value, _copyFilterToSystemForOculus)) return;
                    _copyFilterToSystemForOculus = value;
                    ValuesStorage.Set("Settings.DriveSettings.CopyFilterToSystemForOculus", value);
                    OnPropertyChanged();
                }
            }

            private bool? _camberExtravaganzaIntegration;

            public bool CamberExtravaganzaIntegration {
                get => _camberExtravaganzaIntegration
                        ?? (_camberExtravaganzaIntegration = ValuesStorage.Get("Settings.DriveSettings.CamberExtravaganzaIntegration", true)).Value;
                set {
                    if (Equals(value, _camberExtravaganzaIntegration)) return;
                    _camberExtravaganzaIntegration = value;
                    ValuesStorage.Set("Settings.DriveSettings.CamberExtravaganzaIntegration", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickIntegration;

            public bool SidekickIntegration {
                get => _sidekickIntegration
                        ?? (_sidekickIntegration = ValuesStorage.Get("Settings.DriveSettings.SidekickIntegration", true)).Value;
                set {
                    if (Equals(value, _sidekickIntegration)) return;
                    _sidekickIntegration = value;
                    ValuesStorage.Set("Settings.DriveSettings.SidekickIntegration", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickUpdateExistingKunos;

            public bool SidekickUpdateExistingKunos {
                get => _sidekickUpdateExistingKunos
                        ?? (_sidekickUpdateExistingKunos = ValuesStorage.Get("Settings.DriveSettings.SidekickUpdateExistingKunos", false)).Value;
                set {
                    if (Equals(value, _sidekickUpdateExistingKunos)) return;
                    _sidekickUpdateExistingKunos = value;
                    ValuesStorage.Set("Settings.DriveSettings.SidekickUpdateExistingKunos", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickUpdateExistingMods;

            public bool SidekickUpdateExistingMods {
                get => _sidekickUpdateExistingMods
                        ?? (_sidekickUpdateExistingMods = ValuesStorage.Get("Settings.DriveSettings.SidekickUpdateExistingMods", true)).Value;
                set {
                    if (Equals(value, _sidekickUpdateExistingMods)) return;
                    _sidekickUpdateExistingMods = value;
                    ValuesStorage.Set("Settings.DriveSettings.SidekickUpdateExistingMods", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickOdometerImportValues;

            public bool SidekickOdometerImportValues {
                get => _sidekickOdometerImportValues
                        ?? (_sidekickOdometerImportValues = ValuesStorage.Get("Settings.DriveSettings.sidekickOdometerImportValues", true)).Value;
                set {
                    if (Equals(value, _sidekickOdometerImportValues)) return;
                    _sidekickOdometerImportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.sidekickOdometerImportValues", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sidekickOdometerExportValues;

            public bool SidekickOdometerExportValues {
                get => _sidekickOdometerExportValues
                        ?? (_sidekickOdometerExportValues = ValuesStorage.Get("Settings.DriveSettings.sidekickOdometerExportValues", true)).Value;
                set {
                    if (Equals(value, _sidekickOdometerExportValues)) return;
                    _sidekickOdometerExportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.sidekickOdometerExportValues", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceEssentialsIntegration;

            public bool RaceEssentialsIntegration {
                get => _raceEssentialsIntegration
                        ?? (_raceEssentialsIntegration = ValuesStorage.Get("Settings.DriveSettings.RaceEssentialsIntegration", true)).Value;
                set {
                    if (Equals(value, _raceEssentialsIntegration)) return;
                    _raceEssentialsIntegration = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceEssentialsIntegration", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceEssentialsUpdateExistingKunos;

            public bool RaceEssentialsUpdateExistingKunos {
                get => _raceEssentialsUpdateExistingKunos
                        ?? (_raceEssentialsUpdateExistingKunos = ValuesStorage.Get("Settings.DriveSettings.RaceEssentialsUpdateExistingKunos", false)).Value;
                set {
                    if (Equals(value, _raceEssentialsUpdateExistingKunos)) return;
                    _raceEssentialsUpdateExistingKunos = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceEssentialsUpdateExistingKunos", value);
                    OnPropertyChanged();
                }
            }

            private bool? _raceEssentialsUpdateExistingMods;

            public bool RaceEssentialsUpdateExistingMods {
                get => _raceEssentialsUpdateExistingMods
                        ?? (_raceEssentialsUpdateExistingMods = ValuesStorage.Get("Settings.DriveSettings.RaceEssentialsUpdateExistingMods", true)).Value;
                set {
                    if (Equals(value, _raceEssentialsUpdateExistingMods)) return;
                    _raceEssentialsUpdateExistingMods = value;
                    ValuesStorage.Set("Settings.DriveSettings.RaceEssentialsUpdateExistingMods", value);
                    OnPropertyChanged();
                }
            }

            private bool? _stereoOdometerImportValues;

            public bool StereoOdometerImportValues {
                get => _stereoOdometerImportValues
                        ?? (_stereoOdometerImportValues = ValuesStorage.Get("Settings.DriveSettings.stereoOdometerImportValues", true)).Value;
                set {
                    if (Equals(value, _stereoOdometerImportValues)) return;
                    _stereoOdometerImportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.stereoOdometerImportValues", value);
                    OnPropertyChanged();
                }
            }

            private bool? _stereoOdometerExportValues;

            public bool StereoOdometerExportValues {
                get => _stereoOdometerExportValues
                        ?? (_stereoOdometerExportValues = ValuesStorage.Get("Settings.DriveSettings.stereoOdometerExportValues", true)).Value;
                set {
                    if (Equals(value, _stereoOdometerExportValues)) return;
                    _stereoOdometerExportValues = value;
                    ValuesStorage.Set("Settings.DriveSettings.stereoOdometerExportValues", value);
                    OnPropertyChanged();
                }
            }

            private string _preCommand;

            public string PreCommand {
                get => _preCommand
                        ?? (_preCommand = ValuesStorage.Get("Settings.DriveSettings.PreCommand", ""));
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
                get => _postCommand
                        ?? (_postCommand = ValuesStorage.Get("Settings.DriveSettings.PostCommand", ""));
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
                get => _preReplayCommand
                        ?? (_preReplayCommand = ValuesStorage.Get("Settings.DriveSettings.PreReplayCommand", ""));
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
                get => _postReplayCommand
                        ?? (_postReplayCommand = ValuesStorage.Get("Settings.DriveSettings.PostReplayCommand", ""));
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
                get => _immediateStart
                        ?? (_immediateStart = ValuesStorage.Get("Settings.DriveSettings.ImmediateStart", false)).Value;
                set {
                    if (Equals(value, _immediateStart)) return;
                    _immediateStart = value;
                    ValuesStorage.Set("Settings.DriveSettings.ImmediateStart", value);
                    OnPropertyChanged();
                }
            }

            private bool? _immediateCancel;

            public bool ImmediateCancel {
                get => _immediateCancel
                        ?? (_immediateCancel = ValuesStorage.Get("Settings.DriveSettings.ImmediateCancel", false)).Value;
                set {
                    if (Equals(value, _immediateCancel)) return;
                    _immediateCancel = value;
                    ValuesStorage.Set("Settings.DriveSettings.ImmediateCancel", value);
                    OnPropertyChanged();
                }
            }

            private bool? _continueOnEscape;

            public bool ContinueOnEscape {
                get => _continueOnEscape
                        ?? (_continueOnEscape = ValuesStorage.Get("Settings.DriveSettings.ContinueOnEscape", false)).Value;
                set {
                    if (Equals(value, _continueOnEscape)) return;
                    _continueOnEscape = value;
                    ValuesStorage.Set("Settings.DriveSettings.ContinueOnEscape", value);
                    OnPropertyChanged();
                }
            }

            private bool? _skipPracticeResults;

            public bool SkipPracticeResults {
                get => _skipPracticeResults
                        ?? (_skipPracticeResults = ValuesStorage.Get("Settings.DriveSettings.SkipPracticeResults", false)).Value;
                set {
                    if (Equals(value, _skipPracticeResults)) return;
                    _skipPracticeResults = value;
                    ValuesStorage.Set("Settings.DriveSettings.SkipPracticeResults", value);
                    OnPropertyChanged();
                }
            }

            private int? _raceResultsLimit;

            public int RaceResultsLimit {
                get => _raceResultsLimit
                        ?? (_raceResultsLimit = ValuesStorage.Get("Settings.DriveSettings.RaceResultsLimit", 1000)).Value;
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
                get => _tryToLoadReplays
                        ?? (_tryToLoadReplays = ValuesStorage.Get("Settings.DriveSettings.TryToLoadReplays", true)).Value;
                set {
                    if (Equals(value, _tryToLoadReplays)) return;
                    _tryToLoadReplays = value;
                    ValuesStorage.Set("Settings.DriveSettings.TryToLoadReplays", value);
                    OnPropertyChanged();
                }
            }

            private bool? _autoSaveReplays;

            public bool AutoSaveReplays {
                get => _autoSaveReplays
                        ?? (_autoSaveReplays = ValuesStorage.Get("Settings.DriveSettings.AutoSaveReplays", false)).Value;
                set {
                    if (Equals(value, _autoSaveReplays)) return;
                    _autoSaveReplays = value;
                    ValuesStorage.Set("Settings.DriveSettings.AutoSaveReplays", value);
                    OnPropertyChanged();
                }
            }

            private bool? _autoAddReplaysExtension;

            public bool AutoAddReplaysExtension {
                get => _autoAddReplaysExtension
                        ?? (_autoAddReplaysExtension = ValuesStorage.Get("Settings.DriveSettings.AutoAddReplaysExtension", true)).Value;
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
                get => _replaysNameFormat
                        ?? (_replaysNameFormat = ValuesStorage.Get("Settings.DriveSettings.ReplaysNameFormat", DefaultReplaysNameFormat));
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
                get => _use32BitVersion
                        ?? (_use32BitVersion = ValuesStorage.Get("Settings.DriveSettings.Use32BitVersion", false)).Value;
                set {
                    if (Equals(value, _use32BitVersion)) return;
                    _use32BitVersion = value;
                    ValuesStorage.Set("Settings.DriveSettings.Use32BitVersion", value);
                    OnPropertyChanged();
                }
            }

            private bool? _runSteamIfNeeded;

            public bool RunSteamIfNeeded {
                get => _runSteamIfNeeded
                        ?? (_runSteamIfNeeded = ValuesStorage.Get("Settings.DriveSettings.RunSteamIfNeeded", true)).Value;
                set {
                    if (Equals(value, _runSteamIfNeeded)) return;
                    _runSteamIfNeeded = value;
                    ValuesStorage.Set("Settings.DriveSettings.RunSteamIfNeeded", value);
                    OnPropertyChanged();
                }
            }

            private string _playerName;

            public string PlayerName {
                get => _playerName
                        ?? (_playerName = ValuesStorage.Get<string>("Settings.DriveSettings.PlayerName"));
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
                get => _playerNationality
                        ?? (_playerNationality = ValuesStorage.Get<string>("Settings.DriveSettings.PlayerNationality"));
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
                get => _differentPlayerNameOnline
                        ?? (_differentPlayerNameOnline = ValuesStorage.Get("Settings.DriveSettings.DifferentPlayerNameOnline", false)).Value;
                set {
                    if (Equals(value, _differentPlayerNameOnline)) return;
                    _differentPlayerNameOnline = value;
                    ValuesStorage.Set("Settings.DriveSettings.DifferentPlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            private string _playerNameOnline;

            public string PlayerNameOnline {
                get => _playerNameOnline
                        ?? (_playerNameOnline = ValuesStorage.Get("Settings.DriveSettings.PlayerNameOnline", PlayerName));
                set {
                    value = value.Trim();
                    if (Equals(value, _playerNameOnline)) return;
                    _playerNameOnline = value;
                    ValuesStorage.Set("Settings.DriveSettings.PlayerNameOnline", value);
                    OnPropertyChanged();
                }
            }

            private bool? _loadAssistsWithQuickDrivePreset;

            public bool LoadAssistsWithQuickDrivePreset {
                get => _loadAssistsWithQuickDrivePreset
                        ?? (_loadAssistsWithQuickDrivePreset = ValuesStorage.Get("Settings.DriveSettings.LoadAssistsWithQuickDrivePreset", false)).Value;
                set {
                    if (Equals(value, _loadAssistsWithQuickDrivePreset)) return;
                    _loadAssistsWithQuickDrivePreset = value;
                    ValuesStorage.Set("Settings.DriveSettings.LoadAssistsWithQuickDrivePreset", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveTrackDayViaPractice;

            public bool QuickDriveTrackDayViaPractice {
                get => _quickDriveTrackDayViaPractice
                        ?? (_quickDriveTrackDayViaPractice = ValuesStorage.Get("Settings.DriveSettings.QuickDriveTrackDayViaPractice", true)).Value;
                set {
                    if (Equals(value, _quickDriveTrackDayViaPractice)) return;
                    _quickDriveTrackDayViaPractice = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveTrackDayViaPractice", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveAllowCustomData;

            public bool QuickDriveAllowCustomData {
                get => _quickDriveAllowCustomData
                        ?? (_quickDriveAllowCustomData = ValuesStorage.Get("Settings.DriveSettings.QuickDriveAllowCustomData", false)).Value;
                set {
                    if (Equals(value, _quickDriveAllowCustomData)) return;
                    _quickDriveAllowCustomData = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveAllowCustomData", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveAllowExtendedPhysics;

            public bool QuickDriveAllowExtendedPhysics {
                get => _quickDriveAllowExtendedPhysics
                        ?? (_quickDriveAllowExtendedPhysics = ValuesStorage.Get("Settings.DriveSettings.QuickDriveAllowExtendedPhysics", false)).Value;
                set {
                    if (Equals(value, _quickDriveAllowExtendedPhysics)) return;
                    _quickDriveAllowExtendedPhysics = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveAllowExtendedPhysics", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveExpandBounds;

            public bool QuickDriveExpandBounds {
                get => _quickDriveExpandBounds
                        ?? (_quickDriveExpandBounds = ValuesStorage.Get("Settings.DriveSettings.ExpandBounds", false)).Value;
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
                get => _quickDriveAiLimitations
                        ?? (_quickDriveAiLimitations = ValuesStorage.Get("Settings.DriveSettings.QuickDriveAiLimitations", false)).Value;
                set {
                    if (Equals(value, _quickDriveAiLimitations)) return;
                    _quickDriveAiLimitations = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveAiLimitations", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveAiLevelInName;

            public bool QuickDriveAiLevelInName {
                get => _quickDriveAiLevelInName
                        ?? (_quickDriveAiLevelInName = ValuesStorage.Get("RaceGrid.AiLevelInDriverName", false)).Value;
                set {
                    if (Equals(value, _quickDriveAiLevelInName)) return;
                    _quickDriveAiLevelInName = value;
                    ValuesStorage.Set("RaceGrid.AiLevelInDriverName", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveAiAggressionInName;

            public bool QuickDriveAiAggressionInName {
                get => _quickDriveAiAggressionInName
                        ?? (_quickDriveAiAggressionInName = ValuesStorage.Get("RaceGrid.AiAggressionInDriverName", false)).Value;
                set {
                    if (Equals(value, _quickDriveAiAggressionInName)) return;
                    _quickDriveAiAggressionInName = value;
                    ValuesStorage.Set("RaceGrid.AiAggressionInDriverName", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveUseSkinNames;

            public bool QuickDriveUseSkinNames {
                get => _quickDriveUseSkinNames
                        ?? (_quickDriveUseSkinNames = ValuesStorage.Get("Settings.DriveSettings.QuickDriveUseSkinNames", true)).Value;
                set {
                    if (Equals(value, _quickDriveUseSkinNames)) return;
                    _quickDriveUseSkinNames = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveUseSkinNames", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickDriveCheckTrack;

            public bool QuickDriveCheckTrack {
                get => _quickDriveCheckTrack
                        ?? (_quickDriveCheckTrack = ValuesStorage.Get("Settings.DriveSettings.QuickDriveCheckTrack", true)).Value;
                set {
                    if (Equals(value, _quickDriveCheckTrack)) return;
                    _quickDriveCheckTrack = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickDriveCheckTrack", value);
                    OnPropertyChanged();
                }
            }

            private bool? _alwaysRecordGhost;

            public bool AlwaysRecordGhost {
                get => _alwaysRecordGhost
                        ?? (_alwaysRecordGhost = ValuesStorage.Get("Settings.DriveSettings.AlwaysRecordGhost", false)).Value;
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
                get => _kunosCareerUserAiLevel
                        ?? (_kunosCareerUserAiLevel = ValuesStorage.Get("Settings.DriveSettings.KunosCareerUserAiLevel", true)).Value;
                set {
                    if (Equals(value, _kunosCareerUserAiLevel)) return;
                    _kunosCareerUserAiLevel = value;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserAiLevel", value);
                    OnPropertyChanged();
                }
            }

            private bool? _kunosCareerUserSkin;

            public bool KunosCareerUserSkin {
                get => _kunosCareerUserSkin
                        ?? (_kunosCareerUserSkin = ValuesStorage.Get("Settings.DriveSettings.KunosCareerUserSkin", true)).Value;
                set {
                    if (Equals(value, _kunosCareerUserSkin)) return;
                    _kunosCareerUserSkin = value;
                    ValuesStorage.Set("Settings.DriveSettings.KunosCareerUserSkin", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickSwitches;

            public bool QuickSwitches {
                get => _quickSwitches
                        ?? (_quickSwitches = ValuesStorage.Get("Settings.DriveSettings.QuickSwitches", true)).Value;
                set {
                    if (Equals(value, _quickSwitches)) return;
                    _quickSwitches = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickSwitches", value);
                    OnPropertyChanged();
                }
            }

            private bool? _quickSwitchesRightMouseButton;

            public bool QuickSwitchesRightMouseButton {
                get
                        =>
                                _quickSwitchesRightMouseButton
                                        ?? (_quickSwitchesRightMouseButton = ValuesStorage.Get("Settings.DriveSettings.QuickSwitchesRightMouseButton", true))
                                                .Value;
                set {
                    if (Equals(value, _quickSwitchesRightMouseButton)) return;
                    _quickSwitchesRightMouseButton = value;
                    ValuesStorage.Set("Settings.DriveSettings.QuickSwitchesRightMouseButton", value);
                    OnPropertyChanged();
                }
            }

            private string[] _quickSwitchesList;

            public string[] QuickSwitchesList {
                get => _quickSwitchesList
                        ?? (_quickSwitchesList = ValuesStorage.GetStringList("Settings.DriveSettings.QuickSwitchesList", new[] {
                            @"WidgetExposure",
                            @"WidgetUiPresets",
                            @"WidgetHideDriveArms",
                            @"WidgetHideSteeringWheel"
                        }).ToArray());
                set {
                    if (Equals(value, _quickSwitchesList)) return;
                    ValuesStorage.Storage.SetStringList("Settings.DriveSettings.QuickSwitchesList", value);
                    _quickSwitchesList = value;
                    OnPropertyChanged();
                }
            }

            /*private bool? _automaticallyConvertBmpToJpg;

            public bool AutomaticallyConvertBmpToJpg {
                get => _automaticallyConvertBmpToJpg
                        ?? (_automaticallyConvertBmpToJpg = ValuesStorage.Get("Settings.DriveSettings.AutomaticallyConvertBmpToJpg", false)).Value;
                set {
                    if (Equals(value, _automaticallyConvertBmpToJpg)) return;
                    _automaticallyConvertBmpToJpg = value;
                    ValuesStorage.Set("Settings.DriveSettings.AutomaticallyConvertBmpToJpg", value);
                    OnPropertyChanged();
                }
            }*/

            private string _localAddress;

            [CanBeNull]
            public string LocalAddress {
                get => _localAddress
                        ?? (_localAddress = ValuesStorage.Get<string>("Settings.DriveSettings.LocalAddress"));
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
                get => _weatherSpecificClouds
                        ?? (_weatherSpecificClouds = ValuesStorage.Get("Settings.DriveSettings.WeatherSpecificClouds", true)).Value;
                set {
                    if (Equals(value, _weatherSpecificClouds)) return;
                    _weatherSpecificClouds = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificClouds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _weatherSpecificPpFilter;

            public bool WeatherSpecificPpFilter {
                get => _weatherSpecificPpFilter
                        ?? (_weatherSpecificPpFilter = ValuesStorage.Get("Settings.DriveSettings.WeatherSpecificPpFilter", true)).Value;
                set {
                    if (Equals(value, _weatherSpecificPpFilter)) return;
                    _weatherSpecificPpFilter = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificPpFilter", value);
                    OnPropertyChanged();
                }
            }

            private bool? _weatherSpecificTyreSmoke;

            public bool WeatherSpecificTyreSmoke {
                get => _weatherSpecificTyreSmoke
                        ?? (_weatherSpecificTyreSmoke = ValuesStorage.Get("Settings.DriveSettings.WeatherSpecificTyreSmoke", true)).Value;
                set {
                    if (Equals(value, _weatherSpecificTyreSmoke)) return;
                    _weatherSpecificTyreSmoke = value;
                    ValuesStorage.Set("Settings.DriveSettings.WeatherSpecificTyreSmoke", value);
                    OnPropertyChanged();
                }
            }

            private bool? _rhmIntegration;

            public bool RhmIntegration {
                get => _rhmIntegration
                        ?? (_rhmIntegration = ValuesStorage.Get("Settings.DriveSettings.RhmIntegration", false)).Value;
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
                get => _rhmLocation
                        ?? (_rhmLocation = ValuesStorage.Get<string>("Settings.DriveSettings.RhmLocation"));
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
                get => _rhmSettingsLocation
                        ?? (_rhmSettingsLocation = ValuesStorage.Get("Settings.DriveSettings.RhmSettingsLocation",
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

            public DelayEntry[] RhmKeepAlivePeriods => _rhmKeepAlivePeriods
                    ?? (_rhmKeepAlivePeriods = new[] {
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
                    var saved = ValuesStorage.Get<TimeSpan?>("Settings.DriveSettings.RhmKeepAlivePeriod");
                    return _rhmKeepAlivePeriod
                            ?? (_rhmKeepAlivePeriod = RhmKeepAlivePeriods.FirstOrDefault(x => x.TimeSpan == saved) ??
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
                RhmLocation = FileRelatedDialogs.Open(new OpenDialogParams {
                    DirectorySaveKey = "rhm",
                    Filters = {
                        new DialogFilterPiece("Real Head Motion", "RealHeadMotionAssettoCorsa.exe"),
                        DialogFilterPiece.Applications,
                        DialogFilterPiece.AllFiles,
                    },
                    Title = "Select Real Head Motion application",
                    InitialDirectory = FileUtils.GetDirectoryNameSafe(RhmLocation) ?? "",
                    DefaultFileName = FileUtils.GetFileNameSafe(RhmLocation),
                }) ?? RhmLocation;
            }));

            private DelegateCommand _selectRhmSettingsCommand;

            public DelegateCommand SelectRhmSettingsCommand => _selectRhmSettingsCommand ?? (_selectRhmSettingsCommand = new DelegateCommand(() => {
                RhmSettingsLocation = FileRelatedDialogs.Open(new OpenDialogParams {
                    DirectorySaveKey = "rhmsettings",
                    Filters = {
                        new DialogFilterPiece("Real Head Motion Settings", "Settings.xml"),
                        DialogFilterPiece.XmlFiles,
                        DialogFilterPiece.AllFiles
                    },
                    Title = "Select Real Head Motion settings",
                    InitialDirectory = FileUtils.GetDirectoryNameSafe(RhmSettingsLocation) ?? "",
                    DefaultFileName = FileUtils.GetFileNameSafe(RhmSettingsLocation),
                }) ?? RhmSettingsLocation;
            }));

            public BeepingNoiseType[] BeepingNoises => EnumExtension.GetValues<BeepingNoiseType>();

            private BeepingNoiseType? _crashBeepingNoise;

            public BeepingNoiseType CrashBeepingNoise {
                get => _crashBeepingNoise ?? (_crashBeepingNoise = ValuesStorage.Get("Settings.DriveSettings.CrashBeepingNoise", BeepingNoiseType.System)).Value
                        ;
                set {
                    if (Equals(value, _crashBeepingNoise)) return;
                    _crashBeepingNoise = value;
                    ValuesStorage.Set("Settings.DriveSettings.CrashBeepingNoise", value);
                    OnPropertyChanged();
                }
            }

            private bool? _checkAndFixControlsOrder;

            public bool CheckAndFixControlsOrder {
                get => _checkAndFixControlsOrder
                        ?? (_checkAndFixControlsOrder = ValuesStorage.Get("Settings.DriveSettings.CheckAndFixControlsOrder", false)).Value;
                set {
                    if (Equals(value, _checkAndFixControlsOrder)) return;
                    _checkAndFixControlsOrder = value;
                    ValuesStorage.Set("Settings.DriveSettings.CheckAndFixControlsOrder", value);
                    OnPropertyChanged();
                }
            }

            private bool? _showExtraComboBoxes;

            public bool ShowExtraComboBoxes {
                get => _showExtraComboBoxes
                        ?? (_showExtraComboBoxes = ValuesStorage.Get("Settings.DriveSettings.ShowExtraComboBoxes", true)).Value;
                set {
                    if (Equals(value, _showExtraComboBoxes)) return;
                    _showExtraComboBoxes = value;
                    ValuesStorage.Set("Settings.DriveSettings.ShowExtraComboBoxes", value);
                    OnPropertyChanged();
                }
            }

            private bool? _scanControllersAutomatically;

            public bool ScanControllersAutomatically {
                get => _scanControllersAutomatically
                        ?? (_scanControllersAutomatically = ValuesStorage.Get("Settings.DriveSettings.ScanControllersAutomatically", false)).Value;
                set {
                    if (Equals(value, _scanControllersAutomatically)) return;
                    _scanControllersAutomatically = value;
                    ValuesStorage.Set("Settings.DriveSettings.ScanControllersAutomatically", value);
                    OnPropertyChanged();
                }
            }

            private bool? _sameControllersKeepFirst;

            public bool SameControllersKeepFirst {
                get => _sameControllersKeepFirst
                        ?? (_sameControllersKeepFirst = ValuesStorage.Get("Settings.DriveSettings.SameControllersKeepFirst", false)).Value;
                set {
                    if (Equals(value, _sameControllersKeepFirst)) return;
                    _sameControllersKeepFirst = value;
                    ValuesStorage.Set("Settings.DriveSettings.SameControllersKeepFirst", value);
                    OnPropertyChanged();
                }
            }

            private bool? _patchAcToDisableShadows;

            public bool PatchAcToDisableShadows {
                get => _patchAcToDisableShadows
                        ?? (_patchAcToDisableShadows = ValuesStorage.Get("Settings.DriveSettings.PatchAcToDisableShadows", false)).Value;
                set {
                    if (Equals(value, _patchAcToDisableShadows)) return;
                    _patchAcToDisableShadows = value;
                    ValuesStorage.Set("Settings.DriveSettings.PatchAcToDisableShadows", value);
                    OnPropertyChanged();
                }
            }

            private bool? _allowDecimalTrackState;

            public bool AllowDecimalTrackState {
                get => _allowDecimalTrackState
                        ?? (_allowDecimalTrackState = ValuesStorage.Get("Settings.DriveSettings.AllowDecimalTrackState", false)).Value;
                set {
                    if (Equals(value, _allowDecimalTrackState)) return;
                    _allowDecimalTrackState = value;
                    ValuesStorage.Set("Settings.DriveSettings.AllowDecimalTrackState", value);
                    OnPropertyChanged();
                }
            }

            private bool? _monitorFramesPerSecond;

            public bool MonitorFramesPerSecond {
                get => _monitorFramesPerSecond
                        ?? (_monitorFramesPerSecond = ValuesStorage.Get("Settings.DriveSettings.MonitorFramesPerSecond", true)).Value;
                set {
                    if (Equals(value, _monitorFramesPerSecond)) return;
                    _monitorFramesPerSecond = value;
                    ValuesStorage.Set("Settings.DriveSettings.MonitorFramesPerSecond", value);
                    OnPropertyChanged();
                }
            }

            // Demoted from UI option to an app flag, kept here to avoid rewriting any code
            public bool WatchForSharedMemory { get; set; }
        }

        private static DriveSettings _drive;
        public static DriveSettings Drive => _drive ?? (_drive = new DriveSettings());
    }
}