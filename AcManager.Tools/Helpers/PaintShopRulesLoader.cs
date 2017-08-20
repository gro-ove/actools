/*using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Loaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpCompress.Readers;

// Version tag and download URL
using ToDownload = System.Tuple<string, string>;

namespace AcManager.Tools.Helpers {
    public class PaintShopRulesLoader : NotifyPropertyChanged {
        public class RulesSource : NotifyPropertyChanged {
            private static string UrlToId(string url) {
                url = Regex.Replace(url.Replace('\\', '/').ToLowerInvariant(), @"^(?:https?|ftp)://|(?<=^|:)www\.|/$", "");
                using (var sha1 = SHA1.Create()) {
                    return sha1.ComputeHash(Encoding.UTF8.GetBytes(url)).ToHexString().ToLowerInvariant();
                }
            }

            private static bool IsUrlUpdatable(string url) {
                return Regex.IsMatch(url, @"^(?:https?://)?github\.com/[^/]+/[^/]+");
            }

            public string Id { get; }
            public string Url { get; }
            public bool IsUpdatable { get; }
            public string Location { get; }
            public string Manifest => Path.Combine(Location, "Manifest.json");

            public RulesSource(string url) {
                Id = UrlToId(url);
                Url = url;
                IsUpdatable = IsUrlUpdatable(url);
                Location = FilesStorage.Instance.GetDirectory("Temporary", "Paint Shop", Id);
            }

            private async Task<ToDownload> GetVersionAndDownloadLink(CancellationToken cancellation) {
                SetProgress(ProgressPhase.DownloadingDetails);

                var github = Regex.Match(Url, @"^(?:https?://)?github\.com/([^/]+)/([^/]+)");
                if (github.Success) {
                    try {
                        var data = JArray.Parse(await HttpClientHolder.Get().GetStringAsync(
                                $@"https://api.github.com/repos/{github.Groups[1].Value}/{github.Groups[2].Value}/releases"));
                        if (cancellation.IsCancellationRequested) return null;

                        var releases = data.OfType<JObject>().Select(x => new {
                            Name = x.GetStringValueOnly("name"),
                            Version = x.GetStringValueOnly("tag_name"),
                            IsPrerelease = x.GetBoolValueOnly("prerelease"),
                            DownloadUrl = (x["assets"] as JArray)?.Select(y => new {
                                Type = y.GetStringValueOnly("content_type"),
                                Size = y.GetIntValueOnly("size"),
                                Url = y.GetStringValueOnly("browser_download_url"),
                            }).Where(y => y.Type.Contains("compressed") && y.Url != null && y.Size.HasValue)
                                                                  .MaxEntryOrDefault(y => y.Size ?? 0)?.Url
                        }).Where(x => x.Version != null && x.DownloadUrl != null).ToList();

                        var latest = releases.MaxEntryOrDefault(x => x.Version, VersionComparer.Instance);
                        if (latest == null) return null;

                        Logging.Debug($"Latest release: {latest.Name} ({latest.Version}, prerelease={latest.IsPrerelease}, download URL={latest.DownloadUrl})");
                        return new ToDownload(latest.DownloadUrl, latest.Version);
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t load repository releases information", e);
                    }
                }

                return new ToDownload(Url, null);
            }

            private enum ProgressPhase {
                [Description("Checking for update…")]
                CheckingForUpdate,

                [Description("Downloading details…")]
                DownloadingDetails,

                Downloading,

                [Description("Checking for update…")]
                CleaningUp,

                [Description("Unpacking…")]
                Unpacking
            }

            private static readonly int PhasesCount = Enum.GetNames(typeof(ProgressPhase)).Length;

            private void SetProgress(ProgressPhase phase, AsyncProgressEntry? entry = null) {
                ActionExtension.InvokeInMainThread(() => {
                    var count = PhasesCount;
                    var offset = (double)(int)phase / count;
                    Progress = entry.HasValue ?
                            new AsyncProgressEntry(entry.Value.Message, entry.Value.Progress / count + offset) :
                            new AsyncProgressEntry(phase.GetDescription(), offset);
                });
            }

            private async Task Install(string downloaded, string version, CancellationToken cancellation) {
                SetProgress(ProgressPhase.CleaningUp);
                await Task.Run(() => {
                    foreach (var v in FileUtils.GetFilesRecursive(Location)) {
                        FileUtils.TryToDelete(v);
                        if (cancellation.IsCancellationRequested) return;
                    }
                });

                if (cancellation.IsCancellationRequested) return;

                SetProgress(ProgressPhase.Unpacking);
                await Task.Run(() => {
                    using (var stream = File.OpenRead(downloaded)) {
                        var reader = ReaderFactory.Open(stream);
                        while (reader.MoveToNextEntry()) {
                            if (!reader.Entry.IsDirectory) {
                                reader.WriteEntryToDirectory(Location, new ExtractionOptions {
                                    ExtractFullPath = true,
                                    Overwrite = true
                                });
                            }

                            if (cancellation.IsCancellationRequested) return;
                        }
                    }

                    File.WriteAllText(Manifest, new JObject {
                        ["version"] = version
                    }.ToString(Formatting.Indented));
                });
            }

            private async Task Download(ToDownload toDownload, CancellationToken cancellation) {
                if (toDownload?.Item1 == null) return;
                SetProgress(ProgressPhase.Downloading);
                var destination = FileUtils.EnsureUnique(
                        FilesStorage.Instance.GetTemporaryFilename("Paint Shop Downloads", $"{Id}-{toDownload.Item2 ?? @"_"}.zip"));
                await FlexibleLoader.LoadAsyncTo(toDownload.Item1, destination, new Progress<AsyncProgressEntry>(x => {
                    SetProgress(ProgressPhase.Downloading, x);
                }), cancellation: cancellation);
                if (cancellation.IsCancellationRequested) return;
                await Install(destination, toDownload.Item2, cancellation);
            }

            private async Task Download(CancellationToken cancellation) {
                var pair = await GetVersionAndDownloadLink(cancellation);
                if (cancellation.IsCancellationRequested) return;
                await Download(pair, cancellation);
            }

            private async Task CheckForUpdate(CancellationToken cancellation) {
                SetProgress(ProgressPhase.CheckingForUpdate);
                var pair = await GetVersionAndDownloadLink(cancellation);
                RemoteVersion = pair?.Item2;
                if (cancellation.IsCancellationRequested || pair?.Item2.IsVersionNewerThan(InstalledVersion) != true) return;
                await Download(pair, cancellation);
            }

            public async Task EnsureActual(CancellationToken cancellation) {
                if (IsReady || HasFailed) return;
                FileUtils.EnsureDirectoryExists(Location);

                try {
                    var manifest = Manifest;
                    if (File.Exists(manifest)) {
                        try {
                            var json = JsonExtension.Parse((await FileUtils.ReadAllBytesAsync(manifest)).ToUtf8String());
                            InstalledVersion = json.GetStringValueOnly("version");
                            await CheckForUpdate(cancellation);
                        } catch (Exception e) {
                            NonfatalError.Notify("Can’t read manifest", e);
                            await Download(cancellation);
                        }
                    } else {
                        await Download(cancellation);
                    }

                    if (cancellation.IsCancellationRequested) return;
                    IsReady = true;
                } catch (Exception e) when (e.IsCanceled()) {
                } catch (Exception e) {
                    Logging.Warning(e);
                    HasFailed = true;
                    FailedReason = e.Message;
                }
            }

            private string _installedVersion;

            public string InstalledVersion {
                get => _installedVersion;
                set {
                    if (Equals(value, _installedVersion)) return;
                    _installedVersion = value;
                    OnPropertyChanged();
                }
            }

            private string _remoteVersion;

            public string RemoteVersion {
                get => _remoteVersion;
                set {
                    if (Equals(value, _remoteVersion)) return;
                    _remoteVersion = value;
                    OnPropertyChanged();
                }
            }

            private AsyncProgressEntry _progress;

            public AsyncProgressEntry Progress {
                get => _progress;
                set {
                    if (Equals(value, _progress)) return;
                    _progress = value;
                    OnPropertyChanged();
                }
            }

            private bool _hasFailed;

            public bool HasFailed {
                get => _hasFailed;
                set {
                    if (Equals(value, _hasFailed)) return;
                    _hasFailed = value;
                    OnPropertyChanged();
                }
            }

            private string _failedReason = "Unknown reason";

            public string FailedReason {
                get => _failedReason;
                set {
                    if (Equals(value, _failedReason)) return;
                    _failedReason = value;
                    OnPropertyChanged();
                }
            }

            private bool _isReady;

            public bool IsReady {
                get => _isReady;
                set {
                    if (Equals(value, _isReady)) return;
                    _isReady = value;
                    OnPropertyChanged();
                }
            }
        }

        public RulesSource[] Sources { get; }

        public PaintShopRulesLoader() {
            Sources = SettingsHolder.CustomShowroom.PaintShopSources.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()
                                    .Select(x => new RulesSource(x)).ToArray();
        }

        public Task EnsureActual(CancellationToken cancellation) {
            return Sources.Select(x => x.EnsureActual(cancellation)).WhenAll(10, cancellation);
        }
    }
}*/