using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.LargeFilesSharing.Implementations {
    [UsedImplicitly]
    public class DropboxUploader : FileUploaderBase {
        public DropboxUploader(IStorage storage) : base(storage, "Dropbox (App Folder)",
                new Uri("/AcManager.LargeFilesSharing;component/Assets/Icons/Dropbox.png", UriKind.Relative),
                "2 GB of space by default, allows to use direct links, so CM can download files easily. This version uploads to app’s folder.", true, false) { }

        private static Task<OAuthCode> GetAuthenticationCode(string clientId, CancellationToken cancellation) {
            return OAuth.GetCode("Dropbox",
                    $"https://www.dropbox.com/oauth2/authorize?response_type=code&token_access_type=offline&client_id={Uri.EscapeDataString(clientId)}",
                    null, cancellation: cancellation);
        }

#pragma warning disable 0649
        internal class AuthResponse {
            [JsonProperty(@"access_token")]
            public string AccessToken;

            [JsonProperty(@"refresh_token")]
            public string RefreshToken;

            [JsonProperty(@"expires_in")]
            public int ExpiresIn;

            [JsonProperty(@"account_id")]
            public string AccountId;

            [JsonProperty(@"uid")]
            public string Uid;

            [JsonProperty(@"token_type")]
            public string TokenType;
        }
#pragma warning restore 0649

        private AuthResponse _authToken;
        private DateTime _authExpiration;
        private const string KeyAuthToken = "token";
        private const string KeyAuthExpiration = "expiration";
        private const string KeyMuteUpload = "muteUpload";

        public override async Task ResetAsync(CancellationToken cancellation) {
            if (_authToken != null) {
                Request.Post("https://api.dropboxapi.com/2/auth/token/revoke", null, _authToken.AccessToken, cancellation: cancellation).Ignore();
            }

            await base.ResetAsync(cancellation);
            _authToken = null;
            _authExpiration = default;
            Storage.Remove(KeyAuthToken);
            Storage.Remove(KeyAuthExpiration);
        }

        private void SetSession(AuthResponse token) {
            _authToken = token;
            _authExpiration = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn) - TimeSpan.FromSeconds(20);
            Storage.SetEncrypted(KeyAuthToken, JsonConvert.SerializeObject(token));
            Storage.Set(KeyAuthExpiration, _authExpiration);
            IsReady = true;
        }

        public override async Task PrepareAsync(CancellationToken cancellation) {
            if (IsReady && DateTime.Now < _authExpiration) return;

            // From here on, either session gets renewed, or it gets dropped completely so user could sign in again: keeping
            // an expired access token around with IsReady set would turn every following request into 401 with no way out.
            AuthResponse stored = null;
            try {
                var enc = Storage.GetEncrypted<string>(KeyAuthToken);
                stored = enc == null ? null : JsonConvert.DeserializeObject<AuthResponse>(enc);
            } catch (Exception e) {
                Logging.Warning(e);
            }

            // Tokens saved before refresh tokens were requested can’t be renewed, so those need a new sign in once
            if (stored?.RefreshToken == null) {
                await ResetAsync(cancellation);
                return;
            }

            _authToken = stored;
            _authExpiration = Storage.Get(KeyAuthExpiration, default(DateTime));
            if (DateTime.Now < _authExpiration) {
                IsReady = true;
                return;
            }

            var data = InternalUtils.GetDropboxCredentials();
            AuthResponse refresh = null;
            try {
                refresh = await Request.Post<AuthResponse>(@"https://api.dropboxapi.com/oauth2/token", new NameValueCollection {
                    { @"client_id", data.Item1.Substring(2) },
                    { @"client_secret", data.Item2.Substring(2) },
                    { @"refresh_token", _authToken.RefreshToken },
                    { @"grant_type", @"refresh_token" }
                }, null, cancellation: cancellation);
            } catch (ApiException e) when (e.Status == HttpStatusCode.BadRequest || e.Status == HttpStatusCode.Unauthorized) {
                // Refresh token is rejected (revoked or app access removed), only a new sign in would help. Anything else (connection
                // issues, server errors) is thrown further with stored token intact, so the next attempt could succeed.
                Logging.Warning(e);
            }
            cancellation.ThrowIfCancellationRequested();

            if (refresh?.AccessToken == null) {
                await ResetAsync(cancellation);
                return;
            }

            // Dropbox doesn’t return a new refresh token, the original one stays valid until revoked
            refresh.RefreshToken = refresh.RefreshToken ?? _authToken.RefreshToken;
            SetSession(refresh);
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken cancellation) {
            await PrepareAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();

            if (_authToken == null) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            return _authToken.AccessToken;
        }

        public override async Task SignInAsync(CancellationToken cancellation) {
            await PrepareAsync(cancellation);
            if (IsReady) return;

            var data = InternalUtils.GetDropboxCredentials();
            var clientId = data.Item1.Substring(2);
            var clientSecret = data.Item2.Substring(2);

            var code = await GetAuthenticationCode(clientId, cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (code == null) {
                throw new UserCancelledException();
            }

            var response = await Request.Post<AuthResponse>(@"https://api.dropboxapi.com/oauth2/token", new NameValueCollection {
                { @"code", code.Code },
                { @"client_id", clientId },
                { @"client_secret", clientSecret },
                { @"redirect_uri", code.RedirectUri },
                { @"grant_type", @"authorization_code" }
            }, null, cancellation: cancellation);
            if (cancellation.IsCancellationRequested) return;

            SetSession(response ?? throw new Exception(ToolsStrings.Uploader_CannotFinishAuthorization));
        }

        public override Task<DirectoryEntry[]> GetDirectoriesAsync(CancellationToken cancellation) {
            throw new NotSupportedException();
        }

        private bool? _muteUpload;

        public bool MuteUpload {
            get => _muteUpload ?? (_muteUpload = ValuesStorage.Get(KeyMuteUpload, false)).Value;
            set {
                if (Equals(value, MuteUpload)) return;
                _muteUpload = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeyMuteUpload, value);
            }
        }

#pragma warning disable 0649
        internal class SessionOpenedResult {
            [JsonProperty(@"session_id")]
            public string SessionId;
        }

        internal class CursorParams {
            [JsonProperty(@"session_id")]
            public string SessionId;

            [JsonProperty(@"offset")]
            public long Offset;
        }

        internal class CommitParams {
            [JsonProperty(@"path")]
            public string Path;

            [JsonProperty(@"mode")]
            public string Mode = "add";

            [JsonProperty(@"autorename")]
            public bool AutoRename = true;

            [JsonProperty(@"mute")]
            public bool Mute;
        }

        internal class StartParams {
            [JsonProperty(@"close")]
            public bool Close;
        }

        internal class UploadParams {
            [JsonProperty(@"cursor")]
            public CursorParams Cursor;

            [JsonProperty(@"close")]
            public bool Close;
        }

        internal class UploadFinishParams {
            [JsonProperty(@"cursor")]
            public CursorParams Cursor;

            [JsonProperty(@"commit")]
            public CommitParams Commit;
        }

        internal class UploadFinishResult {
            [JsonProperty(@"id")]
            public string Id;

            [JsonProperty(@"name")]
            public string Name;

            [JsonProperty(@"path_lower")]
            public string Path;
        }

        internal class ShareSettingsParams {
            [JsonProperty(@"requested_visibility")]
            public string Visibility = "public";
        }

        internal class ShareParams {
            [JsonProperty(@"path")]
            public string Path;

            [JsonProperty(@"settings")]
            public ShareSettingsParams Settings;
        }

        internal class ShareResult {
            [JsonProperty(@"url")]
            public string Url;

            [JsonProperty(@"name")]
            public string Name;

            [JsonProperty(@"path_lower")]
            public string Path;
        }
#pragma warning restore 0649

        public override async Task<UploadResult> UploadAsync(string name, string originalName, string mimeType, string description, Stream data, UploadAs uploadAs,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            // Dropbox access tokens only live for 4 hours, so each request below takes a current one: a large upload could outlive it
            await GetAccessTokenAsync(cancellation);
            var commitParams = new CommitParams { Path = "/" + name, Mute = MuteUpload };
            var total = data.Length;
            var bufferSize = Math.Min(total, 50 * 1024 * 1024);
            var totalPieces = ((double)total / bufferSize).Ceiling();

            UploadFinishResult result;
            if (totalPieces == 1) {
                var bytes = await data.ReadAsBytesAsync();
                cancellation.ThrowIfCancellationRequested();
                result = await Request.Post<UploadFinishResult>(@"https://content.dropboxapi.com/2/files/upload", bytes,
                        await GetAccessTokenAsync(cancellation), progress, cancellation, new NameValueCollection {
                            ["Dropbox-API-Arg"] = JsonConvert.SerializeObject(commitParams)
                        });
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
                var initialPiece = await NextPiece();
                var session = await Request.Post<SessionOpenedResult>(@"https://content.dropboxapi.com/2/files/upload_session/start",
                        buffer.Slice(0, initialPiece), await GetAccessTokenAsync(cancellation),
                        progress.Subrange((double)uploaded / total, (double)initialPiece / total, $"Uploading piece #1 out of {totalPieces} ({{0}})…"),
                        cancellation,
                        new NameValueCollection {
                            ["Dropbox-API-Arg"] = JsonConvert.SerializeObject(new StartParams())
                        });
                uploaded += initialPiece;
                Logging.Debug(session?.SessionId);

                try {
                    cancellation.ThrowIfCancellationRequested();
                    if (session == null) {
                        RaiseUploadFailedException("Upload session start failed.");
                    }

                    for (var index = 2; index < 10000 && !ended; index++) {
                        var piece = await NextPiece();
                        await Request.Post(@"https://content.dropboxapi.com/2/files/upload_session/append_v2", buffer.Slice(0, piece),
                                await GetAccessTokenAsync(cancellation),
                                progress.Subrange((double)uploaded / total, (double)piece / total, $"Uploading piece #{index} out of {totalPieces} ({{0}})…"),
                                cancellation,
                                new NameValueCollection {
                                    ["Dropbox-API-Arg"] = JsonConvert.SerializeObject(new UploadParams {
                                        Cursor = new CursorParams { Offset = uploaded, SessionId = session.SessionId }
                                    })
                                });
                        uploaded += piece;
                        cancellation.ThrowIfCancellationRequested();
                    }
                } catch (Exception) when (session != null) {
                    try {
                        Request.Post(@"https://content.dropboxapi.com/2/files/upload_session/start", new byte[0], _authToken.AccessToken,
                                extraHeaders: new NameValueCollection {
                                    ["Dropbox-API-Arg"] = JsonConvert.SerializeObject(new StartParams { Close = true })
                                }).Ignore();
                    } catch {
                        // ignored
                    }

                    throw;
                }

                progress.Report("Finishing upload session…", 0.999999d);
                result = await Request.Post<UploadFinishResult>(@"https://content.dropboxapi.com/2/files/upload_session/finish",
                        new byte[0], await GetAccessTokenAsync(cancellation), null, cancellation, new NameValueCollection {
                            ["Dropbox-API-Arg"] = JsonConvert.SerializeObject(new UploadFinishParams {
                                Cursor = new CursorParams { Offset = uploaded, SessionId = session.SessionId },
                                Commit = commitParams
                            })
                        });
            }

            cancellation.ThrowIfCancellationRequested();
            if (result == null) {
                RaiseUploadFailedException();
            }

            var url = (await Request.Post<ShareResult>(@"https://api.dropboxapi.com/2/sharing/create_shared_link_with_settings",
                    new ShareParams {
                        Path = result.Path,
                        Settings = new ShareSettingsParams()
                    }, await GetAccessTokenAsync(cancellation), cancellation: cancellation))?.Url;
            if (url == null) {
                RaiseShareFailedException();
            }

            Logging.Debug(url);
            var id = Regex.Match(url, @"/s/([\w_!-]+)");
            return id.Success ?
                    new UploadResult { Id = $"{(uploadAs == UploadAs.Content ? "Bi" : "RB")}{id.Groups[1].Value.ToCutBase64()}", DirectUrl = url } :
                    WrapUrl(url, uploadAs);
        }
    }
}