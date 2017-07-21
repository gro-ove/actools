using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls.WebParts;
using AcManager.Internal;
using AcManager.Tools;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.LargeFilesSharing.Implementations {
    public class DropboxUploader : FileUploaderBase {
        public DropboxUploader(IStorage storage) : base(storage, "Dropbox (App Folder)",
                "2 GB of space by default, allows to use direct links, so CM can download files easily. This version uploads to app’s folder.", true, false) { }

        private static Task<OAuthCode> GetAuthenticationCode(string clientId, CancellationToken cancellation) {
            return OAuth.GetCode("Dropbox",
                    $"https://www.dropbox.com/oauth2/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}", null, cancellation: cancellation);
        }

#pragma warning disable 0649
        internal class AuthResponse {
            // {"access_token": "ABCDEFG", "token_type": "bearer", "account_id": "dbid:AAH4f99T0taONIb-OurWxbNQ6ywGRopQngc", "uid": "12345"}

            [JsonProperty(@"access_token")]
            public string AccessToken;

            [JsonProperty(@"account_id")]
            public string AccountId;

            [JsonProperty(@"uid")]
            public string Uid;

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
#pragma warning restore 0649

        private AuthResponse _authToken;
        private const string KeyAuthToken = "token";
        private const string KeyMuteUpload = "muteUpload";

        public override async Task ResetAsync(CancellationToken cancellation) {
            if (_authToken != null) {
                await Request.Post("https://api.dropboxapi.com/2/auth/token/revoke", null, _authToken.AccessToken, cancellation: cancellation);
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
                        Logging.Warning(enc);
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

#pragma warning disable 0649
        internal class SearchResult {
            [JsonProperty(@"items")]
            public SearchResultFile[] Items;
        }

        internal class SearchResultFile {
            [JsonProperty(@"id")]
            public string Id;

            [JsonProperty(@"title")]
            public string Title;

            [JsonProperty(@"parents")]
            public SearchResultParent[] Parents;
        }

        internal class SearchResultParent {
            [JsonProperty(@"id")]
            public string Id;

            [JsonProperty(@"isRoot")]
            public bool IsRoot;
        }
#pragma warning restore 0649

        public override async Task<DirectoryEntry[]> GetDirectoriesAsync(CancellationToken cancellation) {
            await PrepareAsync(cancellation);

            if (_authToken == null) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            const string query = "mimeType='application/vnd.google-apps.folder' and trashed = false and 'me' in writers";
            const string fields = "items(id,parents(id,isRoot),title)";
            var data = await Request.Get<SearchResult>(
                    @"https://www.googleapis.com/drive/v2/files?maxResults=1000&orderBy=title&" +
                            $"q={HttpUtility.UrlEncode(query)}&fields={HttpUtility.UrlEncode(fields)}",
                    _authToken.AccessToken, cancellation: cancellation);

            if (data == null) {
                throw new Exception(ToolsStrings.Uploader_RequestFailed);
            }

            var directories = data.Items.Select(x => new DirectoryEntry {
                Id = x.Id,
                DisplayName = x.Title
            }).ToList();

            foreach (var directory in directories) {
                directory.Children = data.Items.Where(x => x.Parents.Any(y => !y.IsRoot && y.Id == directory.Id))
                                         .Select(x => directories.GetById(x.Id)).ToArray();
            }

            return new [] {
                new DirectoryEntry {
                    Id = null,
                    DisplayName = ToolsStrings.Uploader_RootDirectory,
                    Children = data.Items.Where(x => x.Parents.All(y => y.IsRoot)).Select(x => directories.GetById(x.Id)).ToArray()
                }
            };
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

            var ended = false;
            var total = data.Length;
            var buffer = new byte[Math.Min(total, 50 * 1024 * 1024)];
            var uploaded = 0;

            async Task<int> NextPiece() {
                if (ended) return 0;
                var piece = await data.ReadAsync(buffer, 0, buffer.Length);
                cancellation.ThrowIfCancellationRequested();
                ended = piece == 0 || uploaded + piece == total;
                return piece;
            }

            progress.Report("Starting upload session…", 0.00001d);
            var initialPiece = await NextPiece();
            var session = await Request.Post<SessionOpenedResult>(@"https://content.dropboxapi.com/2/files/upload_session/start",
                    buffer.Slice(0, initialPiece), _authToken.AccessToken,
                    progress.Subrange((double)uploaded / total, (double)initialPiece / total, $"Uploading piece #1 ({{0}})…"), cancellation,
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
                            progress.Subrange((double)uploaded / total, (double)piece / total, $"Uploading piece #{index} ({{0}})…"), cancellation,
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
                            cancellation: cancellation, extraHeaders: new NameValueCollection {
                                ["Dropbox-API-Arg"] = JsonConvert.SerializeObject(new StartParams { Close = true })
                            }).Ignore();
                } catch {
                    // ignored
                }

                throw;
            }

            progress.Report("Finishing upload session…", 0.999999d);
            var result = await Request.Post<UploadFinishResult>(@"https://content.dropboxapi.com/2/files/upload_session/finish",
                    new byte[0],
                    _authToken.AccessToken, null, cancellation, new NameValueCollection {
                        ["Dropbox-API-Arg"] = JsonConvert.SerializeObject(new UploadFinishParams {
                            Cursor = new CursorParams { Offset = uploaded, SessionId = session.SessionId },
                            Commit = new CommitParams { Path = "/" + name, Mute = MuteUpload }
                        })
                    });
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
            var id = Regex.Match(url, @"/s/(\w+)");
            return id.Success ?
                    new UploadResult { Id = $"{(uploadAs == UploadAs.Content ? "Bi" : "RB")}{id.Groups[1].Value}", DirectUrl = url } :
                    WrapUrl(url, uploadAs);
        }
    }
}