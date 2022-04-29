using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using AcManager.Internal;
using AcManager.Pages.Drive;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.TheSetupMarket;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using SharpCompress.Archives.Zip;

namespace AcManager.Tools {
    public static partial class ArgumentsHandler {
        public static bool IsCmCommand(Uri uri) {
            return uri.IsAbsoluteUri && (uri.OriginalString.StartsWith(@"https://acstuff.ru/s/", StringComparison.OrdinalIgnoreCase)
                    || uri.OriginalString.StartsWith(@"http://acstuff.ru/s/", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, @"acmanager", StringComparison.OrdinalIgnoreCase));
        }

        public static string UnwrapDownloadRequest(string request) {
            if (!request.StartsWith($@"{CustomUriSchemeHelper.UriScheme}//", StringComparison.Ordinal)) {
                var splitted = request.Split(new[] { '/' }, 2);
                if (splitted.Length != 2) return null;

                var index = splitted[1].IndexOf('?');
                if (index != -1) {
                    splitted[1] = splitted[1].Substring(0, index);
                }

                return splitted[0] == @"install" ? splitted[1] : null;
            }

            CustomUriRequest custom;
            try {
                custom = CustomUriRequest.Parse(request);
            } catch (Exception e) when (e.IsCancelled()) {
                return null;
            } catch (Exception) {
                NonfatalError.Notify(AppStrings.Arguments_CannotParseRequest, AppStrings.Main_CannotProcessArgument_Commentary);
                return null;
            }

            switch (custom.Path.ToLowerInvariant()) {
                case "install":
                    return custom.Params.Get(@"url");
            }

            return null;
        }

        [Obsolete]
        private static async Task<ArgumentHandleResult> ProcessUriRequestObsolete(string request) {
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
                    if (!await QuickDrive.RunAsync(serializedPreset: preset)) {
                        NonfatalError.Notify(AppStrings.Common_CannotStartRace, AppStrings.Arguments_CannotStartRace_Commentary);
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
                    var address = Convert.FromBase64String(param).ToUtf8String();
                    try {
                        return await ProcessInputFile(await LoadRemoteFile(address, query?.Get(@"name")));
                    } catch (Exception e) when (e.IsCancelled()) {
                        return ArgumentHandleResult.Failed;
                    } catch (Exception e) {
                        Logging.Warning(e);
                        return ArgumentHandleResult.FailedShow;
                    }

                case "install":
                    return await ContentInstallationManager.Instance.InstallAsync(param, new ContentInstallationParams(true))
                            ? ArgumentHandleResult.Successful : ArgumentHandleResult.Failed;
            }

            return ArgumentHandleResult.Successful;
        }

        private static async Task<ArgumentHandleResult> ProcessUriRequest(string uri) {
            if (!IsCustomUriScheme(uri)) return ArgumentHandleResult.FailedShow;

            var request = uri.SubstringExt(CustomUriSchemeHelper.UriScheme.Length);
            Logging.Debug("URI Request: " + request);

            if (!request.StartsWith(@"//", StringComparison.Ordinal)) {
#pragma warning disable 612
                return await ProcessUriRequestObsolete(request);
#pragma warning restore 612
            }

            CustomUriRequest custom;
            try {
                custom = CustomUriRequest.Parse(uri);
            } catch (Exception e) when (e.IsCancelled()) {
                return ArgumentHandleResult.Failed;
            } catch (Exception) {
                NonfatalError.Notify(AppStrings.Arguments_CannotParseRequest, AppStrings.Main_CannotProcessArgument_Commentary);
                return ArgumentHandleResult.Failed;
            }

            try {
                switch (custom.Path.ToLowerInvariant()) {
                    case "batch":
                        var commands = Encoding.UTF8.GetString(custom.Params.Get(@"cmds").FromCutBase64() ?? new byte[0]);
                        Logging.Debug("Batch command: " + commands);
                        return (await commands.Split('\n').Select(ProcessUriRequest).WhenAll()).Max();

                    case "launch":
                        return ArgumentHandleResult.SuccessfulShow;

                    case "race/quick":
                        return await ProcessRaceQuick(custom);

                    case "race/config":
                        return await ProcessRaceConfig(custom);

                    case "race/online":
                        return await ProcessRaceOnline(custom.Params);

                    case "race/online/join":
                        return await ProcessRaceOnlineJoin(custom.Params);

                    case "race/raceu":
                        return await ProcessRaceRaceU(custom.Params);

                    case "race/worldsimseries":
                        return await ProcessWorldSimSeries(custom.Params);

                    case "race/worldsimseries/login":
                        return await ProcessWorldSimSeriesLogin(custom.Params);

                    case "setsteamid":
                        return ArgumentHandleResult.Ignore; // TODO?

                    case "loadgooglespreadsheetslocale":
                        return await ProcessGoogleSpreadsheetsLocale(custom.Params.Get(@"id"), custom.Params.Get(@"locale"), custom.Params.GetFlag(@"around"));

                    case "install":
                        var urls = custom.Params.GetValues(@"url") ?? new string[0];
                        if (custom.Params.GetFlag("fromWebsite")) {
                            Logging.Debug("From website:" + urls.JoinToString(@"; "));
                            ModsWebBrowser.PrepareForCommand(urls, custom.Params.GetValues(@"websiteData") ?? new string[0]);
                        }

                        return (await urls.Select(
                                x => ContentInstallationManager.Instance.InstallAsync(x, new ContentInstallationParams(true) {
                                    CarId = custom.Params.Get(@"car")
                                })).WhenAll()).Any() ? ArgumentHandleResult.Successful : ArgumentHandleResult.Failed;

                    case "importwebsite":
                        return await ProcessImportWebsite(custom.Params.GetValues(@"data") ?? new string[0]);

                    case "cup/registry":
                        return await ProcessCupRegistry(custom.Params.Get(@"url"));

                    case "live":
                        return await ProcessLiveService(custom.Params.Get(@"url"), custom.Params.Get(@"name"), custom.Params.Get(@"color"));

                    case "csp/install":
                        return await ProcessCspInstall(custom.Params.Get(@"version"));

                    case "replay":
                        return await ProcessReplay(custom.Params.Get(@"url"), custom.Params.Get(@"uncompressed") == null);

                    case "rsr":
                        return await ProcessRsrEvent(custom.Params.Get(@"id"));

                    case "rsr/setup":
                        return await ProcessRsrSetup(custom.Params.Get(@"id"));

                    case "thesetupmarket/setup":
                        return await ProcessTheSetupMarketSetup(custom.Params.Get(@"id"));

                    case "shared":
                        var result = ArgumentHandleResult.Ignore;
                        foreach (var id in custom.Params.GetValues(@"id") ?? new string[0]) {
                            result = await ProcessSharedById(id, custom.Params.GetValues(@"go") != null);
                        }
                        return result;

                    default:
                        NonfatalError.Notify(string.Format(AppStrings.Main_NotSupportedRequest, custom.Path), AppStrings.Main_CannotProcessArgument_Commentary);
                        return ArgumentHandleResult.Failed;
                }
            } catch (Exception e) when (e.IsCancelled()) {
                return ArgumentHandleResult.Failed;
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Arguments_CannotProcessRequest, AppStrings.Arguments_CannotProcessRequest_Commentary, e);
                return ArgumentHandleResult.Failed;
            }
        }

        private static async Task<ArgumentHandleResult> ProcessGoogleSpreadsheetsLocale(string id, [CanBeNull] string locale, bool around) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InformativeException(ToolsStrings.Common_IdIsMissing);
            }

            var url = around
                    ? $@"{InternalUtils.MainApiDomain}/u/around?id={id}" : $@"https://docs.google.com/spreadsheets/d/{id}/export?format=xlsx&authuser=0";
            await Task.Run(() => {
                if (File.Exists(LocaleHelper.GetGoogleSheetsFilename())) {
                    FileUtils.Recycle(LocaleHelper.GetGoogleSheetsFilename());
                    FileUtils.TryToDelete(LocaleHelper.GetGoogleSheetsFilename());
                }
            });
            await LoadRemoteFileToNew(url, LocaleHelper.GetGoogleSheetsFilename());

            SettingsHolder.Locale.LoadUnpacked = true;
            if (locale != null) {
                SettingsHolder.Locale.LocaleName = locale;
            }

            ActionExtension.InvokeInMainThreadAsync(() => {
                if (ModernDialog.ShowMessage(AppStrings.CustomLocalesUpdated_Message, AppStrings.CustomLocalesUpdated_Title, MessageBoxButton.YesNo) ==
                        MessageBoxResult.Yes) {
                    WindowsHelper.RestartCurrentApplication();
                }
            });

            return ArgumentHandleResult.Successful;
        }

        private static async Task<ArgumentHandleResult> ProcessImportWebsite(string[] data) {
            var result = await ModsWebBrowser.ImportWebsitesAsync(data, names => {
                return Task.FromResult(ModernDialog.ShowMessage(
                        $"Details for {names.Select(x => $"“{x}”").JoinToReadableString()} contain scripts which will have access to your data on those websites. Do you trust the source? You can verify scripts later in Content/Browser section.",
                        "Details with scripts", MessageBoxButton.YesNo) == MessageBoxResult.Yes);
            }).ConfigureAwait(false);
            switch (result) {
                case null:
                    return ArgumentHandleResult.Failed;
                case 0:
                    Toast.Show("Nothing to import", data.Length == 1 ? "That website is already added" : "Those websites are already added");
                    return ArgumentHandleResult.Successful;
                case 1:
                    Toast.Show("Imported", "Website has been added");
                    return ArgumentHandleResult.Successful;
                default:
                    Toast.Show("Imported", $"Added {PluralizingConverter.PluralizeExt(result.Value, @"{0} website")}");
                    return ArgumentHandleResult.Successful;
            }
        }

        private static async Task<ArgumentHandleResult> ProcessCupRegistry(string url) {
            await Task.Delay(0);

            if (SettingsHolder.Content.CupRegistriesList.Contains(url)) {
                Toast.Show("Nothing to import", "This CUP registry is already added");
                return ArgumentHandleResult.Successful;
            }

            if (ModernDialog.ShowMessage(
                    $"Do you want to add “{url}” to the list of CUP registeries? Content Manager will use it to check and download latest updates for installed mods.",
                    "New CUP registry", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                SettingsHolder.Content.CupRegistriesList = SettingsHolder.Content.CupRegistriesList.Append(url);
                Toast.Show("New CUP registry", "New CUP registry has been added");
                return ArgumentHandleResult.Successful;
            }

            return ArgumentHandleResult.Failed;
        }

        private static async Task<ArgumentHandleResult> ProcessLiveService(string url, string name, string color) {
            await Task.Delay(0);

            if (SettingsHolder.Live.UserEntries.Any(x => x.Url == url)) {
                Toast.Show("Nothing to import", "This CUP registry is already added");
                return ArgumentHandleResult.Successful;
            }

            if (ModernDialog.ShowMessage(
                    $"Do you want to add “{url}” as a new live service? When used, it would get access to your Steam ID and list of installed content.",
                    "New live service", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                SettingsHolder.Live.UserEntries =
                        SettingsHolder.Live.UserEntries.Append(new SettingsHolder.LiveSettings.LiveServiceEntry(url, name, color)).ToList();
                Toast.Show("New live service", "New live service has been added");
                return ArgumentHandleResult.Successful;
            }

            return ArgumentHandleResult.Failed;
        }

        private static async Task<ArgumentHandleResult> ProcessCspInstall(string versionString) {
            using (var waiting = new WaitingDialog("Installing CSP…")) {
                var versions = await PatchVersionInfo.GetPatchManifestAsync(null, waiting.CancellationToken);
                if (waiting.CancellationToken.IsCancellationRequested) return ArgumentHandleResult.Failed;

                PatchVersionInfo info;
                if (versionString == @"latest") {
                    info = versions.MaxEntry(x => x.Build);
                } else if (versionString == @"recommended") {
                    info = versions.Where(x => x.Tags?.Contains("recommended") == true).MaxEntry(x => x.Build);
                } else {
                    var version = versionString.As(0);
                    if (version == 0) {
                        throw new Exception($"Wrong parameter: version={versionString}, number expected");
                    }
                    info = versions.FirstOrDefault(x => x.Build == version);
                }

                if (info == null) {
                    throw new Exception($"Wrong parameter: version={versionString}, no such version");
                }

                await PatchUpdater.Instance.InstallAsync(info, waiting.CancellationToken);

                using (var model = PatchSettingsModel.Create()) {
                    var item = model.Configs?
                            .FirstOrDefault(x => x.FileNameWithoutExtension == "general")?.Sections.GetByIdOrDefault("BASIC")?
                            .GetByIdOrDefault("ENABLED");
                    if (item != null) {
                        item.Value = @"1";
                    }
                }
            }
            return ArgumentHandleResult.Successful;
        }

        private static async Task<ArgumentHandleResult> ProcessReplay(string url, bool compressed) {
            var path = await LoadRemoteFile(url, extension: compressed ? @".zip" : @".acreplay");

            try {
                if (!FileUtils.Exists(path)) return ArgumentHandleResult.FailedShow;
            } catch (Exception) {
                return ArgumentHandleResult.FailedShow;
            }

            try {
                if (!compressed) {
                    return await ProcessInputFile(path);
                }

                var filename = FileUtils.GetTempFileName(Path.GetTempPath(), @".acreplay");
                var archive = ZipArchive.Open(path);
                var acreplay = archive.Entries.FirstOrDefault(
                        x => x.IsDirectory == false && !string.Equals(x.Key, @"ReadMe.txt", StringComparison.OrdinalIgnoreCase));
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

        private static async Task<ArgumentHandleResult> ProcessRsrEvent(string id) {
            Logging.Write("RSR Event: " + id);
            return await Rsr.RunAsync(id) ? ArgumentHandleResult.SuccessfulShow : ArgumentHandleResult.Failed;
        }

        private static async Task<ArgumentHandleResult> ProcessTheSetupMarketSetup(string id) {
            var details = await TheSetupMarketApiProvider.GetSetupFullInformation(id);
            if (details == null) {
                throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, "The Setup Market is unavailable or has changed.");
            }

            var car = CarsManager.Instance.GetById(details.Item1.CarId);
            var track = details.Item1.TrackKunosId == null ? null : TracksManager.Instance.GetLayoutByKunosId(details.Item1.TrackKunosId);
            var setupId = details.Item1.FileName;

            var result = ShowDialog(new SharedEntry {
                Author = details.Item1.Author,
                Name = setupId.ApartFromLast(".ini", StringComparison.OrdinalIgnoreCase),
                Data = new byte[0],
                EntryType = SharedEntryType.CarSetup,
                Id = setupId,
                Target = car?.DisplayName ?? details.Item1.CarId
            }, false, applyable: false, additionalButton: AppStrings.Arguments_SaveAsGeneric);

            switch (result) {
                case Choise.Save:
                case Choise.Extra:
                    var filename = FileUtils.EnsureUnique(Path.Combine(AcPaths.GetCarSetupsDirectory(details.Item1.CarId),
                            result == Choise.Save
                                    ? (track?.Id ?? details.Item1.TrackKunosId ?? CarSetupObject.GenericDirectory) : CarSetupObject.GenericDirectory, setupId));
                    FileUtils.EnsureFileDirectoryExists(filename);
                    File.WriteAllText(filename, details.Item2);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private static async Task<ArgumentHandleResult> ProcessRsrSetup(string id) {
            string data, header;
            using (var client = new WebClient()) {
                data = await client.DownloadStringTaskAsync($"http://www.radiators-champ.com/RSRLiveTiming/ajax.php?action=download_setup&id={id}");
                header =
                        client.ResponseHeaders[@"Content-Disposition"]?.Split(new[] { @"filename=" }, StringSplitOptions.None).ArrayElementAtOrDefault(1)?.Trim();
            }

            if (data == null || header == null) {
                throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, AppStrings.Arguments_CannotInstallSetup_Commentary);
            }

            var match = Regex.Match(header, @"^([^_]+_.+)_\d+_\d+_\d+_(.+)\.ini$");
            if (!match.Success) {
                throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, AppStrings.Arguments_CannotInstallSetup_CommentaryFormat);
            }

            var ids = match.Groups[1].Value;
            var author = match.Groups[2].Value;

            CarObject car = null;
            TrackObjectBase track = null;

            var splitted = ids.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 1; i < splitted.Length - 1 && (car == null || track == null); i++) {
                var candidateCarId = splitted.Skip(i).JoinToString('_');
                var candidateTrackId = splitted.Take(i).JoinToString('_');
                car = CarsManager.Instance.GetById(candidateCarId);
                track = TracksManager.Instance.GetById(candidateTrackId);
            }

            if (car == null || track == null) {
                throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, AppStrings.Arguments_CannotInstallSetup_CommentaryFormat);
            }

            var result = ShowDialog(new SharedEntry {
                Author = author,
                Data = new byte[0],
                EntryType = SharedEntryType.CarSetup,
                Id = header,
                Target = car.DisplayName
            }, false, applyable: false, additionalButton: AppStrings.Arguments_SaveAsGeneric);

            switch (result) {
                case Choise.Save:
                case Choise.Extra:
                    var filename = FileUtils.EnsureUnique(Path.Combine(AcPaths.GetCarSetupsDirectory(car.Id),
                            result == Choise.Save ? track.Id : CarSetupObject.GenericDirectory, header));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllText(filename, data);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }
    }
}