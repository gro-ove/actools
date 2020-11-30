using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Workshop;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.WorkshopPublishTools.Submitters {
    public interface IWorkshopSubmitter {
        Task PrepareAsync();

        JObject BuildPayload();
    }

    public abstract class WorkshopBaseSubmittable<T> where T : AcJsonObjectNew {
        private string _temporaryLocation;

        protected string TemporaryLocation => _temporaryLocation ?? (_temporaryLocation =
                FilesStorage.Instance.GetTemporaryDirectory("Workshop", "Upload", Target.GetType().Name, Target.Id));

        private WorkshopClient.UploadGroup _uploadGroup;

        protected WorkshopClient.UploadGroup UploadGroup => _uploadGroup ?? (_uploadGroup =
                Client.StartNewUploadGroup());

        protected T Target { get; private set; }

        public WorkshopSubmitterParams Params { get; private set; }

        [NotNull]
        public WorkshopClient Client => Params.Client;

        [CanBeNull]
        public IUploadLogger Log => Params.Log;

        protected long GetDecompressedSize(string filename) {
            long ret = 0;
            using (var stream = File.OpenRead(filename))
            using (var archive = new ZipArchive(stream)) {
                foreach (var entry in archive.Entries) {
                    ret += entry.Length;
                }
            }
            return ret;
        }

        protected async Task<string> UploadFileAsync(string displayName, [Localizable(false)] string fileName, string source) {
            using (var op = Params.Log?.BeginParallel($"Uploading {displayName} for {Target.Id}")) {
                var ret = await UploadGroup.UploadAsync(File.ReadAllBytes(source), fileName, op);
                op?.SetResult(ret);
                return ret;
            }
        }

        public void SetTarget([NotNull] T target, WorkshopSubmitterParams submitterParams) {
            if (Target != null) throw new Exception("Target is already set");
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Params = submitterParams;
        }

        public virtual void EnsureInitialized() { }

        protected abstract Task PrepareOverrideAsync();

        private void CleanUp() {
            if (_temporaryLocation != null) {
                FileUtils.TryToDeleteDirectory(_temporaryLocation);
            }
        }

        public async Task PrepareAsync() {
            EnsureInitialized();
            await PrepareOverrideAsync();
            CleanUp();
        }

        public string DownloadUrl;
        public long InstallSize;

        public async Task PrepareMainPackageAsync(string packedFilename) {
            InstallSize = GetDecompressedSize(packedFilename);
            DownloadUrl = await UploadFileAsync("main package", "main.zip", packedFilename);
        }
    }

    public class WorkshopBaseSubmitter<T, TSubmittable> : IWorkshopSubmitter
            where T : AcJsonObjectNew
            where TSubmittable : WorkshopBaseSubmittable<T>, new() {
        [NotNull]
        protected T Target { get; }

        [NotNull]
        public TSubmittable Data { get; }

        protected bool IsChildObject { get; }

        [NotNull]
        protected WorkshopSubmitterParams Params => Data.Params;

        protected WorkshopBaseSubmitter([NotNull] T obj, bool isChildObject, WorkshopSubmitterParams submitterParams) {
            Data = new TSubmittable();
            Target = obj;
            IsChildObject = isChildObject;
            Data.SetTarget(Target, submitterParams);
        }

        public async Task PrepareAsync() {
            await Data.PrepareAsync();
        }

        public virtual JObject BuildPayload() {
            Data.EnsureInitialized();
            return new JObject {
                ["name"] = Target.Name,
                ["version"] = Target.Version,
                ["informationURL"] = Target.Url,
                ["description"] = Target.Description.Or(null),
                ["tags"] = JArray.FromObject(Target.Tags),
                ["originality"] = (int)Params.Originality,
                ["collabsInfo"] = JObject.FromObject(Params.CollabsInfo),
                ["downloadURL"] = Data.DownloadUrl,
                ["installSize"] = Data.InstallSize,
            };
        }
    }
}