using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using SharpCompress.Common;
using SharpCompress.Readers;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Tools {
    public static partial class ArgumentsHandler {
        public static bool OptionUserChampionshipExtMode = true;

        private static async Task<ArgumentHandleResult> ProcessSharedEntry(SharedEntry shared, bool justGo) {
            var data = shared?.Data;
            if (data == null) return ArgumentHandleResult.Failed;

            switch (shared.EntryType) {
                case SharedEntryType.PpFilter:
                    return ProcessSharedPpFilter(shared, data, justGo);

                case SharedEntryType.CarSetup:
                    return ProcessSharedCarSetup(shared, data, justGo);

                case SharedEntryType.ControlsPreset:
                    return ProcessSharedControlsPreset(shared, data, justGo);

                case SharedEntryType.ForceFeedbackPreset:
                    return ProcessSharedForceFeedbackPreset(shared, data, justGo);

                case SharedEntryType.VideoSettingsPreset:
                    return ProcessSharedSettingsPreset(AcSettingsHolder.VideoPresets, shared, data, justGo);

                case SharedEntryType.AudioSettingsPreset:
                    return ProcessSharedSettingsPreset(AcSettingsHolder.AudioPresets, shared, data, justGo);

                case SharedEntryType.InGameAppsPreset:
                    return ProcessSharedSettingsPreset(AcSettingsHolder.AppsPresets, shared, data, justGo);

                case SharedEntryType.AssistsSetupPreset:
                    return ProcessSharedAssistsSetupPreset(shared, data, justGo);

                case SharedEntryType.TrackStatePreset:
                    return ProcessSharedTrackStatePreset(shared, data, justGo);

                case SharedEntryType.QuickDrivePreset:
                    return await ProcessSharedQuickDrivePreset(shared, data, justGo);

                case SharedEntryType.RaceGridPreset:
                    return ProcessSharedRaceGridPreset(shared, data, justGo);

                case SharedEntryType.RhmPreset:
                    return ProcessSharedRhmPreset(shared, data, justGo);

                case SharedEntryType.UserChampionship:
                    return OptionUserChampionshipExtMode ? ProcessSharedUserChampionshipExt(shared, data, justGo) :
                            ProcessSharedUserChampionship(shared, data, justGo);

                case SharedEntryType.Weather:
                    return ProcessSharedWeather(shared, data, justGo);

                case SharedEntryType.CustomShowroomPreset:
                    return ProcessSharedCustomShowroomPreset(shared, data, justGo);

                case SharedEntryType.CustomPreviewsPreset:
                    return ProcessSharedCustomPreviewsPreset(shared, data, justGo);

                case SharedEntryType.CspSettings:
                    return ProcessSharedCspSettings(shared, data, justGo);

                case SharedEntryType.CarLodsGenerationPreset:
                    return ProcessSharedCarLodsGenerationPreset(shared, data, justGo);

                case SharedEntryType.BakedShadowsPreset:
                    return ProcessSharedBakedShadowsPreset(shared, data, justGo);

                case SharedEntryType.Replay:
                    throw new NotSupportedException();

                case SharedEntryType.Results:
                    ModernDialog.ShowMessage(Encoding.UTF8.GetString(data));
                    return ArgumentHandleResult.Successful;

                default:
                    throw new Exception(string.Format(AppStrings.Arguments_SharedUnsupported, shared.EntryType));
            }
        }

        private static async Task<ArgumentHandleResult> ProcessSharedById(string id, bool justGo) {
            SharedEntry shared;
            using (var waiting = new WaitingDialog()) {
                waiting.Report(ControlsStrings.Common_Loading);
                shared = await SharingHelper.GetSharedAsync(id, waiting.CancellationToken);
            }
            return await ProcessSharedEntry(shared, justGo);
        }

        private static SharedEntryType GuessEntryType(string data) {
            data = data.TrimStart();
            if (data.StartsWith(@"{")) {
                if (data.Contains(@"""StabilityControl"":")) return SharedEntryType.AssistsSetupPreset;
                if (data.Contains(@"""AudioData"":")) return SharedEntryType.AudioSettingsPreset;
                if (data.Contains(@"""SoftwareDownsize"":")) return SharedEntryType.CustomPreviewsPreset;
                if (data.Contains(@"""CubemapReflectionMapSize"":")) return SharedEntryType.CustomShowroomPreset;
                if (data.Contains(@"""ModeData"":")) return SharedEntryType.QuickDrivePreset;
                if (data.Contains(@"""AiLevelArrangeReverse"":")) return SharedEntryType.RaceGridPreset;
                if (data.Contains(@"""VideoData"":")) return SharedEntryType.VideoSettingsPreset;
                if (data.Contains(@"""s"":") && data.Contains(@"""t"":") && data.Contains(@"""r"":")) return SharedEntryType.TrackStatePreset;

                if (data.Contains("\"AmbientShadowDiffusion\":")) return SharedEntryType.AmbientShadowsPreset;
                if (data.Contains("\"ShadowBiasCullBack\":")) return SharedEntryType.BakedShadowsPreset;
                if (data.Contains("\"PythonData\":")) return SharedEntryType.InGameAppsPreset;
                if (data.Contains("\"PackIntoSingle\":")) return SharedEntryType.PackServerPreset;
                if (data.Contains("\"DisableSweetFx\":")) return SharedEntryType.AcPreviewsPreset;
                if (data.Contains("\"DisableWatermark\":")) return SharedEntryType.AcShowroomPreset;
                if (data.Contains("\"TyresShortName\":")) return SharedEntryType.TyresGenerationExamplesPreset;
                if (data.Contains("\"SeparateNetworks\":")) return SharedEntryType.TyresGenerationParamsPreset;
                if (data.Contains("\"UserDefinedValues\":")) return SharedEntryType.CarLodsGenerationPreset;
            } else if (data.StartsWith("<RealHeadMotion>")) {
                return SharedEntryType.RhmPreset;
            } else if (data.StartsWith(@"[")) {
                if (data.Contains("STEERING_OPPOSITE_DIRECTION_SPEED=") && data.Contains("COMBINE_WITH_KEYBOARD_CONTROL=")) {
                    return SharedEntryType.ControlsPreset;
                }
                if (Regex.IsMatch(data, @"\[[A-Z_]+:[A-Z_]+\]")) {
                    return SharedEntryType.CspSettings;
                }
            }
            Logging.Warning(data);
            throw new Exception("Failed to determine CM preset type");
        }

        private static async Task<ArgumentHandleResult> ProcessSharedFile(string filename) {
            try {
                var data = await FileUtils.ReadAllTextAsync(filename);
                return await ProcessSharedEntry(new SharedEntry {
                    EntryType = GuessEntryType(data),
                    Name = Path.GetFileNameWithoutExtension(filename),
                    Data = Encoding.UTF8.GetBytes(data),
                    LocalSource = filename
                }, false);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t load CM preset", e);
                return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedWeather(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, applyable: false);
            switch (result) {
                case Choise.Save:
                    if (WeatherManager.Instance.Directories == null) return ArgumentHandleResult.Failed;
                    var directory = WeatherManager.Instance.Directories.GetLocation(WeatherManager.Instance.Directories.GetUniqueId(shared.GetFileName()), true);
                    Directory.CreateDirectory(directory);

                    var written = 0;
                    using (var stream = new MemoryStream(data)) {
                        var reader = ReaderFactory.Open(stream);

                        try {
                            while (reader.MoveToNextEntry()) {
                                if (!reader.Entry.IsDirectory) {
                                    reader.WriteEntryToDirectory(directory, new ExtractionOptions {
                                        ExtractFullPath = true,
                                        Overwrite = true
                                    });
                                    written++;
                                }
                            }
                        } catch (EndOfStreamException) {
                            if (written < 2) {
                                throw;
                            }
                        }
                    }

                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedCustomShowroomPreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, applyable: false);
            switch (result) {
                case Choise.Save:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(DarkRendererSettingsValues.DefaultPresetableKeyValue), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedCustomPreviewsPreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, applyable: false);
            switch (result) {
                case Choise.Save:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(CmPreviewsSettingsValues.DefaultPresetableKeyValue), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedRaceGridPreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo);
            switch (result) {
                case Choise.Save:
                case Choise.ApplyAndSave:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(RaceGridViewModel.PresetableKeyValue), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    if (result == Choise.ApplyAndSave) {
                        RaceGridViewModel.LoadPreset(filename);
                        QuickDrive.NavigateToPage();
                    }
                    return ArgumentHandleResult.SuccessfulShow;
                case Choise.Apply:
                    RaceGridViewModel.LoadSerializedPreset(data.ToUtf8String());
                    QuickDrive.NavigateToPage();
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedRhmPreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo);
            switch (result) {
                case Choise.Save:
                case Choise.ApplyAndSave:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(RhmService.Instance.PresetableCategory), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    if (result == Choise.ApplyAndSave) {
                        RhmService.Instance.ImportFromPresetData(data.ToUtf8String());
                        UserPresetsControl.SetCurrentFilename(RhmService.Instance.PresetableKey, filename);
                    }
                    return ArgumentHandleResult.SuccessfulShow;
                case Choise.Apply:
                    RhmService.Instance.ImportFromPresetData(data.ToUtf8String());
                    UserPresetsControl.SetCurrentFilename(RhmService.Instance.PresetableKey, null);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedCspSettings(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo);
            using (var model = PatchSettingsModel.Create()) {
                switch (result) {
                    case Choise.Save:
                    case Choise.ApplyAndSave:
                        var filename = FileUtils.EnsureUnique(Path.Combine(
                                PresetsManager.Instance.GetDirectory(model.PresetableCategory), @"Loaded", shared.GetFileName()));
                        Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                        File.WriteAllBytes(filename, data);
                        if (result == Choise.ApplyAndSave) {
                            model.ImportFromPresetData(data.ToUtf8String());
                            UserPresetsControl.SetCurrentFilename(model.PresetableKey, filename);
                        }
                        return ArgumentHandleResult.SuccessfulShow;
                    case Choise.Apply:
                        model.ImportFromPresetData(data.ToUtf8String());
                        UserPresetsControl.SetCurrentFilename(model.PresetableKey, null);
                        return ArgumentHandleResult.SuccessfulShow;
                    default:
                        return ArgumentHandleResult.Failed;
                }
            }
        }

        private static ArgumentHandleResult ProcessSharedCarLodsGenerationPreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, applyable: false);
            switch (result) {
                case Choise.Save:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(CarGenerateLodsDialog.PresetableKey), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedBakedShadowsPreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, applyable: false);
            switch (result) {
                case Choise.Save:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(BakedShadowsRendererViewModel.PresetableKey), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static async Task<ArgumentHandleResult> ProcessSharedQuickDrivePreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, AppStrings.Arguments_Shared_JustGo);
            switch (result) {
                case Choise.Save:
                case Choise.ApplyAndSave:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(QuickDrive.PresetableKeyValue), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    if (result == Choise.ApplyAndSave) {
                        QuickDrive.LoadPreset(filename);
                    }
                    return ArgumentHandleResult.SuccessfulShow;
                case Choise.Apply:
                    QuickDrive.LoadSerializedPreset(data.ToUtf8String());
                    return ArgumentHandleResult.SuccessfulShow;
                case Choise.Extra: // just go
                    if (!await QuickDrive.RunAsync(serializedPreset: data.ToUtf8String())) {
                        throw new InformativeException(AppStrings.Common_CannotStartRace, AppStrings.Arguments_CannotStartRace_Commentary);
                    }

                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedAssistsSetupPreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo);
            switch (result) {
                case Choise.Save:
                case Choise.ApplyAndSave:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(AssistsViewModel.Instance.PresetableKey), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    if (result == Choise.ApplyAndSave) {
                        UserPresetsControl.LoadPreset(AssistsViewModel.Instance.PresetableKey, filename);
                    }
                    return ArgumentHandleResult.SuccessfulShow;
                case Choise.Apply:
                    AssistsViewModel.Instance.ImportFromPresetData(data.ToUtf8String());
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedTrackStatePreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, applyable: false);
            switch (result) {
                case Choise.Save:
                case Choise.ApplyAndSave:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(TrackStateViewModelBase.PresetableCategory), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    if (result == Choise.ApplyAndSave) {
                        UserPresetsControl.LoadPreset(TrackStateViewModel.Instance.PresetableKey, filename);
                    }
                    return ArgumentHandleResult.SuccessfulShow;
                case Choise.Apply:
                    TrackStateViewModel.Instance.ImportFromPresetData(data.ToUtf8String());
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedSettingsPreset(IUserPresetable presetable, SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo);
            switch (result) {
                case Choise.Save:
                case Choise.ApplyAndSave:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            PresetsManager.Instance.GetDirectory(presetable.PresetableKey), @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    if (result == Choise.ApplyAndSave) {
                        UserPresetsControl.LoadPreset(presetable.PresetableKey, filename);
                    }
                    return ArgumentHandleResult.SuccessfulShow;
                case Choise.Apply:
                    presetable.ImportFromPresetData(data.ToUtf8String());
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedForceFeedbackPreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, saveable: false);
            switch (result) {
                case Choise.Apply:
                    var ini = IniFile.Parse(data.ToUtf8String());
                    AcSettingsHolder.Controls.LoadFfbFromIni(ini);
                    AcSettingsHolder.System.LoadFfbFromIni(ini);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedControlsPreset(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, AppStrings.Arguments_Shared_ApplyFfbOnly);
            switch (result) {
                case Choise.Save:
                case Choise.ApplyAndSave:
                    var filename = FileUtils.EnsureUnique(Path.Combine(
                            ControlsSettings.UserPresetsDirectory, @"Loaded", shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    if (result == Choise.ApplyAndSave) {
                        AcSettingsHolder.Controls.LoadPreset(filename, true);
                    }
                    return ArgumentHandleResult.SuccessfulShow;
                case Choise.Apply:
                    if (File.Exists(AcSettingsHolder.Controls.Filename)) {
                        FileUtils.Recycle(AcSettingsHolder.Controls.Filename);
                    }
                    File.WriteAllBytes(AcSettingsHolder.Controls.Filename, data);
                    return ArgumentHandleResult.SuccessfulShow;
                case Choise.Extra: // ffb only
                    var ini = IniFile.Parse(data.ToUtf8String());
                    AcSettingsHolder.Controls.LoadFfbFromIni(ini);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedCarSetup(SharedEntry shared, byte[] data, bool justGo) {
            var content = data.ToUtf8String();
            var metadata = SharingHelper.GetMetadata(SharedEntryType.CarSetup, content, out content);

            var carId = metadata.GetValueOrDefault("car");
            var trackId = metadata.GetValueOrDefault("track") ?? CarSetupObject.GenericDirectory;
            if (carId == null) {
                throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, AppStrings.Arguments_MetadataIsMissing);
            }

            var result = ShowDialog(shared, justGo, applyable: false,
                    additionalButton: trackId == CarSetupObject.GenericDirectory ? null : AppStrings.Arguments_SaveAsGeneric);
            switch (result) {
                case Choise.Save:
                case Choise.Extra:
                    var filename = FileUtils.EnsureUnique(Path.Combine(AcPaths.GetCarSetupsDirectory(carId),
                            result == Choise.Save ? trackId : CarSetupObject.GenericDirectory, shared.GetFileName()));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllText(filename, content);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static ArgumentHandleResult ProcessSharedPpFilter(SharedEntry shared, byte[] data, bool justGo) {
            var result = ShowDialog(shared, justGo, appliableWithoutSaving: false);
            switch (result) {
                case Choise.Save:
                case Choise.ApplyAndSave:
                    if (PpFiltersManager.Instance.Directories == null) return ArgumentHandleResult.Failed;
                    var filename = PpFiltersManager.Instance.Directories.GetLocation(PpFiltersManager.Instance.Directories.GetUniqueId(shared.GetFileName()),
                            true);
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllBytes(filename, data);
                    if (result == Choise.ApplyAndSave) {
                        AcSettingsHolder.Video.PostProcessingFilter = Path.GetFileNameWithoutExtension(filename);
                    }
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }
    }
}