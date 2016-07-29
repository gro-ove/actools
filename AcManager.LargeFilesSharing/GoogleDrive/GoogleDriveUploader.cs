using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using AcManager.Controls.Dialogs;
using AcManager.Internal;
using AcManager.Tools.Data;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.LargeFilesSharing.GoogleDrive {
    internal class GoogleDriveUploader : FileUploaderBase {
        public GoogleDriveUploader() : base(Tools.ToolsStrings.Uploader_GoogleDrive, true, true) { }

        private const string RedirectUrl = "urn:ietf:wg:oauth:2.0:oob";

        private static readonly string[] Scopes = {
            @"https://www.googleapis.com/auth/drive",
            @"https://www.googleapis.com/auth/drive.file"
        };

        private async Task<string> GetAutenficationCode(string clientId, CancellationToken cancellation) {
            Process.Start($"https://accounts.google.com/o/oauth2/v2/auth?scope={Uri.EscapeDataString(Scopes.JoinToString(' '))}&" +
                    $"redirect_uri={RedirectUrl}&response_type=code&" +
                    $"client_id={clientId}");

            string code = null;
            var waiting = true;

            var handler = new EventHandler((sender, args) => {
                if (!waiting) return;
                waiting = false;

                // ReSharper disable once AccessToModifiedClosure
                code = Prompt.Show(Tools.ToolsStrings.Uploader_EnterGoogleDriveAuthenticationCode, Tools.ToolsStrings.Uploader_GoogleDrive, code);
            });

            Application.Current.MainWindow.Activated += handler;

            for (var i = 0; i < 500 && waiting; i++) {
                if (cancellation.IsCancellationRequested) {
                    Application.Current.MainWindow.Activated -= handler;
                    return null;
                }

                if (code == null) {
                    code = OpenWindowGetter.GetOpenWindows()
                                           .Select(x => x.Value)
                                           .FirstOrDefault(x => x.Contains(@"Success code="))?
                                           .Split(new[] { @"Success code=" }, StringSplitOptions.None)
                                           .ElementAtOrDefault(1)?
                                           .Split(' ')
                                           .FirstOrDefault();
                }

                await Task.Delay(200, cancellation);
            }

            Application.Current.MainWindow.Activated -= handler;
            return code;
        }

#pragma warning disable 0649
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
#pragma warning restore 0649

        private AuthResponse _authToken;
        private DateTime _authExpiration;
        private const string KeyAuthToken = "GD.at";
        private const string KeyAuthExpiration = "GD.ex";

        public override void Reset() {
            IsReady = false;
            _authToken = null;
            _authExpiration = default(DateTime);
            ValuesStorage.Remove(KeyAuthToken);
            ValuesStorage.Remove(KeyAuthExpiration);
        }

        public override async Task Prepare(CancellationToken cancellation) {
            if (IsReady && DateTime.Now < _authExpiration) return;

            var data = InternalUtils.GetGoogleDriveCredentials();
            var clientId = data.Item1.Substring(2);
            var clientSecret = data.Item2.Substring(2);

            var enc = ValuesStorage.GetEncryptedString(KeyAuthToken);
            if (enc == null) return;
            
            try {
                _authToken = JsonConvert.DeserializeObject<AuthResponse>(enc);
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
            }.GetQuery(), null, cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (refresh == null) {
                ValuesStorage.Remove(KeyAuthToken);
            } else {
                _authToken.AccessToken = refresh.AccessToken;
                _authExpiration = DateTime.Now + TimeSpan.FromSeconds(refresh.ExpiresIn) - TimeSpan.FromSeconds(20);
                ValuesStorage.SetEncrypted(KeyAuthToken, JsonConvert.SerializeObject(_authToken));
                ValuesStorage.Set(KeyAuthExpiration, _authExpiration);
                IsReady = true;
            }
        }

        public override async Task SignIn(CancellationToken cancellation) {
            await Prepare(cancellation);
            if (IsReady && DateTime.Now < _authExpiration) return;

            var data = InternalUtils.GetGoogleDriveCredentials();
            var clientId = data.Item1.Substring(2);
            var clientSecret = data.Item2.Substring(2);

            var code = await GetAutenficationCode(clientId, cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (code == null) {
                throw new UserCancelledException();
            }

            var response = await Request.Post<AuthResponse>(@"https://www.googleapis.com/oauth2/v4/token", new NameValueCollection {
                { @"code", code },
                { @"client_id", clientId },
                { @"client_secret", clientSecret },
                { @"redirect_uri", RedirectUrl },
                { @"grant_type", @"authorization_code" }
            }.GetQuery(), null, cancellation);
            if (cancellation.IsCancellationRequested) return;

            if (response == null) {
                throw new Exception(Tools.ToolsStrings.Uploader_CannotFinishAuthorization);
            }

            _authToken = response;
            _authExpiration = DateTime.Now + TimeSpan.FromSeconds(response.ExpiresIn) - TimeSpan.FromSeconds(20);
            ValuesStorage.SetEncrypted(KeyAuthToken, JsonConvert.SerializeObject(_authToken));
            ValuesStorage.Set(KeyAuthExpiration, _authExpiration);
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

        public override async Task<DirectoryEntry[]> GetDirectories(CancellationToken cancellation) {
            await Prepare(cancellation);

            if (_authToken == null) {
                throw new Exception(Tools.ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            const string query = "mimeType='application/vnd.google-apps.folder' and trashed = false and 'me' in writers";
            const string fields = "items(id,parents(id,isRoot),title)";
            var data = await Request.Get<SearchResult>(
                    @"https://www.googleapis.com/drive/v2/files?maxResults=1000&orderBy=title&" +
                            $"q={HttpUtility.UrlEncode(query)}&fields={HttpUtility.UrlEncode(fields)}",
                    _authToken.AccessToken, cancellation);
            
            if (data == null) {
                throw new Exception(Tools.ToolsStrings.Uploader_RequestFailed);
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
                    DisplayName = Tools.ToolsStrings.Uploader_RootDirectory,
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

        public override async Task<UploadResult> Upload(string name, string originalName, string mimeType, string description, byte[] data,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            await Prepare(cancellation);

            if (_authToken == null) {
                throw new Exception(Tools.ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }
            
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
                    data,
                    mimeType,
                    progress,
                    cancellation);
            if (entry == null) {
                throw new InformativeException(Tools.ToolsStrings.Uploader_CannotUploadToGoogleDrive, Tools.ToolsStrings.Common_MakeSureThereIsEnoughSpace);
            }

            var shared = await Request.Post<PermissionResult>($"https://www.googleapis.com/drive/v2/files/fileId/permissions?fileId={entry.Id}",
                    JsonConvert.SerializeObject(new {
                        role = @"reader",
                        type = @"anyone",
                    }).GetBytes(), _authToken.AccessToken, cancellation);
            if (shared == null) {
                throw new Exception(Tools.ToolsStrings.Uploader_CannotShareGoogleDrive);
            }

            return new UploadResult {
                Id = $"RG{entry.Id}",
                DirectUrl = $"https://drive.google.com/uc?export=download&id={entry.Id}"
            };
        }
    }
}