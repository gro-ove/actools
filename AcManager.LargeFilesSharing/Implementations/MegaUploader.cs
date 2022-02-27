using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools;
using AcTools.Utils.Helpers;
using CG.Web.MegaApiClient;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing.Implementations {
    [UsedImplicitly]
    public class MegaUploader : FileUploaderBase {
        public MegaUploader(IStorage storage) : base(storage, "Mega",
                new Uri("/AcManager.LargeFilesSharing;component/Assets/Icons/Mega.png", UriKind.Relative),
                "50 GB of space, but with floating download quota. Has proper API for downloading shared files, so it works with CM, but files might get damaged due to poor network connection.",
                true, true) {
            _userEmail = Storage.GetEncrypted<string>(KeyUserEmail);
        }

        private MegaApiClient.LogonSessionToken _token;
        private bool IsTokenValid => _token?.MasterKey != null && _token.SessionId != null;

        public override async Task ResetAsync(CancellationToken cancellation) {
            await base.ResetAsync(cancellation);
            UserEmail = null;
            UserPassword = null;
            _token = null;
            Storage.Remove(KeySession);
            Storage.Remove(KeyToken);
        }

        public override Task PrepareAsync(CancellationToken cancellation) {
            if (!IsReady) {
                _token = new MegaApiClient.LogonSessionToken(
                        Storage.GetEncrypted<string>(KeySession),
                        Storage.GetEncrypted<byte[]>(KeyToken));
                if (IsTokenValid) {
                    IsReady = true;
                } else {
                    _token = null;
                }
            }

            return Task.Delay(0);
        }

        private const string KeyUserEmail = "email";
        private const string KeySession = "session";
        private const string KeyToken = "token";

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
            set => Apply(value, ref _userPassword);
        }

        public override async Task SignInAsync(CancellationToken cancellation) {
            await PrepareAsync(cancellation);
            if (IsReady || cancellation.IsCancellationRequested) return;

            Logging.Debug("UserEmail=" + UserEmail);
            Logging.Debug("UserPassword=" + UserPassword);

            if (string.IsNullOrWhiteSpace(UserEmail) || string.IsNullOrWhiteSpace(UserPassword)) {
                throw new InformativeException("Nothing to log in with to Mega.nz");
            }

            var client = new MegaApiClient(new Options(InternalUtils.GetMegaAppKey().Item1));
            _token = await client.LoginAsync(MegaApiClient.GenerateAuthInfos(UserEmail, UserPassword));
            Storage.SetEncrypted(KeySession, _token.SessionId);
            Storage.SetEncrypted(KeyToken, _token.MasterKey);
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

            HierarchicalNode<T, TResult> Process(T node) {
                var index = flat.FindIndex(x => Equals(x.Obj, node));
                if (index != -1) return flat[index];

                var created = new HierarchicalNode<T, TResult>() { Obj = node };
                flat.Add(created);

                var parentN = getParent(node, list);
                if (!Equals(parentN, default(T))) {
                    var parent = Process(parentN);
                    parent.Children.Add(created);
                } else {
                    root.Add(created);
                }

                return created;
            }

            for (var i = 0; i < list.Count; i++) {
                Process(list[i]);
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

        public override async Task<DirectoryEntry[]> GetDirectoriesAsync(CancellationToken cancellation) {
            await PrepareAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();

            if (!IsTokenValid) {
                throw new Exception(ToolsStrings.Uploader_AuthenticationTokenIsMissing);
            }

            var client = new MegaApiClient(new Options(InternalUtils.GetMegaAppKey().Item1));
            await client.LoginAsync(_token);
            cancellation.ThrowIfCancellationRequested();

            return HierarchicalNodes<INode, DirectoryEntry>(
                    (await client.GetNodesAsync()).Where(x => x.Type == NodeType.Root || x.Id != null && x.Name != null && x.Type == NodeType.Directory),
                    (node, nodes) => nodes.FirstOrDefault(x => x.Id == node.ParentId),
                    (node, results) => new DirectoryEntry {
                        Id = node.Id,
                        DisplayName = node.Name ?? ToolsStrings.Uploader_RootDirectory,
                        Children = results.ToArray()
                    }).ToArray();
        }

        public override async Task<UploadResult> UploadAsync(string name, string originalName, string mimeType, string description, Stream data, UploadAs uploadAs,
                IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var client = new MegaApiClient(new Options(InternalUtils.GetMegaAppKey().Item1));
            await client.LoginAsync(_token);
            cancellation.ThrowIfCancellationRequested();

            var totalLength = data.Length;
            var nodes = (await client.GetNodesAsync()).ToList();
            cancellation.ThrowIfCancellationRequested();

            var node = nodes.FirstOrDefault(x => x.Id == DestinationDirectoryId) ?? nodes.First(x => x.Type == NodeType.Root);
            var stopwatch = new AsyncProgressBytesStopwatch();
            var result = await client.UploadAsync(data, name, node, new Progress<double>(v => {
                progress?.Report(AsyncProgressEntry.CreateUploading((long)(v / 100d * totalLength), totalLength, stopwatch));
            }), null, cancellation);
            cancellation.ThrowIfCancellationRequested();

            var url = (await client.GetDownloadLinkAsync(result)).ToString();
            cancellation.ThrowIfCancellationRequested();

            Logging.Debug(url);
            var id = Regex.Match(url, @"#!(.+)");
            return id.Success ?
                    new UploadResult { Id = $"{(uploadAs == UploadAs.Content ? "Ei" : "RE")}{id.Groups[1].Value.ToCutBase64()}", DirectUrl = url } :
                    WrapUrl(url, uploadAs);
        }
    }
}