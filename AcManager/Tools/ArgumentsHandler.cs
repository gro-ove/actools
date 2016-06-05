using System;
using System.Collections.Specialized;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.SemiGui;
using AcManager.Tools.Starters;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

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
                Game.Start(AcsStarterFactory.Create(),
                        new Game.StartProperties(new Game.ReplayProperties {
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
                    case "shared":
                        return await ProcessShared(parsed.Params.Get("id"));

                    default:
                        NonfatalError.Notify($"Not supported request: “{parsed.Path}”", "Make sure format is valid");
                        return ArgumentHandleResult.Failed;
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t process request", "Make sure data is valid", e);
                return ArgumentHandleResult.Failed;
            }
        }

        private MessageBoxResult ShowDialog(SharingHelper.SharedEntry shared, bool saveable, string additionalButton = null) {
            var description = $@"Name: [b]{shared.Name ?? "[/b][i]?[/i][b]"}[/b]
For: [b]{shared.Target ?? "[/b][i]?[/i][b]"}[/b]
Author: [b]{shared.Author ?? "[/b][i]?[/i][b]"}[/b]";

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
                saveable ? dlg.CreateCloseDialogButton("Apply & Save", true, false, MessageBoxResult.Yes) : null,
                dlg.CreateCloseDialogButton(saveable ? "Apply Only" : "Apply", true, false, MessageBoxResult.OK),
                saveable ? dlg.CreateCloseDialogButton("Save Only", true, false, MessageBoxResult.No) : null,
                additionalButton == null ? null : dlg.CreateCloseDialogButton(additionalButton, true, false, MessageBoxResult.None),
                dlg.CancelButton
            }.NonNull();
            dlg.ShowDialog();
            return dlg.MessageBoxResult;
        }

        private async Task<ArgumentHandleResult> ProcessShared(string id) {
            SharingHelper.SharedEntry shared;

            using (var waiting = new WaitingDialog()) {
                waiting.Report("Loading…");
                shared = await SharingHelper.GetSharedAsync(id, waiting.CancellationToken);
            }

            var data = shared?.Data;
            if (data == null) return ArgumentHandleResult.Failed;

            MessageBoxResult result;
            switch (shared.EntryType) {
                case SharingHelper.EntryType.ControlsPreset:
                    result = ShowDialog(shared, true, "Apply FFB Only");
                    break;

                case SharingHelper.EntryType.ForceFeedbackPreset:
                    result = ShowDialog(shared, false);
                    break;

                case SharingHelper.EntryType.QuickDrivePreset:
                    result = ShowDialog(shared, true, "Just Go");
                    break;

                default:
                    throw new Exception($"Unsupported yet type: “{shared.EntryType}”");
            }

            switch (shared.EntryType) {
                case SharingHelper.EntryType.ControlsPreset:
                    switch (result) {
                        case MessageBoxResult.Yes: // save (and apply)
                        case MessageBoxResult.No:
                            var filename = Path.Combine(AcSettingsHolder.Controls.UserPresetsDirectory, "Loaded", shared.GetFileName());
                            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                            File.WriteAllBytes(filename, data);

                            if (result == MessageBoxResult.Yes) {
                                AcSettingsHolder.Controls.LoadPreset(filename);
                            }

                            return ArgumentHandleResult.SuccessfulShow;
                        case MessageBoxResult.OK: // apply only
                            if (File.Exists(AcSettingsHolder.Controls.Filename)) {
                                FileUtils.Recycle(AcSettingsHolder.Controls.Filename);
                            }

                            File.WriteAllBytes(AcSettingsHolder.Controls.Filename, data);
                            return ArgumentHandleResult.SuccessfulShow;
                        case MessageBoxResult.None: // ffb only
                            var ini = IniFile.Parse(data.ToUtf8String());
                            AcSettingsHolder.Controls.LoadFfbFromIni(ini);
                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }


                case SharingHelper.EntryType.ForceFeedbackPreset:
                    if (result == MessageBoxResult.OK) {
                        var ini = IniFile.Parse(data.ToUtf8String());
                        AcSettingsHolder.Controls.LoadFfbFromIni(ini);
                        AcSettingsHolder.System.LoadFfbFromIni(ini);
                        return ArgumentHandleResult.SuccessfulShow;
                    }
                    return ArgumentHandleResult.Failed;

                case SharingHelper.EntryType.QuickDrivePreset:
                    switch (result) {
                        case MessageBoxResult.Yes: // save (and apply)
                        case MessageBoxResult.No:
                            var filename = Path.Combine(PresetsManager.Instance.GetDirectory(QuickDrive.UserPresetableKeyValue), "Loaded", shared.GetFileName());
                            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                            File.WriteAllBytes(filename, data);

                            if (result == MessageBoxResult.Yes) {
                                QuickDrive.LoadPreset(filename);
                            }

                            return ArgumentHandleResult.SuccessfulShow;
                        case MessageBoxResult.OK: // apply only
                            QuickDrive.LoadSerializedPreset(data.ToUtf8String());
                            return ArgumentHandleResult.SuccessfulShow;
                        case MessageBoxResult.None: // just go
                            if (!QuickDrive.RunSerializedPreset(data.ToUtf8String())) {
                                throw new InformativeException("Can’t start race", "Make sure required car & track are installed and available.");
                            }
                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
            }

            return ArgumentHandleResult.FailedShow;
        }
    }
}