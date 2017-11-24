using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public static class IndexDirectDownloader {
        private static bool _availableIdsLoaded;
        private static Task _availableLoadingTask;

        public static event EventHandler AvailableIdsLoaded;

        private static async Task LoadAvailableIdsAsyncInner() {
            try {
                var ids = await CmApiProvider.GetContentAsync<Dictionary<string, string[]>>();
                if (ids == null) {
                    Logging.Warning("Can’t load lists of available-to-download IDs");
                    return;
                }

                AvailableCarIds = ids.GetValueOrDefault("cars");
                AvailableTrackIds = ids.GetValueOrDefault("tracks");
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

        private static string[] AvailableCarIds { get; set; }
        private static string[] AvailableTrackIds { get; set; }

        public static async Task<bool> IsCarAvailableAsync(string carId) {
            await LoadAvailableIdsAsyncInner();
            return AvailableCarIds != null && Array.IndexOf(AvailableCarIds, carId) != -1;
        }

        public static async Task<bool> IsTrackAvailableAsync(string trackId) {
            await LoadAvailableIdsAsyncInner();
            return AvailableTrackIds != null && Array.IndexOf(AvailableCarIds, trackId.Replace(@"/", @"-")) != -1;
        }

        public static bool IsCarAvailable(string carId) {
            return AvailableCarIds != null && Array.IndexOf(AvailableCarIds, carId) != -1;
        }

        public static bool IsTrackAvailable(string trackId) {
            return AvailableTrackIds != null && Array.IndexOf(AvailableTrackIds, trackId.Replace(@"/", @"-")) != -1;
        }

        public static async Task DownloadCarAsync([NotNull] string id, string version = null) {
            try {
                var entry = await CmApiProvider.GetContentAsync<AcContentEntry>($"car/{id}");
                if (entry != null) {
                    if (version != null && entry.UiVersion != version && entry.OriginVersion != version) {
                        throw new InformativeException($"Can’t download car “{id}”",
                                $"Not the required version: indexed is {entry.UiVersion ?? entry.OriginVersion}, while required is {version}.");
                    }

                    if (!entry.ImmediateDownload && entry.OriginUrl != null && (Keyboard.Modifiers & ModifierKeys.Control) == 0) {
                        WindowsHelper.ViewInBrowser(entry.OriginUrl);
                    } else {
                        await ContentInstallationManager.Instance.InstallAsync(entry.DownloadUrl, new ContentInstallationParams {
                            AllowExecutables = false,
                            DisplayName = entry.UiName,
                            DisplayVersion = entry.UiVersion,
                            InformationUrl = entry.OriginUrl
                        });
                    }
                } else {
                    throw new InformativeException($"Can’t download car “{id}”", "Attempt to find a description failed.");
                }
            } catch (Exception e) {
                NonfatalError.Notify($"Can’t download car “{id}”", e);
            }
        }

        public static async Task DownloadTrackAsync([NotNull] string id, string version = null) {
            id = id.Replace(@"/", @"-");

            try {
                var entry = await CmApiProvider.GetContentAsync<AcContentEntry>($"track/{id}");
                if (entry != null) {
                    if (version != null && entry.UiVersion != version && entry.OriginVersion != version) {
                        throw new InformativeException($"Can’t download track “{id}”",
                                $"Not the required version: indexed is {entry.UiVersion ?? entry.OriginVersion}, while required is {version}.");
                    }

                    if (entry.OriginUrl != null && (Keyboard.Modifiers & ModifierKeys.Control) == 0) {
                        WindowsHelper.ViewInBrowser(entry.OriginUrl);
                    } else {
                        await ContentInstallationManager.Instance.InstallAsync(entry.DownloadUrl, new ContentInstallationParams {
                            AllowExecutables = false,
                            DisplayName = entry.UiName ?? entry.OriginName,
                            DisplayVersion = entry.UiVersion,
                            InformationUrl = entry.OriginUrl
                        });
                    }
                } else {
                    NonfatalError.Notify($"Can’t download track “{id}”", "Attempt to find a description failed.");
                }
            } catch (Exception e) {
                NonfatalError.Notify($"Can’t download track “{id}”", e);
            }
        }

        private class AcContentEntry {
            [JsonConstructor]
            public AcContentEntry(string id, string uiName, string originName, string uiVersion, string originVersion, string originUrl, string authorUrl,
                    string downloadUrl, bool immediateDownload) {
                Id = id;
                UiName = uiName;
                OriginName = originName;
                UiVersion = uiVersion;
                OriginVersion = originVersion;
                OriginUrl = originUrl;
                AuthorUrl = authorUrl;
                DownloadUrl = downloadUrl;
                ImmediateDownload = immediateDownload;
            }

            public string Id { get; }
            public string UiName { get; }
            public string OriginName { get; }
            public string UiVersion { get; }
            public string OriginVersion { get; }
            public string OriginUrl { get; }
            public string AuthorUrl { get; }
            public string DownloadUrl { get; }
            public bool ImmediateDownload { get; }
        }
    }
}