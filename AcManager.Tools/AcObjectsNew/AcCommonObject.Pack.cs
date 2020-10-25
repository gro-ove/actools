using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using SharpCompress.Common;
using SharpCompress.Writers;
using StringBasedFilter;
using StringBasedFilter.TestEntries;
using StringBasedFilter.Utils;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        public static string OptionCanBePackedFilter = "k-";

        private static readonly Lazy<IFilter<AcCommonObject>> CanBePackedFilterObj = new Lazy<IFilter<AcCommonObject>>(() =>
                Filter.Create(AcCommonObjectTester.Instance, OptionCanBePackedFilter));

        public virtual bool CanBePacked() {
            return CanBePackedFilterObj.Value.Test(this);
        }

        public class AcCommonObjectPackerParams {
            [CanBeNull]
            public string Destination { get; set; }

            public Func<string, Action<IWriter, string>> Override { get; set; }

            public bool ShowInExplorer { get; set; } = true;

            [CanBeNull]
            public IProgress<AsyncProgressEntry> Progress { get; set; }

            public CancellationToken Cancellation { get; set; }
        }

        protected abstract class AcCommonObjectPacker {
            private List<string> _added = new List<string>();
            private AcCommonObject _current;
            private string _basePath;

            [CanBeNull]
            private AcCommonObjectPackerParams _packerParams;

            public void SetParams([CanBeNull] AcCommonObjectPackerParams packerParams) {
                _packerParams = packerParams;
            }

            [CanBeNull]
            private IProgress<string> _progress;

            private CancellationToken _cancellation;

            public void SetProgress([CanBeNull] IProgress<string> progress, CancellationToken cancellation) {
                _progress = progress;
                _cancellation = cancellation;
            }

            [NotNull]
            public T GetParams<T>() where T : AcCommonObjectPackerParams, new() {
                if (!(_packerParams is T result)) {
                    result = new T();
                    _packerParams = result;
                }
                return result;
            }

            private void SetBasePath(params string[] basePath) {
                _basePath = basePath.Length == 0 ? null : Path.Combine(basePath);
            }

            private string GetKey(string name) {
                return name.StartsWith("/") ? name.Substring(1) :
                        FileUtils.NormalizePath(_basePath == null ? name : Path.Combine(_basePath, name));
            }

            private IWriter _writer;

            private bool Write(string name, Action<IWriter, string> fn) {
                var key = GetKey(name);
                var lower = key.ToLowerInvariant();
                if (_added.Contains(lower)) return false;
                _added.Add(lower);
                _progress?.Report(key);

                var overrideFn = _packerParams?.Override?.Invoke(key);
                if (overrideFn != null) {
                    overrideFn.Invoke(_writer, key);
                } else {
                    fn?.Invoke(_writer, key);
                }
                return true;
            }

            private bool AddFilename(string filename, bool forceExists) {
                var name = FileUtils.GetRelativePath(filename, _current.Location);
                if (!forceExists && !File.Exists(filename)) return false;
                return Write(name, (w, k) => w.Write(k, filename));
            }

            protected bool AddFilename(string name, string filename) {
                return Write(name, (w, k) => w.Write(k, filename));
            }

            private string[] _subFiles;

            private IEnumerable<string> GetFiles(string mask) {
                var location = _current.Location;

                if (_subFiles == null) {
                    _subFiles = Directory.GetFiles(location, "*", SearchOption.AllDirectories)
                            .Select(x => FileUtils.GetRelativePath(x, location).Replace('\\', '/')).ToArray();
                }

                var f = RegexFromQuery.Create(mask.Replace('\\', '/'), StringMatchMode.CompleteMatch);
                return _subFiles.Where(x => f.IsMatch(x)).Select(x => Path.Combine(location, x));
            }

            private bool Add(string name) {
                if (string.IsNullOrWhiteSpace(name)) return false;
                return name.Contains("*") || name.Contains("?")
                        ? GetFiles(name).Aggregate(false, (current, filename) => current | AddFilename(filename, true))
                        : AddFilename(Path.IsPathRooted(name) ? name : FileUtils.NormalizePath(Path.Combine(_current.Location, name)), false);
            }

            public bool Has(string name) {
                return _added.Contains(GetKey(name).ToLowerInvariant());
            }

            #region Public Add() methods
            public bool AddBytes(string name, byte[] data) {
                return Write(name, (w, k) => w.WriteBytes(k, data));
            }

            public bool AddString(string name, string data) {
                return Write(name, (w, k) => w.WriteString(k, data));
            }

            public bool AddStream(string name, Stream data) {
                return Write(name, (w, k) => w.Write(k, data));
            }

            [Pure]
            protected IEnumerable Add(IEnumerable<string> names) {
                return names.Select(Add);
            }

            [Localizable(false), Pure]
            protected IEnumerable Add(params string[] names) {
                return names.Select(Add);
            }

            [Pure]
            public IEnumerable Add(params AcCommonObject[] obj) {
                return Add((IEnumerable<AcCommonObject>)obj);
            }

            [Pure]
            public IEnumerable Add(IEnumerable<AcCommonObject> objs) {
                if (objs == null) yield break;

                foreach (var o in objs) {
                    if (o == null) continue;
                    var p = o.CreatePacker();
                    p._writer = _writer;
                    p._progress = _progress;
                    p._added = _added;

                    foreach (var v in p.Pack(o)) {
                        yield return v;
                    }
                }
            }
            #endregion

            private IEnumerable Pack(AcCommonObject obj) {
                SetBasePath(GetBasePath(obj));
                _current = obj;
                _subFiles = null;

                return PackOverride(obj);
            }

            private void Drain(IEnumerable enumerable, CancellationToken cancellation) {
                if (enumerable == null) return;

                var enumerator = enumerable.GetEnumerator();
                while (!cancellation.IsCancellationRequested && enumerator.MoveNext()) {
                    Drain(enumerator.Current as IEnumerable, cancellation);
                    if (enumerator.Current is string name) {
                        Add(name);
                    }
                }
            }

            public void Pack(Stream outputZipStream, AcCommonObject obj) {
                var description = PackedDescription.ToString(new[] { GetDescriptionOverride(obj) });
                using (var writer = WriterFactory.Open(outputZipStream, ArchiveType.Zip, CompressionType.Deflate)) {
                    _writer = writer;
                    _added.Clear();

                    Drain(Pack(obj), _cancellation);
                    if (_cancellation.IsCancellationRequested) return;

                    writer.WriteString("ReadMe.txt", description);
                }

                outputZipStream.AddZipDescription(description);
            }

            public void Pack(Stream outputZipStream, IEnumerable<AcCommonObject> objs) {
                var list = objs.ToList();
                var description = PackedDescription.ToString(list.Select(GetDescriptionOverride));
                using (var writer = WriterFactory.Open(outputZipStream, ArchiveType.Zip, CompressionType.Deflate)) {
                    _writer = writer;
                    _added.Clear();

                    foreach (var obj in list) {
                        Drain(Pack(obj), _cancellation);
                        if (_cancellation.IsCancellationRequested) return;
                    }

                    writer.WriteString("ReadMe.txt", description);
                }

                outputZipStream.AddZipDescription(description);
            }

            protected abstract string GetBasePath(AcCommonObject acCommonObject);
            protected abstract IEnumerable PackOverride(AcCommonObject acCommonObject);

            [CanBeNull]
            protected abstract PackedDescription GetDescriptionOverride(AcCommonObject acCommonObject);
        }

        protected abstract class AcCommonObjectPacker<T, TParams> : AcCommonObjectPacker
                where T : AcCommonObject where TParams : AcCommonObjectPackerParams, new() {
            private TParams _params;
            public TParams Params => _params ?? (_params = GetParams<TParams>());

            protected sealed override IEnumerable PackOverride(AcCommonObject acCommonObject) {
                return PackOverride((T)acCommonObject);
            }

            protected sealed override PackedDescription GetDescriptionOverride(AcCommonObject acCommonObject) {
                return GetDescriptionOverride((T)acCommonObject);
            }

            protected sealed override string GetBasePath(AcCommonObject acCommonObject) {
                return GetBasePath((T)acCommonObject);
            }

            protected abstract string GetBasePath(T t);
            protected abstract IEnumerable PackOverride(T t);

            [CanBeNull]
            protected abstract PackedDescription GetDescriptionOverride(T t);
        }

        protected abstract class AcCommonObjectPacker<T> : AcCommonObjectPacker<T, AcCommonObjectPackerParams>
                where T : AcCommonObject { }

        protected virtual AcCommonObjectPacker CreatePacker() {
            throw new NotSupportedException();
        }

        private void Pack(Stream outputZipStream, AcCommonObjectPackerParams packParams, IProgress<string> progress, CancellationToken cancellation) {
            var packer = CreatePacker();
            packer.SetProgress(progress, cancellation);
            packer.SetParams(packParams);
            packer.Pack(outputZipStream, this);
        }

        public static void Pack<T>(IEnumerable<T> objs, Stream outputZipStream, [CanBeNull] AcCommonObjectPackerParams packParams,
                [CanBeNull] IProgress<string> progress, CancellationToken cancellation) where T : AcCommonObject {
            var list = objs.ToList();
            if (list.Count == 0) return;

            var packer = list.First().CreatePacker();
            packer.SetProgress(progress, cancellation);
            packer.SetParams(packParams);
            packer.Pack(outputZipStream, list);
        }

        public async Task<bool> TryToPack(AcCommonObjectPackerParams packerParams) {
            try {
                using (var waiting = packerParams.Progress == null ? WaitingDialog.Create("Packing…") : null) {
                    var progress = waiting ?? packerParams.Progress;
                    var cancellation = waiting?.CancellationToken ?? packerParams.Cancellation;

                    await Task.Run(() => {
                        var destination = packerParams.Destination ?? Path.Combine(Location,
                                $"{Id}-{(this as IAcObjectVersionInformation)?.Version ?? "0"}-{DateTime.Now.ToUnixTimestamp()}.zip");
                        using (var output = File.Create(destination)) {
                            Pack(output, packerParams,
                                    new Progress<string>(x => progress?.Report(AsyncProgressEntry.FromStringIndetermitate($"Packing: {x}…"))),
                                    cancellation);
                        }

                        if (cancellation.IsCancellationRequested) return;
                        if (packerParams.ShowInExplorer) {
                            WindowsHelper.ViewFile(destination);
                        }
                    });
                }

                return true;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t pack", e);
                return false;
            }
        }

        private AsyncCommand<AcCommonObjectPackerParams> _packCommand;

        public AsyncCommand<AcCommonObjectPackerParams> PackCommand
            => _packCommand ?? (_packCommand = new AsyncCommand<AcCommonObjectPackerParams>(TryToPack, packerParams => CanBePacked()));
    }
}