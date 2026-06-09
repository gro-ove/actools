using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Internal;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public static class IndexDirectDownloader {
        private static bool _availableIdsLoaded;
        private static Task _availableLoadingTask;

        public static event EventHandler AvailableIdsLoaded;

        // TODO: Use CUP URLs
#if DEBUG
        private static string Registry => "http://127.0.0.1:12033/registry";
#else
        private static string Registry => InternalUtils.GetContentRegistryUrl();
#endif

        public class RegistryTypeData {
            [JsonProperty("car")]
            public string[] Cars;

            [JsonProperty("track")]
            public string[] Tracks;
        }

        public class RegistryData {
            [JsonProperty("base")]
            public RegistryTypeData BaseEntries;

            [JsonProperty("limited")]
            public RegistryTypeData LimitedEntries;
        }

        public enum ContentState {
            Unknown,
            Available,
            Limited
        }

        private static Dictionary<string, ContentState> Populate([CanBeNull] string[] baseList, [CanBeNull] string[] limitedList) {
            var dst = new Dictionary<string, ContentState>();
            if (baseList != null) {
                foreach (var id in baseList) {
                    dst[id] = ContentState.Available;
                }
            }
            if (limitedList != null) {
                foreach (var id in limitedList) {
                    dst[id] = ContentState.Limited;
                }
            }
            return dst;
        }

        private static async Task LoadAvailableIdsAsyncInner() {
            try {
                var data = JsonConvert.DeserializeObject<RegistryData>(await HttpClientHolder.Get()
                        .GetStringAsync(SettingsHolder.Online.SearchContentMode.IntValue == 1
                                ? $"{Registry}/index/original" : $"{Registry}/index/all"));
                AvailableCarIds = Populate(data.BaseEntries?.Cars, data.LimitedEntries?.Cars);
                AvailableTrackIds = Populate(data.BaseEntries?.Tracks, data.LimitedEntries?.Tracks);
                AvailableIdsLoaded?.Invoke(null, EventArgs.Empty);
            } catch (Exception e) {
                Logging.Warning(e);
            } finally {
                _availableLoadingTask = null;
                _availableIdsLoaded = true;
            }
        }

        public static Task LoadAvailableIdsAsync() {
            if (_availableIdsLoaded) return Task.Delay(0);
            if (_availableLoadingTask != null) return _availableLoadingTask;
            return _availableLoadingTask = LoadAvailableIdsAsyncInner();
        }

        private static Dictionary<string, ContentState> AvailableCarIds { get; set; }
        private static Dictionary<string, ContentState> AvailableTrackIds { get; set; }

        public static async Task<ContentState> IsCarAvailableAsync(string carId) {
            await LoadAvailableIdsAsyncInner();
            return IsCarAvailable(carId);
        }

        public static async Task<ContentState> IsTrackAvailableAsync(string trackId) {
            await LoadAvailableIdsAsyncInner();
            return IsTrackAvailable(trackId);
        }

        public static ContentState IsCarAvailable(string carId) {
            return AvailableCarIds?.GetValueOrDefault(carId) ?? ContentState.Unknown;
        }

        public static ContentState IsTrackAvailable(string trackId) {
            // TODO: trackId uses / as separator, check the layout as well!
            // return AvailableTrackIds != null && Array.IndexOf(AvailableTrackIds, trackId.Replace(@"/", @"-")) != -1;
            return AvailableCarIds?.GetValueOrDefault(trackId.Split('/')[0]) ?? ContentState.Unknown;
        }

        private static async Task DownloadSomethingAsync(CupContentType type,
                [NotNull] string id, string version = null) {
            if (CupClient.Instance == null) return;
            var displayType = type.GetDescription().ToSentenceMember();
            try {
                var entry = await CupClient.Instance.LoadCupEntryAsync(Registry,
                        new CupClient.CupKey(type, id));
                if (entry != null) {
                    if (version != null && entry.Version != version) {
                        throw new InformativeException($"Can’t download {displayType} “{id}”",
                                $"Not the required version: indexed is {entry.Version}, while required is {version}.");
                    }

                    var redirect = await CupClient.Instance.GetUpdateUrlFromAsync(Registry, type, id);
                    if (entry.IsToUpdateManually) {
                        WindowsHelper.ViewInBrowser(redirect.Item1);
                    } else {
                        await ContentInstallationManager.Instance.InstallAsync(redirect.Item1, new ContentInstallationParams(false) {
                            ForcedFileName = redirect.Item2,
                            DisplayName = entry.Name,
                            Version = entry.Version,
                            InformationUrl = entry.InformationUrl
                        });
                    }
                } else {
                    throw new InformativeException($"Can’t download {displayType} “{id}”", "Attempt to find a description failed.");
                }
            } catch (Exception e) {
                NonfatalError.Notify($"Can’t download {displayType} “{id}”", e);
            }
        }

        public static Task DownloadCarAsync([NotNull] string id, string version = null) {
            return DownloadSomethingAsync(CupContentType.Car, id, version);
        }

        public static Task DownloadTrackAsync([NotNull] string id, string version = null) {
            // id = id.Replace(@"/", @"-");
            return DownloadSomethingAsync(CupContentType.Track, id.Split('/')[0], version);
        }
    }
}