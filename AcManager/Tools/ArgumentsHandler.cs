using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Dialogs;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using ZipArchive = SharpCompress.Archive.Zip.ZipArchive;

namespace AcManager.Tools {
    public enum ArgumentHandleResult {
        Successful,
        SuccessfulShow,
        Failed,
        FailedShow
    }

    public class ArgumentsHandler {
        public async Task<ArgumentHandleResult> ProcessArgument(string argument) {
            if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;

            if (argument.StartsWith(CustomUriSchemeHelper.UriScheme)) {
                return await ProcessUriRequest(argument);
            }

            if (argument.StartsWith("http", StringComparison.OrdinalIgnoreCase) || argument.StartsWith("https", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("ftp", StringComparison.OrdinalIgnoreCase)) {
                argument = await LoadRemoveFile(argument);
                if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;
            }

            try {
                if (!FileUtils.Exists(argument)) return ArgumentHandleResult.FailedShow;
            } catch (Exception) {
                return ArgumentHandleResult.FailedShow;
            }

            return await ProcessInputFile(argument);
        }

        private async Task<string> LoadRemoveFile(string argument, string name = null, string extension = null) {
            using (var waiting = new WaitingDialog("Loading…")) {
                return await FlexibleLoader.LoadAsync(argument, name, extension, waiting, waiting.CancellationToken);
            }
        }

        private async Task<ArgumentHandleResult> ProcessInputFile(string filename) {
            var isDirectory = FileUtils.IsDirectory(filename);
            if (!isDirectory && filename.EndsWith(".acreplay", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetDirectoryName(filename)?.Equals(FileUtils.GetReplaysDirectory(), StringComparison.OrdinalIgnoreCase) == true) {
                await GameWrapper.StartReplayAsync(new Game.StartProperties(new Game.ReplayProperties {
                    Filename = filename
                }));
                return ArgumentHandleResult.Successful;
            }

            if (!isDirectory && filename.EndsWith(".kn5", StringComparison.OrdinalIgnoreCase)) {
                await CustomShowroomWrapper.StartAsync(filename);
                return ArgumentHandleResult.Successful;
            }

            try {
                new InstallAdditionalContentDialog(filename).ShowDialog();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t install additional content", e);
                return ArgumentHandleResult.Failed;
            }

            return ArgumentHandleResult.Successful;
        }

        private async Task<ArgumentHandleResult> ProcessUriRequestObsolete(string request) {
            string key, param;
            NameValueCollection query;

            {
                var splitted = request.Split(new[] { '/' }, 2);
                if (splitted.Length != 2) return ArgumentHandleResult.FailedShow;

                key = splitted[0];
                param = splitted[1];

                var index = param.IndexOf('?');
                if (index != -1) {
                    query = HttpUtility.ParseQueryString(param.SubstringExt(index + 1));
                    param = param.Substring(0, index);
                } else {
                    query = null;
                }
            }

            switch (key) {
                case "quickdrive":
                    var preset = Convert.FromBase64String(param).ToUtf8String();
                    if (!QuickDrive.RunSerializedPreset(preset)) {
                        NonfatalError.Notify("Can’t start race", "Make sure required car & track are installed and available.");
                        return ArgumentHandleResult.Failed;
                    }
                    break;

                case "race":
                    var raceIni = Convert.FromBase64String(param).ToUtf8String();
                    await GameWrapper.StartAsync(new Game.StartProperties {
                        PreparedConfig = IniFile.Parse(raceIni)
                    });
                    break;

                case "open":
                case "install":
                    var address = Convert.FromBase64String(param).ToUtf8String();
                    var path = await LoadRemoveFile(address, query?.Get("name"));
                    if (string.IsNullOrWhiteSpace(path)) return ArgumentHandleResult.FailedShow;

                    try {
                        if (!FileUtils.Exists(path)) return ArgumentHandleResult.FailedShow;
                    } catch (Exception) {
                        return ArgumentHandleResult.FailedShow;
                    }

                    return await ProcessInputFile(path);
            }

            return ArgumentHandleResult.Successful;
        }

        internal class ParsedUriRequest {
            public string Path { get; private set; }

            public NameValueCollection Params { get; private set; }

            public string Hash { get; private set; }

            public static ParsedUriRequest Parse(string s) {
                var m = Regex.Match(s, @"^/((?:/[\w\.-]+)+)/?([?&][^#]*)?(?:#(.*))?");
                if (!m.Success) throw new Exception("Invalid format");

                return new ParsedUriRequest {
                    Path = m.Groups[1].Value.Substring(1),
                    Params = HttpUtility.ParseQueryString(m.Groups[2].Value),
                    Hash = m.Groups[3].Value
                };
            }
        }

        private async Task<ArgumentHandleResult> ProcessUriRequest(string uri) {
            if (!uri.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.OrdinalIgnoreCase)) return ArgumentHandleResult.FailedShow;

            var request = uri.SubstringExt(CustomUriSchemeHelper.UriScheme.Length);
            Logging.Write("[MAINWINDOW] URI Request: " + request);

            if (!request.StartsWith("//", StringComparison.Ordinal)) {
                return await ProcessUriRequestObsolete(request);
            }

            ParsedUriRequest parsed;
            try {
                parsed = ParsedUriRequest.Parse(request);
            } catch (Exception) {
                NonfatalError.Notify("Can’t parse request", "Make sure format is valid");
                return ArgumentHandleResult.Failed;
            }

            try {
                switch (parsed.Path) {
                    case "replay":
                        return await ProcessReplay(parsed.Params.Get("url"), parsed.Params.Get("uncompressed") == null);

                    case "rsr":
                        return await ProcessRsrEvent(parsed.Params.Get("id"));

                    case "rsr/setup":
                        return await ProcessRsrSetup(parsed.Params.Get("id"));

                    case "shared":
                        return await ProcessShared(parsed.Params.Get("id"));

                    default:
                        NonfatalError.Notify($"Not supported request: “{parsed.Path}”", "Make sure format is valid");
                        return ArgumentHandleResult.Failed;
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t process request", "Make sure data is valid.", e);
                return ArgumentHandleResult.Failed;
            }
        }

        public async Task<ArgumentHandleResult> ProcessReplay(string url, bool compressed) {
            var path = await LoadRemoveFile(url, extension: compressed ? ".zip" : ".acreplay");
            if (string.IsNullOrWhiteSpace(path)) return ArgumentHandleResult.FailedShow;

            try {
                if (!FileUtils.Exists(path)) return ArgumentHandleResult.FailedShow;
            } catch (Exception) {
                return ArgumentHandleResult.FailedShow;
            }

            try {
                if (!compressed) {
                    return await ProcessInputFile(path);
                }

                var filename = FileUtils.GetTempFileName(Path.GetTempPath(), ".acreplay");

                /*using (var archive = ZipFile.OpenRead(path)) {
                    foreach (var entry in archive.Entries.Where(x => !string.Equals(x.Name, "ReadMe.txt", StringComparison.OrdinalIgnoreCase))) {
                        await Task.Run(() => {
                            entry.ExtractToFile(filename);
                        });
                        break;
                    }
                }*/

                var archive = ZipArchive.Open(path);
                var acreplay = archive.Entries.FirstOrDefault(
                        x => x.IsDirectory == false && !string.Equals(x.Key, "ReadMe.txt", StringComparison.OrdinalIgnoreCase));
                if (acreplay == null) {
                    return ArgumentHandleResult.FailedShow;
                }

                using (var stream = acreplay.OpenEntryStream())
                using (var output = new FileStream(filename, FileMode.CreateNew)) {
                    await stream.CopyToAsync(output);
                }

                try {
                    return await ProcessInputFile(filename);
                } finally {
                    try {
                        File.Delete(filename);
                    } catch (Exception) {
                        // ignored
                    }
                }
            } finally {
                try {
                    File.Delete(path);
                } catch (Exception) {
                    // ignored
                }
            }
        }

        public async Task<ArgumentHandleResult> ProcessRsrEvent(string id) {
            Logging.Write("RSR Event: " + id);
            return await Rsr.RunAsync(id) ? ArgumentHandleResult.SuccessfulShow : ArgumentHandleResult.Failed;
        }

        public async Task<ArgumentHandleResult> ProcessRsrSetup(string id) {
            string data, header;
            using (var client = new WebClient {
                Headers = {
                        [HttpRequestHeader.UserAgent] = CmApiProvider.UserAgent
                    }
            }) {
                data = await client.DownloadStringTaskAsync($"http://www.radiators-champ.com/RSRLiveTiming/ajax.php?action=download_setup&id={id}");
                header = client.ResponseHeaders["Content-Disposition"]?.Split(new[] { "filename=" }, StringSplitOptions.None).ElementAtOrDefault(1)?.Trim();
            }

            if (data == null || header == null) {
                throw new InformativeException("Can’t install setup", "RSR has changed.");
            }

            var match = Regex.Match(header, @"^([^_]+_.+)_\d+_\d+_\d+_(.+)\.ini$");
            if (!match.Success) {
                throw new InformativeException("Can’t install setup", "RSR returned file in unsupported format.");
            }

            var ids = match.Groups[1].Value;
            var author = match.Groups[2].Value;

            CarObject car = null;
            TrackBaseObject track = null;

            var splitted = ids.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 1; i < splitted.Length - 1 && (car == null || track == null); i++) {
                var candidateCarId = splitted.Skip(i).JoinToString('_');
                var candidateTrackId = splitted.Take(i).JoinToString('_');
                car = CarsManager.Instance.GetById(candidateCarId);
                track = TracksManager.Instance.GetById(candidateTrackId);
            }

            if (car == null || track == null) {
                throw new InformativeException("Can’t install setup", "RSR returned file in unsupported format.");
            }

            var result = ShowDialog(new SharedEntry {
                Author = author,
                Data = new byte[0],
                EntryType = SharedEntryType.CarSetup,
                Id = header,
                Target = car.DisplayName
            }, applyable: false, additionalButton: "Save as Generic");

            switch (result) {
                case Choise.Save:
                case Choise.Extra:
                    var filename = FileUtils.EnsureUnique(Path.Combine(FileUtils.GetCarSetupsDirectory(car.Id),
                            result == Choise.Save ? track.Id : CarSetupObject.GenericDirectory, header));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllText(filename, data);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private enum Choise {
            ApplyAndSave,
            Apply,
            Save,
            Extra,
            Cancel
        }

        /// <summary>
        /// Shows dialog with all information about shared entry and offers a choise to user what to do with it.
        /// </summary>
        /// <param name="shared">Shared entry.</param>
        /// <param name="additionalButton">Label of additional button.</param>
        /// <param name="saveable">Can be saved.</param>
        /// <param name="applyable">Can be applied.</param>
        /// <param name="nonSaveable">Can be applied without saving.</param>
        /// <returns>User choise.</returns>
        private Choise ShowDialog(SharedEntry shared, string additionalButton = null, bool saveable = true, bool applyable = true,
                bool nonSaveable = true) {
            var description =
                    $@"Name: [b]{shared.Name ?? "[/b][i]?[/i][b]"}[/b]
For: [b]{shared.Target ?? "[/b][i]?[/i][b]"}[/b]
Author: [b]{
                            shared.Author ?? "[/b][i]?[/i][b]"}[/b]";

            var dlg = new ModernDialog {
                Title = shared.EntryType.GetDescription().ToTitle(),
                Content = new ScrollViewer {
                    Content = new BbCodeBlock {
                        BbCode = description + "\n\n" + (
                                saveable ? "Would you like to apply this preset or to save it for later?" : "Would you like to apply this preset?"),
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 480,
                MaxWidth = 640
            };

            dlg.Buttons = new[] {
                applyable && saveable ? dlg.CreateCloseDialogButton(nonSaveable ? "Apply & Save" : "Save & Apply", true, false, MessageBoxResult.Yes) : null,
                nonSaveable && applyable ? dlg.CreateCloseDialogButton(saveable ? "Apply Only" : "Apply", true, false, MessageBoxResult.OK) : null,
                saveable ? dlg.CreateCloseDialogButton(applyable && nonSaveable ? "Save Only" : "Save", true, false, MessageBoxResult.No) : null,
                additionalButton == null ? null : dlg.CreateCloseDialogButton(additionalButton, true, false, MessageBoxResult.None),
                dlg.CancelButton
            }.NonNull();
            dlg.ShowDialog();

            switch (dlg.MessageBoxResult) {
                case MessageBoxResult.None:
                    return Choise.Extra;
                case MessageBoxResult.OK:
                    return Choise.Apply;
                case MessageBoxResult.Cancel:
                    return Choise.Cancel;
                case MessageBoxResult.Yes:
                    return Choise.ApplyAndSave;
                case MessageBoxResult.No:
                    return Choise.Save;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task<ArgumentHandleResult> ProcessShared(string id) {
            SharedEntry shared;

            using (var waiting = new WaitingDialog()) {
                waiting.Report("Loading…");
                shared = await SharingHelper.GetSharedAsync(id, waiting.CancellationToken);
            }

            var data = shared?.Data;
            if (data == null) return ArgumentHandleResult.Failed;

            switch (shared.EntryType) {
                case SharedEntryType.PpFilter: {
                        var result = ShowDialog(shared, nonSaveable: false);
                        switch (result) {
                            case Choise.Save:
                            case Choise.ApplyAndSave:
                                var filename = FileUtils.EnsureUnique(Path.Combine(
                                        PpFiltersManager.Instance.Directories.EnabledDirectory, shared.GetFileName()));
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

                case SharedEntryType.CarSetup: {
                        var content = data.ToUtf8String();
                        var metadata = SharingHelper.GetMetadata(SharedEntryType.CarSetup, content, out content);

                        var carId = metadata.GetValueOrDefault("car");
                        var trackId = metadata.GetValueOrDefault("track") ?? CarSetupObject.GenericDirectory;
                        if (carId == null) {
                            throw new InformativeException("Can’t install car’s setup", "Metadata is missing.");
                        }

                        var result = ShowDialog(shared, applyable: false, additionalButton: trackId == CarSetupObject.GenericDirectory ? null : "Save as Generic");
                        switch (result) {
                            case Choise.Save:
                            case Choise.Extra:
                                var filename = FileUtils.EnsureUnique(Path.Combine(FileUtils.GetCarSetupsDirectory(carId),
                                    result == Choise.Save ? trackId : CarSetupObject.GenericDirectory, shared.GetFileName()));
                                Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                                File.WriteAllText(filename, content);
                                return ArgumentHandleResult.SuccessfulShow;
                            default:
                                return ArgumentHandleResult.Failed;
                        }
                    }

                case SharedEntryType.ControlsPreset: {
                        var result = ShowDialog(shared, "Apply FFB Only");
                        switch (result) {
                            case Choise.Save:
                            case Choise.ApplyAndSave:
                                var filename = FileUtils.EnsureUnique(Path.Combine(
                                        AcSettingsHolder.Controls.UserPresetsDirectory, "Loaded", shared.GetFileName()));
                                Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                                File.WriteAllBytes(filename, data);
                                if (result == Choise.ApplyAndSave) {
                                    AcSettingsHolder.Controls.LoadPreset(filename);
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

                case SharedEntryType.ForceFeedbackPreset: {
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

                case SharedEntryType.QuickDrivePreset: {
                        var result = ShowDialog(shared, "Just Go");
                        switch (result) {
                            case Choise.Save:
                            case Choise.ApplyAndSave:
                                var filename = FileUtils.EnsureUnique(Path.Combine(
                                        PresetsManager.Instance.GetDirectory(QuickDrive.PresetableKeyValue), "Loaded", shared.GetFileName()));
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
                                if (!QuickDrive.RunSerializedPreset(data.ToUtf8String())) {
                                    throw new InformativeException("Can’t start race", "Make sure required car & track are installed and available.");
                                }

                                return ArgumentHandleResult.SuccessfulShow;
                            default:
                                return ArgumentHandleResult.Failed;
                        }
                    }

                default:
                    throw new Exception($"Unsupported yet type: “{shared.EntryType}”");
            }
        }
    }
}