using System;
using System.IO;
using System.Threading.Tasks;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.CustomShowroom;
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
using SharpCompress.Readers;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Tools {
    public static partial class ArgumentsHandler {
        public static bool OptionUserChampionshipExtMode = true;

        private static async Task<ArgumentHandleResult> ProcessShared(string id) {
            SharedEntry shared;

            using (var waiting = new WaitingDialog()) {
                waiting.Report(ControlsStrings.Common_Loading);
                shared = await SharingHelper.GetSharedAsync(id, waiting.CancellationToken);
            }

            var data = shared?.Data;
            if (data == null) return ArgumentHandleResult.Failed;

            switch (shared.EntryType) {
                case SharedEntryType.PpFilter:
                    return ProcessSharedPpFilter(shared, data);

                case SharedEntryType.CarSetup:
                    return ProcessSharedCarSetup(shared, data);

                case SharedEntryType.ControlsPreset:
                    return ProcessSharedControlsPreset(shared, data);

                case SharedEntryType.ForceFeedbackPreset:
                    return ProcessSharedForceFeedbackPreset(shared, data);

                case SharedEntryType.VideoSettingsPreset:
                    return ProcessSharedSettingsPreset(AcSettingsHolder.VideoPresets, shared, data);

                case SharedEntryType.AudioSettingsPreset:
                    return ProcessSharedSettingsPreset(AcSettingsHolder.AudioPresets, shared, data);

                case SharedEntryType.AssistsSetupPreset:
                    return ProcessSharedAssistsSetupPreset(shared, data);

                case SharedEntryType.TrackStatePreset:
                    return ProcessSharedTrackStatePreset(shared, data);

                case SharedEntryType.QuickDrivePreset:
                    return await ProcessSharedQuickDrivePreset(shared, data);

                case SharedEntryType.RaceGridPreset:
                    return ProcessSharedRaceGridPreset(shared, data);

                case SharedEntryType.RhmPreset:
                    return ProcessSharedRhmPreset(shared, data);

                case SharedEntryType.UserChampionship:
                    return OptionUserChampionshipExtMode ? ProcessSharedUserChampionshipExt(shared, data) :
                            ProcessSharedUserChampionship(shared, data);

                case SharedEntryType.Weather:
                    return ProcessSharedWeather(shared, data);

                case SharedEntryType.CustomShowroomPreset:
                    return ProcessSharedCustomShowroomPreset(shared, data);

                case SharedEntryType.CustomPreviewsPreset:
                    return ProcessSharedCustomPreviewsPreset(shared, data);

                default:
                    throw new Exception(string.Format(AppStrings.Arguments_SharedUnsupported, shared.EntryType));
            }
        }

        private static ArgumentHandleResult ProcessSharedWeather(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared, applyable: false);
            switch (result) {
                case Choise.Save:
                    var directory = WeatherManager.Instance.Directories.GetUniqueId(shared.GetFileName());
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

        private static ArgumentHandleResult ProcessSharedCustomShowroomPreset(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared, applyable: false);
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

        private static ArgumentHandleResult ProcessSharedCustomPreviewsPreset(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared, applyable: false);
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

        private static ArgumentHandleResult ProcessSharedRaceGridPreset(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared);
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

        private static ArgumentHandleResult ProcessSharedRhmPreset(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared);
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

        private static async Task<ArgumentHandleResult> ProcessSharedQuickDrivePreset(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared, AppStrings.Arguments_Shared_JustGo);
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

        private static ArgumentHandleResult ProcessSharedAssistsSetupPreset(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared);
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

        private static ArgumentHandleResult ProcessSharedTrackStatePreset(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared, applyable: false);
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

        private static ArgumentHandleResult ProcessSharedSettingsPreset(IUserPresetable presetable, SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared);
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

        private static ArgumentHandleResult ProcessSharedForceFeedbackPreset(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared, saveable: false);
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

        private static ArgumentHandleResult ProcessSharedControlsPreset(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared, AppStrings.Arguments_Shared_ApplyFfbOnly);
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

        private static ArgumentHandleResult ProcessSharedCarSetup(SharedEntry shared, byte[] data) {
            var content = data.ToUtf8String();
            var metadata = SharingHelper.GetMetadata(SharedEntryType.CarSetup, content, out content);

            var carId = metadata.GetValueOrDefault("car");
            var trackId = metadata.GetValueOrDefault("track") ?? CarSetupObject.GenericDirectory;
            if (carId == null) {
                throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, AppStrings.Arguments_MetadataIsMissing);
            }

            var result = ShowDialog(shared, applyable: false,
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

        private static ArgumentHandleResult ProcessSharedPpFilter(SharedEntry shared, byte[] data) {
            var result = ShowDialog(shared, appliableWithoutSaving: false);
            switch (result) {
                case Choise.Save:
                case Choise.ApplyAndSave:
                    var filename = PpFiltersManager.Instance.Directories.GetUniqueId(shared.GetFileName());
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