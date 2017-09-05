using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using AcManager.Pages.Drive;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.TheSetupMarket;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using SharpCompress.Archives.Zip;

namespace AcManager.Tools {
    public static partial class ArgumentsHandler {
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
                        return await ProcessInputFile(await LoadRemoveFile(address, query?.Get(@"name")));
                    } catch (Exception e) when (e.IsCanceled()) {
                        return ArgumentHandleResult.Failed;
                    } catch (Exception e) {
                        Logging.Warning(e);
                        return ArgumentHandleResult.FailedShow;
                    }

                case "install":
                    return await ContentInstallationManager.Instance.InstallAsync(param, new ContentInstallationParams {
                        AllowExecutables = true
                    }) ? ArgumentHandleResult.Successful : ArgumentHandleResult.Failed;
            }

            return ArgumentHandleResult.Successful;
        }

        private static async Task<ArgumentHandleResult> ProcessUriRequest(string uri) {
            if (!uri.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.OrdinalIgnoreCase)) return ArgumentHandleResult.FailedShow;

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
            } catch (Exception e) when (e.IsCanceled()) {
                return ArgumentHandleResult.Failed;
            } catch (Exception) {
                NonfatalError.Notify(AppStrings.Arguments_CannotParseRequest, AppStrings.Main_CannotProcessArgument_Commentary);
                return ArgumentHandleResult.Failed;
            }

            try {
                switch (custom.Path.ToLowerInvariant()) {
                    case "setsteamid":
                        return ArgumentHandleResult.Ignore; // TODO?

                    case "race/online":
                        return await ProgressRaceOnline(custom.Params);

                    case "race/online/join":
                        return await ProgressRaceOnlineJoin(custom.Params);

                    case "loadgooglespreadsheetslocale":
                        return await ProcessGoogleSpreadsheetsLocale(custom.Params.Get(@"id"), custom.Params.Get(@"locale"), custom.Params.GetFlag(@"around"));

                    case "install":
                        return await ContentInstallationManager.Instance.InstallAsync(custom.Params.Get(@"url"), new ContentInstallationParams {
                            AllowExecutables = true
                        }) ? ArgumentHandleResult.Successful : ArgumentHandleResult.Failed;

                    case "replay":
                        return await ProcessReplay(custom.Params.Get(@"url"), custom.Params.Get(@"uncompressed") == null);

                    case "rsr":
                        return await ProcessRsrEvent(custom.Params.Get(@"id"));

                    case "rsr/setup":
                        return await ProcessRsrSetup(custom.Params.Get(@"id"));

                    case "thesetupmarket/setup":
                        return await ProcessTheSetupMarketSetup(custom.Params.Get(@"id"));

                    case "shared":
                        return await ProcessShared(custom.Params.Get(@"id"));

                    default:
                        NonfatalError.Notify(string.Format(AppStrings.Main_NotSupportedRequest, custom.Path), AppStrings.Main_CannotProcessArgument_Commentary);
                        return ArgumentHandleResult.Failed;
                }
            } catch (Exception e) when (e.IsCanceled()) {
                return ArgumentHandleResult.Failed;
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Arguments_CannotProcessRequest, AppStrings.Arguments_CannotProcessRequest_Commentary, e);
                return ArgumentHandleResult.Failed;
            }
        }

        private static async Task<ArgumentHandleResult> ProcessGoogleSpreadsheetsLocale(string id, [CanBeNull] string locale, bool around) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InformativeException("ID is missing");
            }

            var url = around ? $@"http://acstuff.ru/u/around?id={id}" : $@"https://docs.google.com/spreadsheets/d/{id}/export?format=xlsx&authuser=0";
            await LoadRemoveFileToNew(url, LocaleHelper.GetGoogleSheetsFilename());

            SettingsHolder.Locale.LoadUnpacked = true;
            if (locale != null) {
                SettingsHolder.Locale.LocaleName = locale;
            }

            if (ModernDialog.ShowMessage("Custom locales updated. Would you like to restart app now?", "Locales Updated", MessageBoxButton.YesNo) ==
                    MessageBoxResult.Yes) {
                WindowsHelper.RestartCurrentApplication();
            }

            return ArgumentHandleResult.Successful;
        }

        private static async Task<ArgumentHandleResult> ProcessReplay(string url, bool compressed) {
            var path = await LoadRemoveFile(url, extension: compressed ? @".zip" : @".acreplay");

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
            }, applyable: false, additionalButton: AppStrings.Arguments_SaveAsGeneric);

            switch (result) {
                case Choise.Save:
                case Choise.Extra:
                    var filename = FileUtils.EnsureUnique(Path.Combine(FileUtils.GetCarSetupsDirectory(details.Item1.CarId),
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
            using (var client = new WebClient {
                Headers = {
                    [HttpRequestHeader.UserAgent] = CmApiProvider.UserAgent
                }
            }) {
                data = await client.DownloadStringTaskAsync($"http://www.radiators-champ.com/RSRLiveTiming/ajax.php?action=download_setup&id={id}");
                header = client.ResponseHeaders[@"Content-Disposition"]?.Split(new[] { @"filename=" }, StringSplitOptions.None).ElementAtOrDefault(1)?.Trim();
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
            }, applyable: false, additionalButton: AppStrings.Arguments_SaveAsGeneric);

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
    }
}