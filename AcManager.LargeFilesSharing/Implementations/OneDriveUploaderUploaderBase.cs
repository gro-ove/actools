using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.LargeFilesSharing.Implementations {
    public abstract class OneDriveUploaderUploaderBase : FileUploaderBase {
        protected OneDriveUploaderUploaderBase(IStorage storage, string name, string description, bool supportsSigning, bool supportsDirectories) :
                base(storage, name,
                        new Uri("/AcManager.LargeFilesSharing;component/Assets/Icons/OneDrive.png", UriKind.Relative),
                        description, supportsSigning, supportsDirectories) {
            Request.AuthorizationTokenType = "bearer";
        }

        protected string[] Scopes;
        private const string KeyAuthToken = "token";
        private const string KeyAuthExpiration = "expiration";

        private Task<OAuthCode> GetAuthenticationCode(string clientId, CancellationToken cancellation) {
            return OAuth.GetCode("OneDrive", $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?" +
                    $"scope={Uri.EscapeDataString(Scopes.JoinToString(' '))}&" +
                    $"response_type=code&client_id={Uri.EscapeDataString(clientId)}", null, cancellation: cancellation,
                    successCodeRegex: null);
        }

#pragma warning disable 649
        internal class AuthResponse {
            [JsonProperty(@"access_token")]
            public string AccessToken;

            [JsonProperty(@"refresh_token")]
            public string RefreshToken;

            [JsonProperty(@"expires_in")]
            public int ExpiresIn;

            [JsonProperty(@"token_type")]
            public string TokenType;
        }

        internal class RefreshResponse {
            [JsonProperty(@"access_token")]
            public string AccessToken;

            [JsonProperty(@"expires_in")]
            public int ExpiresIn;

            [JsonProperty(@"token_type")]
            public string TokenType;
        }
#pragma warning restore 649

        private AuthResponse _authToken;
        private DateTime _authExpiration;

        public override async Task ResetAsync(CancellationToken cancellation) {
            await base.ResetAsync(cancellation);
            _authToken = null;
            _authExpiration = default;
            Storage.Remove(KeyAuthToken);
            Storage.Remove(KeyAuthExpiration);
        }

        protected abstract Tuple<string, string> GetCredentials();

        public override async Task PrepareAsync(CancellationToken cancellation) {
            if (IsReady && DateTime.Now < _authExpiration) return;

            var data = GetCredentials();
            var clientId = data.Item1;
            var clientSecret = data.Item2.Substring(2);

            var enc = Storage.GetEncrypted<string>(KeyAuthToken);
            if (enc == null) return;

            try {
                _authToken = JsonConvert.DeserializeObject<AuthResponse>(enc);
                _authExpiration = Storage.Get(KeyAuthExpiration, default(DateTime));
            } catch (Exception) {
                Logging.Warning("Can’t load auth token");
                return;
            }

            if (DateTime.Now < _authExpiration) {
                IsReady = true;
                return;
            }

            RefreshResponse refresh = null;
            try {
                refresh = await Request.Post<RefreshResponse>(@"https://login.microsoftonline.com/common/oauth2/v2.0/token", new NameValueCollection {
                    { @"client_id", clientId },
                    { @"client_secret", clientSecret },
                    { @"refresh_token", _authToken.RefreshToken },
                    { @"grant_type", @"refresh_token" }
                }, null, cancellation: cancellation);
            } catch (Exception e) {
                Logging.Warning(e);
            }
            cancellation.ThrowIfCancellationRequested();

            if (refresh == null) {
                Storage.Remove(KeyAuthToken);
            } else {
                _authToken.AccessToken = refresh.AccessToken;
                _authExpiration = DateTime.Now + TimeSpan.FromSeconds(refresh.ExpiresIn) - TimeSpan.FromSeconds(20);
                Storage.SetEncrypted(KeyAuthToken, JsonConvert.SerializeObject(_authToken));
                Storage.Set(KeyAuthExpiration, _authExpiration);
                IsReady = true;
            }
        }

        public override async Task SignInAsync(CancellationToken cancellation) {
            await PrepareAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (IsReady && DateTime.Now < _authExpiration) return;

            var data = GetCredentials();
            var clientId = data.Item1;
            var clientSecret = data.Item2.Substring(2);

            var code = await GetAuthenticationCode(clientId, cancellation);
            cancellation.ThrowIfCancellationRequested();

            if (code == null) {
                throw new UserCancelledException();
            }

            var response = await Request.Post<AuthResponse>(@"https://login.microsoftonline.com/common/oauth2/v2.0/token", new NameValueCollection {
                { @"code", code.Code },
                { @"client_id", clientId },
                { @"client_secret", clientSecret },
                { @"redirect_uri", code.RedirectUri },
                { @"grant_type", @"authorization_code" }
            }, null, cancellation: cancellation);
            cancellation.ThrowIfCancellationRequested();

            _authToken = response ?? throw new Exception(ToolsStrings.Uploader_CannotFinishAuthorization);
            _authExpiration = DateTime.Now + TimeSpan.FromSeconds(response.ExpiresIn) - TimeSpan.FromSeconds(20);
            Storage.SetEncrypted(KeyAuthToken, JsonConvert.SerializeObject(_authToken));
            Storage.Set(KeyAuthExpiration, _authExpiration);
            IsReady = true;
        }

        public override async Task<DirectoryEntry[]> GetDirectoriesAsync(CancellationToken cancellation) {
            await PrepareAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();

            if (_authToken == null) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            var list = (await Request.Get<JObject>("https://graph.microsoft.com/v1.0/drive/special/approot/children?filter=folder%20ne%20null",
                    _authToken.AccessToken, cancellation: cancellation))?["value"] as JArray;
            cancellation.ThrowIfCancellationRequested();
            if (list == null) {
                throw new Exception(ToolsStrings.Uploader_RequestFailed);
            }

            return new[] {
                new DirectoryEntry {
                    Id = null,
                    DisplayName = ToolsStrings.Uploader_RootDirectory,
                    Children = list.OfType<JObject>().Select(x => new DirectoryEntry {
                        Id = x.GetStringValueOnly("id"),
                        DisplayName = x.GetStringValueOnly("name")
                    }).Where(x => x.Id != null && x.DisplayName != null).ToArray()
                }
            };
        }

        public override async Task<UploadResult> UploadAsync(string name, string originalName, string mimeType, string description, Stream data,
                UploadAs uploadAs,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            await PrepareAsync(cancellation);

            if (_authToken == null) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            var path = DestinationDirectoryId == null ? $"drive/special/approot:/{name}" : $"drive/items/{DestinationDirectoryId}:/{name}";
            var total = data.Length;
            var bufferSize = Math.Min(total, 10 * 320 * 1024);
            var totalPieces = ((double)total / bufferSize).Ceiling();

            JObject result = null;
            if (totalPieces == 1) {
                // https://graph.microsoft.com/v1.0/drive/root:/3ds%20Max/Newtonsoft.Json.xml:/content
                var bytes = await data.ReadAsBytesAsync();
                cancellation.ThrowIfCancellationRequested();
                result = await Request.Put<JObject>(
                        $@"https://graph.microsoft.com/v1.0/{path}:/content?@name.conflictBehavior=rename",
                        bytes, _authToken.AccessToken, progress, cancellation);
            } else {
                var ended = false;
                var buffer = new byte[bufferSize];
                var uploaded = 0;

                async Task<int> NextPiece() {
                    if (ended) return 0;
                    var piece = await data.ReadAsync(buffer, 0, buffer.Length);
                    cancellation.ThrowIfCancellationRequested();

                    // ReSharper disable once AccessToModifiedClosure
                    ended = piece == 0 || uploaded + piece == total;
                    return piece;
                }

                progress.Report("Starting upload session…", 0.00001d);
                Logging.Debug(path);

                var uploadUrl = (await Request.Post<JObject>(
                        $@"https://graph.microsoft.com/v1.0/{path}:/createUploadSession",
                        new JObject {
                            ["item"] = new JObject {
                                ["@microsoft.graph.conflictBehavior"] = "rename",
                                ["name"] = name,
                            }
                        }, _authToken.AccessToken, null, cancellation))?.GetStringValueOnly("uploadUrl");

                try {
                    cancellation.ThrowIfCancellationRequested();
                    if (uploadUrl == null) {
                        RaiseShareFailedException();
                    }

                    for (var index = 1; index < 100000 && !ended; index++) {
                        var rangeFrom = uploaded;
                        var piece = await NextPiece();
                        result = await Request.Put<JObject>(uploadUrl, buffer.Slice(0, piece), null,
                                progress.Subrange((double)uploaded / total, (double)piece / total, $"Uploading piece #{index} out of {totalPieces} ({{0}})…"),
                                cancellation,
                                new NameValueCollection {
                                    ["Content-Range"] = $@"bytes {rangeFrom}-{rangeFrom + piece - 1}/{total}"
                                });
                        uploaded += piece;
                        cancellation.ThrowIfCancellationRequested();
                    }
                } catch (Exception) when (uploadUrl != null) {
                    try {
                        Request.Send("DELETE", uploadUrl, new byte[0], null, null, default, null).Ignore();
                    } catch {
                        // ignored
                    }

                    throw;
                }
            }

            cancellation.ThrowIfCancellationRequested();
            if (result?["id"] == null) {
                RaiseUploadFailedException();
            }

            var link = (await Request.Post<JObject>($"https://graph.microsoft.com/v1.0/drive/items/{result["id"]}/createLink", new {
                type = "view"
            }, _authToken.AccessToken, null, cancellation))?["link"] as JObject;
            cancellation.ThrowIfCancellationRequested();
            if (link == null) {
                RaiseShareFailedException();
            }

#if DEBUG
            Logging.Debug(link.ToString(Formatting.Indented));
#endif

            return WrapUrl((string)link["webUrl"], uploadAs);
        }
    }
}