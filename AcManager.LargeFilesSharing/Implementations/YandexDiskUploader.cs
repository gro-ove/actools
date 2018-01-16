using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.LargeFilesSharing.Implementations {
    public class YandexDiskUploader : FileUploaderBase {
        public YandexDiskUploader(IStorage storage) : base(storage, "Yandex.Disk (App Folder)",
                new Uri("/AcManager.LargeFilesSharing;component/Assets/Icons/YandexDisk.png", UriKind.Relative),
                "10 GB of space by default, does not really have a public API, but with a bit of non-public one, it’s still better than Google Drive. This version uploads to app’s folder.",
                true, true) {
            Request.AuthorizationTokenType = "OAuth";
        }

        private static Task<OAuthCode> GetAuthenticationCode(string clientId, CancellationToken cancellation) {
            return OAuth.GetCode("Yandex.Disk",
                    $"https://oauth.yandex.com/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}", null, cancellation: cancellation);
        }

#pragma warning disable 0649
        internal class AuthResponse {
            [JsonProperty(@"access_token")]
            public string AccessToken;

            [JsonProperty(@"token_type")]
            public string TokenType;

            [JsonProperty(@"expires_in")]
            public double ExpiresIn;
        }
#pragma warning restore 0649

        private AuthResponse _authToken;
        private const string KeyAuthToken = "token";
        private const string KeyMuteUpload = "muteUpload";

        public override async Task ResetAsync(CancellationToken cancellation) {
            /*if (_authToken != null) {
                Request.Post("https://oauth.yandex.com/revoke", null, _authToken.AccessToken, cancellation: cancellation).Ignore();
            }*/

            await base.ResetAsync(cancellation);
            _authToken = null;
            Storage.Remove(KeyAuthToken);
        }

        public override Task PrepareAsync(CancellationToken cancellation) {
            if (!IsReady) {
                var enc = Storage.GetEncrypted<string>(KeyAuthToken);
                if (enc != null) {
                    try {
                        _authToken = JsonConvert.DeserializeObject<AuthResponse>(enc);
                        IsReady = true;
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                }
            }
            return Task.Delay(0);
        }

        public override async Task SignInAsync(CancellationToken cancellation) {
            await PrepareAsync(cancellation);
            if (IsReady) return;

            var data = InternalUtils.GetYandexDiskAppFolderCredentials();
            var clientId = data.Item1.Substring(2);
            var clientSecret = data.Item2.Substring(2);

            var code = await GetAuthenticationCode(clientId, cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (code == null) {
                throw new UserCancelledException();
            }

            var response = await Request.Post<AuthResponse>(@"https://oauth.yandex.com/token", new NameValueCollection {
                { @"code", code.Code },
                { @"redirect_uri", code.RedirectUri },
                { @"grant_type", @"authorization_code" }
            }, null, extraHeaders: new NameValueCollection {
                ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + clientSecret))
            }, cancellation: cancellation);
            if (cancellation.IsCancellationRequested) return;

            _authToken = response ?? throw new Exception(ToolsStrings.Uploader_CannotFinishAuthorization);
            Storage.SetEncrypted(KeyAuthToken, JsonConvert.SerializeObject(_authToken));
            IsReady = true;
        }

        public override async Task<DirectoryEntry[]> GetDirectoriesAsync(CancellationToken cancellation) {
            await PrepareAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();

            if (_authToken == null) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            var list = await Request.Get<JObject>($"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString("app:/")}&" +
                    "fields=name,path,type,_embedded.items.name,_embedded.items.path,_embedded.items.type",
                    _authToken.AccessToken);
            if (list == null) {
                throw new Exception(ToolsStrings.Uploader_RequestFailed);
            }

            return new[] {
                new DirectoryEntry {
                    Id = null,
                    DisplayName = "App folder",
                    Children = (list["_embedded"]?["items"] as JArray)?.OfType<JObject>()
                                                                       .Where(x => x.GetStringValueOnly("type") == "dir").Select(
                                                                               x => new DirectoryEntry {
                                                                                   Id = x.GetStringValueOnly("path"),
                                                                                   DisplayName = x.GetStringValueOnly("name")
                                                                               }).Where(x => x.Id != null && x.DisplayName != null).ToArray()
                },
            };
        }

        private bool? _muteUpload;

        public bool MuteUpload {
            get => _muteUpload ?? (_muteUpload = ValuesStorage.Get<bool>(KeyMuteUpload)).Value;
            set {
                if (Equals(value, MuteUpload)) return;
                _muteUpload = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyMuteUpload, value);
            }
        }

#pragma warning disable 0649
        internal class UploadStartResult {
            [JsonProperty(@"href")]
            public string Href;

            [JsonProperty(@"method")]
            public string Method;

            [JsonProperty(@"templated")]
            public bool Templated;
        }

        internal class InformationResult {
            [JsonProperty(@"path")]
            public string Path;

            [JsonProperty(@"name")]
            public string Name;

            [JsonProperty(@"public_url")]
            public string PublicUrl;
        }
#pragma warning restore 0649

        public override async Task<UploadResult> UploadAsync(string name, string originalName, string mimeType, string description, Stream data, UploadAs uploadAs,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            await PrepareAsync(cancellation);

            if (_authToken == null) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            var path = $"{DestinationDirectoryId ?? "app:"}/{name}";
            var bytes = await data.ReadAsBytesAsync();
            cancellation.ThrowIfCancellationRequested();
            var firstPart = await Request.Get<UploadStartResult>(
                    $@"https://cloud-api.yandex.net/v1/disk/resources/upload?path={Uri.EscapeDataString(path)}&overwrite=1",
                    _authToken.AccessToken, cancellation: cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (firstPart == null) {
                RaiseUploadFailedException();
            }

            await Request.Send(firstPart.Method, firstPart.Href, bytes, _authToken.AccessToken, progress, cancellation, null);

            await Request.Put<UploadStartResult>($@"https://cloud-api.yandex.net/v1/disk/resources/publish?path={Uri.EscapeDataString(path)}",
                    null, _authToken.AccessToken, cancellation: cancellation);
            cancellation.ThrowIfCancellationRequested();

            var url = (await Request.Get<InformationResult>($@"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(path)}",
                    _authToken.AccessToken, null, cancellation))?.PublicUrl;
            cancellation.ThrowIfCancellationRequested();
            if (url == null) {
                RaiseShareFailedException();
            }

            Logging.Debug(url);
            var id = Regex.Match(url, @"/d/(.+)");
            return id.Success ?
                    new UploadResult { Id = $"{(uploadAs == UploadAs.Content ? "Yi" : "RY")}{id.Groups[1].Value}", DirectUrl = url } :
                    WrapUrl(url, uploadAs);
        }
    }
}