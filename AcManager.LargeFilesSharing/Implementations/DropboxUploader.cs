using System;
using System.Collections.Specialized;
using System.IO;
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
using Newtonsoft.Json;

namespace AcManager.LargeFilesSharing.Implementations {
    public class DropboxUploader : FileUploaderBase {
        public DropboxUploader(IStorage storage) : base(storage, "Dropbox (App Folder)",
                new Uri("/AcManager.LargeFilesSharing;component/Assets/Icons/Dropbox.png", UriKind.Relative),
                "2 GB of space by default, allows to use direct links, so CM can download files easily. This version uploads to app’s folder.", true, false) { }

        private static Task<OAuthCode> GetAuthenticationCode(string clientId, CancellationToken cancellation) {
            return OAuth.GetCode("Dropbox",
                    $"https://www.dropbox.com/oauth2/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}", null, cancellation: cancellation);
        }

#pragma warning disable 0649
        internal class AuthResponse {
            [JsonProperty(@"access_token")]
            public string AccessToken;

            [JsonProperty(@"account_id")]
            public string AccountId;

            [JsonProperty(@"uid")]
            public string Uid;

            [JsonProperty(@"token_type")]
            public string TokenType;
        }
#pragma warning restore 0649

        private AuthResponse _authToken;
        private const string KeyAuthToken = "token";
        private const string KeyMuteUpload = "muteUpload";

        public override async Task ResetAsync(CancellationToken cancellation) {
            if (_authToken != null) {
                Request.Post("https://api.dropboxapi.com/2/auth/token/revoke", null, _authToken.AccessToken, cancellation: cancellation).Ignore();
            }

            await base.ResetAsync(cancellation);
            _authToken = null;
            Storage.Remove(KeyAuthToken);
        }

        public override Task PrepareAsync(CancellationToken cancellation) {
            if (!IsReady) {
                var enc = Storage.GetEncryptedString(KeyAuthToken);
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

            _authToken = response ?? throw new Exception(ToolsStrings.Uploader_CannotFinishAuthorization);
            Storage.SetEncrypted(KeyAuthToken, JsonConvert.SerializeObject(_authToken));
            IsReady = true;
        }

        public override Task<DirectoryEntry[]> GetDirectoriesAsync(CancellationToken cancellation) {
            throw new NotSupportedException();
        }

        private bool? _muteUpload;

        public bool MuteUpload {
            get => _muteUpload ?? (_muteUpload = ValuesStorage.GetBool(KeyMuteUpload)).Value;
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
            await PrepareAsync(cancellation);

            if (_authToken == null) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            var commitParams = new CommitParams { Path = "/" + name, Mute = MuteUpload };
            var total = data.Length;
            var bufferSize = Math.Min(total, 50 * 1024 * 1024);
            var totalPieces = ((double)total / bufferSize).Ceiling();

            UploadFinishResult result;
            if (totalPieces == 1) {
                var bytes = await data.ReadAsBytesAsync();
                cancellation.ThrowIfCancellationRequested();
                result = await Request.Post<UploadFinishResult>(@"https://content.dropboxapi.com/2/files/upload", bytes,
                        _authToken.AccessToken, progress, cancellation, new NameValueCollection {
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
                        buffer.Slice(0, initialPiece), _authToken.AccessToken,
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
                        await Request.Post(@"https://content.dropboxapi.com/2/files/upload_session/append_v2", buffer.Slice(0, piece), _authToken.AccessToken,
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
                        new byte[0], _authToken.AccessToken, null, cancellation, new NameValueCollection {
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
                    }, _authToken.AccessToken, cancellation: cancellation))?.Url;
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