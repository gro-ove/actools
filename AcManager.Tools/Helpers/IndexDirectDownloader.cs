using System;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers.Api;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public static class IndexDirectDownloader {
        public static async Task DownloadCarAsync([NotNull] string id) {
            try {
                var entry = await CmApiProvider.GetContentAsync<AcContentEntry>($"car/{id}");
                if (entry != null) {
                    await ContentInstallationManager.Instance.InstallAsync(entry.DownloadUrl, new ContentInstallationParams {
                        AllowExecutables = false,
                        DisplayName = entry.UiName,
                        DisplayVersion = entry.UiVersion,
                        InformationUrl = entry.OriginUrl
                    });
                } else {
                    NonfatalError.Notify($"Can’t download car {id}", "Attempt to find a description failed.");
                }
            } catch (Exception e) {
                NonfatalError.Notify($"Can’t download car {id}", e);
            }
        }

        public static async Task DownloadTrackAsync([NotNull] string id) {
            id = id.Replace(@"/", @"-");

            try {
                var entry = await CmApiProvider.GetContentAsync<AcContentEntry>($"track/{id}");
                if (entry != null) {
                    await ContentInstallationManager.Instance.InstallAsync(entry.DownloadUrl, new ContentInstallationParams {
                        AllowExecutables = false,
                        DisplayName = entry.UiName ?? entry.OriginName,
                        DisplayVersion = entry.UiVersion,
                        InformationUrl = entry.OriginUrl
                    });
                } else {
                    NonfatalError.Notify($"Can’t download track {id}", "Attempt to find a description failed.");
                }
            } catch (Exception e) {
                NonfatalError.Notify($"Can’t download track {id}", e);
            }
        }

        private class AcContentEntry {
            [JsonConstructor]
            public AcContentEntry(string id, string uiName, string originName, string uiVersion, string originVersion, string originUrl, string authorUrl, string downloadUrl) {
                Id = id;
                UiName = uiName;
                OriginName = originName;
                UiVersion = uiVersion;
                OriginVersion = originVersion;
                OriginUrl = originUrl;
                AuthorUrl = authorUrl;
                DownloadUrl = downloadUrl;
            }

            public string Id { get; }
            public string UiName { get; }
            public string OriginName { get; }
            public string UiVersion { get; }
            public string OriginVersion { get; }
            public string OriginUrl { get; }
            public string AuthorUrl { get; }
            public string DownloadUrl { get; }
        }
    }
}