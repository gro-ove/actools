using System;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers.Api;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers {
    public static class IndexDirectDownloader {
        public static async Task DownloadCarAsync(string id) {
            try {
                var entry = await CmApiProvider.GetContentAsync<AcContentEntry>($"car/{id}");
                if (entry != null) {
                    await ContentInstallationManager.Instance.InstallAsync(entry.DownloadUrl, new ContentInstallationParams {
                        AllowExecutables = false,
                        DisplayName = entry.Name,
                        DisplayVersion = entry.UiVersion
                    });
                } else {
                    NonfatalError.Notify($"Can’t download car {id}", "Attempt to find a description failed.");
                }
            } catch (Exception e) {
                NonfatalError.Notify($"Can’t download car {id}", e);
            }
        }

        public static async Task DownloadTrackAsync(string id) {
            try {
                var entry = await CmApiProvider.GetContentAsync<AcContentEntry>($"track/{id}");
                if (entry != null) {
                    await ContentInstallationManager.Instance.InstallAsync(entry.DownloadUrl, new ContentInstallationParams {
                        AllowExecutables = false,
                        DisplayName = entry.Name,
                        DisplayVersion = entry.UiVersion
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
            public AcContentEntry(string id, string name, string uiVersion, string acClubVersion, string acClubUrl, string sourceUrl, string downloadUrl) {
                Id = id;
                Name = name;
                UiVersion = uiVersion;
                AcClubVersion = acClubVersion;
                AcClubUrl = acClubUrl;
                SourceUrl = sourceUrl;
                DownloadUrl = downloadUrl;
            }

            // {"id":"acc_porsche_914-6","name":"Porsche 914/6 '70","uiVersion":"1.1","acClubVersion":"1.1","acClubUrl":"http://assettocorsa.club/mods/auto/porsche-914.html","sourceUrl":null,"downloadUrl":"https://drive.google.com/file/d/0BzHandomFZfxVVJjLWl6ZlA0NU0/view?usp=sharing"}

            public string Id { get; }
            public string Name { get; }
            public string UiVersion { get; }
            public string AcClubVersion { get; }
            public string AcClubUrl { get; }
            public string SourceUrl { get; }
            public string DownloadUrl { get; }
        }
    }
}