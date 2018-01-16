using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AcManager.Internal;
using AcManager.Tools;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.LargeFilesSharing.Implementations {
    public class GoogleDriveUploader : GoogleDriveUploaderBase {
        public GoogleDriveUploader(IStorage storage) : base(storage, ToolsStrings.Uploader_GoogleDrive,
                "15 GB of space, but sadly without any API to download shared files, so CM might break any moment. This version requires an access to all files, but allows to select destination.",
                true, true) {
            Scopes = new[] { @"https://www.googleapis.com/auth/drive", @"https://www.googleapis.com/auth/drive.file" };
        }

        protected override Tuple<string, string> GetCredentials() {
            return InternalUtils.GetGoogleDriveCredentials();
        }
    }

    public class GoogleDriveAppFolderUploader : GoogleDriveUploaderBase {
        public GoogleDriveAppFolderUploader(IStorage storage) : base(storage, "Google Drive (Upload To Root)",
                "15 GB of space, but sadly without any API to download shared files, so CM might break any moment. This version do not require an access to all files, but uploads files only to root directory.",
                true, false) {
            Scopes = new[] { @"https://www.googleapis.com/auth/drive.file" };
        }

        protected override Tuple<string, string> GetCredentials() {
            return InternalUtils.GetGoogleDriveAppFolderCredentials();
        }
    }

    public abstract class GoogleDriveUploaderBase : FileUploaderBase {
        protected GoogleDriveUploaderBase(IStorage storage, string name, string description, bool supportsSigning, bool supportsDirectories) :
                base(storage, name,
                        new Uri("/AcManager.LargeFilesSharing;component/Assets/Icons/GoogleDrive.png", UriKind.Relative),
                        description, supportsSigning, supportsDirectories) { }

        private const string RedirectUrl = "urn:ietf:wg:oauth:2.0:oob";

        protected string[] Scopes;
        private const string KeyAuthToken = "token";
        private const string KeyAuthExpiration = "expiration";

        private Task<OAuthCode> GetAuthenticationCode(string clientId, CancellationToken cancellation) {
            return OAuth.GetCode("Google Drive", $"https://accounts.google.com/o/oauth2/v2/auth?scope={Uri.EscapeDataString(Scopes.JoinToString(' '))}&" +
                    $"response_type=code&client_id={Uri.EscapeDataString(clientId)}", RedirectUrl, cancellation: cancellation);
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
            _authExpiration = default(DateTime);
            Storage.Remove(KeyAuthToken);
            Storage.Remove(KeyAuthExpiration);
        }

        protected abstract Tuple<string, string> GetCredentials();

        public override async Task PrepareAsync(CancellationToken cancellation) {
            if (IsReady && DateTime.Now < _authExpiration) return;

            var data = GetCredentials();
            var clientId = data.Item1.Substring(2);
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

            var refresh = await Request.Post<RefreshResponse>(@"https://www.googleapis.com/oauth2/v4/token", new NameValueCollection {
                { @"client_id", clientId },
                { @"client_secret", clientSecret },
                { @"refresh_token", _authToken.RefreshToken },
                { @"grant_type", @"refresh_token" }
            }, null, cancellation: cancellation);
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
            var clientId = data.Item1.Substring(2);
            var clientSecret = data.Item2.Substring(2);

            var code = await GetAuthenticationCode(clientId, cancellation);
#if DEBUG
            Logging.Debug(code);
#endif
            cancellation.ThrowIfCancellationRequested();

            if (code == null) {
                throw new UserCancelledException();
            }

            var response = await Request.Post<AuthResponse>(@"https://www.googleapis.com/oauth2/v4/token", new NameValueCollection {
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
            cancellation.ThrowIfCancellationRequested();

            if (_authToken == null) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            const string query = "mimeType='application/vnd.google-apps.folder' and trashed = false and 'me' in writers";
            const string fields = "items(id,parents(id,isRoot),title)";
            var data = await Request.Get<SearchResult>(
                    @"https://www.googleapis.com/drive/v2/files?maxResults=1000&orderBy=title&" +
                            $"q={HttpUtility.UrlEncode(query)}&fields={HttpUtility.UrlEncode(fields)}",
                    _authToken.AccessToken, cancellation: cancellation);
            cancellation.ThrowIfCancellationRequested();

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

            return new[] {
                new DirectoryEntry {
                    Id = null,
                    DisplayName = ToolsStrings.Uploader_RootDirectory,
                    Children = data.Items.Where(x => x.Parents.All(y => y.IsRoot)).Select(x => directories.GetById(x.Id)).ToArray()
                }
            };
        }

#pragma warning disable 0649
        internal class InsertParams {
            [JsonProperty(@"title")]
            public string Title;

            [JsonProperty(@"originalFilename")]
            public string OriginalFilename;

            [JsonProperty(@"parents")]
            public InsertParamsParent[] ParentIds;

            [JsonProperty(@"description")]
            public string Description;

            [JsonProperty(@"mimeType")]
            public string MimeType;
        }

        internal class InsertParamsParent {
            [JsonProperty(@"kind")]
            public string Kind = @"drive#file";

            [JsonProperty(@"id")]
            public string Id;
        }

        internal class InsertResult {
            [JsonProperty(@"id")]
            public string Id;
        }

        internal class PermissionResult {
            [JsonProperty(@"role")]
            public string Role;

            [JsonProperty(@"type")]
            public string Type;
        }
#pragma warning restore 0649

        public override async Task<UploadResult> UploadAsync(string name, string originalName, string mimeType, string description, Stream data, UploadAs uploadAs,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            await PrepareAsync(cancellation);

            if (_authToken == null) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            var bytes = await data.ReadAsBytesAsync();
            cancellation.ThrowIfCancellationRequested();

            var entry = await Request.PostMultipart<InsertResult>(@"https://www.googleapis.com/upload/drive/v2/files?uploadType=multipart",
                    new InsertParams {
                        Title = name,
                        OriginalFilename = originalName,
                        Description = description,
                        MimeType = mimeType,
                        ParentIds = DestinationDirectoryId == null ? null : new[] {
                            new InsertParamsParent { Id = DestinationDirectoryId }
                        }
                    },
                    _authToken.AccessToken,
                    bytes,
                    mimeType,
                    progress,
                    cancellation);
            cancellation.ThrowIfCancellationRequested();

            if (entry == null) {
                RaiseUploadFailedException();
            }

            var shared = await Request.Post<PermissionResult>($"https://www.googleapis.com/drive/v2/files/fileId/permissions?fileId={entry.Id}",
                    new { role = @"reader", type = @"anyone" }, _authToken.AccessToken, cancellation: cancellation);
            cancellation.ThrowIfCancellationRequested();

            if (shared == null) {
                RaiseShareFailedException();
            }

            return new UploadResult {
                Id = $"{(uploadAs == UploadAs.Content ? "Gi" : "RG")}{entry.Id}",
                DirectUrl = $"https://drive.google.com/uc?export=download&id={entry.Id}"
            };
        }
    }
}