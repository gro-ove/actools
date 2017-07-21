using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools;
using CG.Web.MegaApiClient;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.LargeFilesSharing.Implementations {
    public class MegaUploader : FileUploaderBase {
        public MegaUploader(IStorage storage) : base(storage, "Mega", true, true) {
            _userEmail = Storage.GetEncryptedString(KeyUserEmail);
        }

        private MegaApiClient.LogonSessionToken _token;
        private bool IsTokenValid => _token?.MasterKey != null && _token.SessionId != null;

        public override async Task Reset() {
            await base.Reset();
            UserEmail = null;
            UserPassword = null;
            _token = null;
            Storage.Remove(KeySession);
            Storage.Remove(KeyToken);
        }

        public override Task Prepare(CancellationToken cancellation) {
            if (!IsReady) {
                _token = new MegaApiClient.LogonSessionToken(
                        Storage.GetEncryptedString(KeySession),
                        Storage.GetEncryptedBytes(KeyToken));
                if (IsTokenValid) {
                    IsReady = true;
                } else {
                    _token = null;
                }
            }

            return Task.Delay(0);
        }

        private const string KeyUserEmail = "umebokzkt9";
        private const string KeySession = "um7x4dkj4i";
        private const string KeyToken = "um3x2sm7vi";

        private string _userEmail;
        public string UserEmail {
            get => _userEmail;
            set {
                if (Equals(value, _userEmail)) return;
                _userEmail = value;
                OnPropertyChanged();
                Storage.SetEncrypted(KeyUserEmail, value);
            }
        }

        private string _userPassword;
        public string UserPassword {
            get => _userPassword;
            set {
                if (Equals(value, _userPassword)) return;
                _userPassword = value;
                OnPropertyChanged();
            }
        }

        public override async Task SignIn(CancellationToken cancellation) {
            await Prepare(cancellation);
            if (IsReady || cancellation.IsCancellationRequested) return;

            var client = new MegaApiClient();
            var token = await client.LoginAsync(MegaApiClient.GenerateAuthInfos(UserEmail, UserPassword));
            Storage.SetEncrypted(KeySession, token.SessionId);
            Storage.SetEncrypted(KeyToken, token.MasterKey);
            IsReady = true;
        }

        public class HierarchicalNode<T, TResult> {
            public T Obj;
            public List<HierarchicalNode<T, TResult>> Children = new List<HierarchicalNode<T, TResult>>(0);
            public TResult Result;
        }

        public static IEnumerable<TResult> HierarchicalNodes<T, TResult>(IEnumerable<T> source, Func<T, IReadOnlyList<T>, T> getParent,
                Func<T, IEnumerable<TResult>, TResult> convert) {
            var list = source.ToList();
            var root = new List<HierarchicalNode<T, TResult>>();
            var flat = new List<HierarchicalNode<T, TResult>>(list.Count);

            Func<T, HierarchicalNode<T, TResult>> process = null;
            process = node => {
                var index = flat.FindIndex(x => Equals(x.Obj, node));
                if (index != -1) return flat[index];

                var created = new HierarchicalNode<T, TResult>() { Obj = node };
                flat.Add(created);

                var parentN = getParent(node, list);
                if (!Equals(parentN, default(T))) {
                    var parent = process(parentN);
                    parent.Children.Add(created);
                } else {
                    root.Add(created);
                }

                return created;
            };

            for (var i = 0; i < list.Count; i++) {
                process(list[i]);
            }

            for (var i = 0; i < 10000; i++) {
                var any = false;
                for (var j = flat.Count - 1; j >= 0; j--) {
                    var hn = flat[j];
                    if (hn.Result == null && hn.Children.All(y => y.Result != null)) {
                        hn.Result = convert(hn.Obj, hn.Children.Select(x => x.Result));
                        any = true;
                        flat.RemoveAt(j);
                    }
                }

                if (!any) break;
            }

            return root.Select(x => x.Result);
        }

        public override async Task<DirectoryEntry[]> GetDirectories(CancellationToken cancellation) {
            await Prepare(cancellation);
            if (!IsTokenValid) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            var client = new MegaApiClient();
            await client.LoginAsync(_token);
            return HierarchicalNodes<INode, DirectoryEntry>(
                    (await client.GetNodesAsync()).Where(x => x.Type == NodeType.Root || x.Id != null && x.Name != null && x.Type == NodeType.Directory),
                    (node, nodes) => nodes.FirstOrDefault(x => x.Id == node.ParentId),
                    (node, results) => new DirectoryEntry {
                        Id = node.Id,
                        DisplayName = node.Name ?? "Root",
                        Children = results.ToArray()
                    }).ToArray();
        }

        public override async Task<UploadResult> Upload(string name, string originalName, string mimeType, string description, Stream data, UploadAs uploadAs,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var client = new MegaApiClient();
            await client.LoginAsync(_token);

            var totalLength = data.Length;
            var nodes = (await client.GetNodesAsync()).ToList();
            var node = nodes.FirstOrDefault(x => x.Id == DestinationDirectoryId) ?? nodes.First(x => x.Type == NodeType.Root);
            var result = await client.UploadAsync(data, name, node, new Progress<double>(v => {
                progress?.Report(AsyncProgressEntry.CreateUploading((long)(v / 100d * totalLength), totalLength));
            }), null, cancellation);
            var url = (await client.GetDownloadLinkAsync(result)).ToString();
            return new UploadResult {
                Id = $"{(uploadAs == UploadAs.Content ? "I6" : "Ii")}{Convert.ToBase64String(Encoding.UTF8.GetBytes(url))}",
                DirectUrl = url
            };
        }
    }
}