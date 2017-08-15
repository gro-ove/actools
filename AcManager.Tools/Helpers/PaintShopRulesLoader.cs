using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json.Linq;

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

            private async Task<Tuple<string, string>> GetVersionAndDownloadLink(CancellationToken cancellation) {
                var github = Regex.Match(Url, @"^(?:https?://)?github\.com/([^/]+)/([^/]+)");
                if (github.Success) {
                    try {
                        var data = JArray.Parse(await HttpClientHolder.Get().GetStringAsync(
                                $@"https://api.github.com/repos/{github.Groups[1].Value}/{github.Groups[2].Value}/releases"));
                        if (cancellation.IsCancellationRequested) return null;

                        var newestRelease = data.OfType<JObject>().MinEntryOrDefault(x =)
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t load repository releases information", e);
                    }
                }

                return Tuple.Create<string, string>(Url, null);
            }

            private async Task Download(CancellationToken cancellation) {

            }

            private async Task CheckForUpdate(CancellationToken cancellation) {
            }

            public async Task EnsureActual(CancellationToken cancellation) {
                FileUtils.EnsureDirectoryExists(Location);

                var manifest = Manifest;
                if (File.Exists(manifest)) {
                    try {
                        var json = JsonExtension.Parse((await FileUtils.ReadAllBytesAsync(manifest)).ToUtf8String());
                        InstalledVersion = json.GetStringValueOnly("version");
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t read manifest", e);
                        await Download(cancellation);
                    }
                } else {
                    await Download(cancellation);
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
            Sources = SettingsHolder.CustomShowroom.PaintShopSources.Select(x => new RulesSource(x)).ToArray();
        }

        public Task EnsureActual(CancellationToken cancellation) {
            return Sources.Select(x => x.EnsureActual(cancellation)).WhenAll(10, cancellation);
        }
    }
}