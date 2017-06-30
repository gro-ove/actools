using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using SharpCompress.Common;
using SharpCompress.Writers;
using StringBasedFilter.Parsing;
using StringBasedFilter;
using StringBasedFilter.Utils;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        public static string OptionCanBePackedFilter = "k-";

        private static readonly Lazy<IFilter<AcCommonObject>> CanBePackedFilterObj = new Lazy<IFilter<AcCommonObject>>(() =>
                Filter.Create(AcCommonObjectTester.Instance, OptionCanBePackedFilter));

        public virtual bool CanBePacked() {
            return CanBePackedFilterObj.Value.Test(this);
        }

        protected class PackedDescription {
            private readonly string _id;
            private readonly string _name;
            private readonly Dictionary<string, string> _attributes;
            private readonly string _installTo;
            private readonly bool _cmSupportsContent;

            public PackedDescription(string id, string name, Dictionary<string, string> attributes, string installTo, bool cmSupportsContent) {
                _id = id;
                _name = name;
                _attributes = attributes;
                _installTo = installTo;
                _cmSupportsContent = cmSupportsContent;
            }

            public static string ToString(IEnumerable<PackedDescription> descriptions) {
                var list = descriptions.ToList();
                if (list.Count == 0) return "";

                var sb = new StringBuilder();

                sb.Append(list.Select(x => x._name).JoinToReadableString());
                sb.Append("\n\n");

                var attr = list.First()._attributes.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList();
                if (attr.Count > 0) {
                    var maxLength = attr.MaxEntry(x => x.Key.Length).Key.Length;
                    foreach (var prop in attr) {
                        sb.Append($"  {prop.Key}:{" ".RepeatString(2 + maxLength - prop.Key.Length)}{prop.Value}\n");
                    }

                    sb.Append('\n');
                }

                var installTo = list[0]._installTo;
                if (installTo.StartsWith(AcRootDirectory.Instance.Value ?? "-")) {
                    installTo = installTo.Replace(AcRootDirectory.Instance.Value ?? "-", "...\\SteamApps\\common\\assettocorsa");
                } else {
                    installTo = installTo.Replace(FileUtils.GetDocumentsDirectory(), "...\\Documents\\Assetto Ccorsa");
                }

                var cmPostfix = SettingsHolder.Content.MentionCmInPackedContent && list[0]._cmSupportsContent
                        ? " or simply drag'n'drop archive to Content Manager." : ".";
                if (list.Count == 1) {
                    sb.Append($"To install, move folder \"{list[0]._id}\" to \"{installTo}\"{cmPostfix}");
                } else {
                    sb.Append($"To install, move folders {list.Select(x => $"\"{x._id}\"").JoinToReadableString()} to \"{installTo}\"{cmPostfix}");
                }

                return sb.ToString().WordWrap(80);
            }
        }

        protected abstract class AcCommonObjectPacker {
            private readonly List<string> _added = new List<string>();
            private AcCommonObject _current;
            private IWriter _writer;
            private IProgress<string> _progress;

            public void SetProgress(IProgress<string> progress) {
                _progress = progress;
            }

            private bool AddFilename(string filename, bool forceExists) {
                var name = FileUtils.GetRelativePath(filename, _current.Location);

                var lower = name.ToLowerInvariant();
                if (_added.Contains(lower)) return false;

                if (forceExists || File.Exists(filename)) {
                    _added.Add(lower);
                    _progress?.Report(name);
                    _writer.Write(Path.Combine(_current.Id, name), filename);
                    return true;
                }

                return false;
            }

            public bool AddBytes(string name, byte[] data) {
                var lower = name.ToLowerInvariant();
                if (_added.Contains(lower)) return false;

                _added.Add(lower);
                _progress?.Report(name);
                _writer.WriteBytes(Path.Combine(_current.Id, name), data);
                return true;
            }

            public bool AddString(string name, string data) {
                var lower = name.ToLowerInvariant();
                if (_added.Contains(lower)) return false;

                _added.Add(lower);
                _progress?.Report(name);
                _writer.WriteString(Path.Combine(_current.Id, name), data);
                return true;
            }

            public bool AddStream(string name, Stream data) {
                var lower = name.ToLowerInvariant();
                if (_added.Contains(lower)) return false;

                _added.Add(lower);
                _progress?.Report(name);
                _writer.Write(Path.Combine(_current.Id, name), data);
                return true;
            }

            private string[] _subFiles;

            private IEnumerable<string> GetFiles(string mask) {
                var location = _current.Location;

                if (_subFiles == null) {
                    _subFiles = Directory.GetFiles(location, "*", SearchOption.AllDirectories).Select(x => FileUtils.GetRelativePath(x, location)).ToArray();
                }

                var f = RegexFromQuery.Create(mask.Replace('/', '\\'), true, true);
                return _subFiles.Where(x => f.IsMatch(x)).Select(x => Path.Combine(location, x));
            }

            protected bool Add(string name) {
                if (string.IsNullOrWhiteSpace(name)) return false;
                Logging.Debug(name);
                if (name.Contains("*") || name.Contains("?")) {
                    return GetFiles(name).Aggregate(false, (current, filename) => current | AddFilename(filename, true));
                } else {
                    return AddFilename(FileUtils.NormalizePath(Path.Combine(_current.Location, name)), false);
                }
            }

            protected bool Add(IEnumerable<string> names) {
                return names != null && names.Aggregate(false, (current, name) => current | Add(name));
            }

            protected bool Add(params string[] names) {
                return names != null && names.Aggregate(false, (current, name) => current | Add(name));
            }

            public void Pack(Stream outputZipStream, AcCommonObject obj) {
                lock (_added) {
                    _added.Clear();

                    var description = PackedDescription.ToString(new[]{ GetDescriptionOverride(obj) });
                    using (var writer = WriterFactory.Open(outputZipStream, ArchiveType.Zip, CompressionType.Deflate)) {
                        _writer = writer;
                        _current = obj;
                        _subFiles = null;
                        PackOverride(obj);
                        writer.WriteString("ReadMe.txt", description);
                    }

                    outputZipStream.AddZipDescription(description);
                }
            }

            public void Pack(Stream outputZipStream, IEnumerable<AcCommonObject> objs) {
                lock (_added) {
                    _added.Clear();

                    var list = objs.ToList();
                    var description = PackedDescription.ToString(list.Select(GetDescriptionOverride));
                    using (var writer = WriterFactory.Open(outputZipStream, ArchiveType.Zip, CompressionType.Deflate)) {
                        _writer = writer;

                        foreach (var obj in list) {
                            _current = obj;
                            _subFiles = null;
                            PackOverride(obj);
                        }

                        writer.WriteString("ReadMe.txt", description);
                    }

                    outputZipStream.AddZipDescription(description);
                }
            }

            protected abstract void PackOverride(AcCommonObject acCommonObject);
            protected abstract PackedDescription GetDescriptionOverride(AcCommonObject acCommonObject);
        }

        protected abstract class AcCommonObjectPacker<T> : AcCommonObjectPacker where T : AcCommonObject {
            protected sealed override void PackOverride(AcCommonObject acCommonObject) {
                PackOverride((T)acCommonObject);
            }

            protected sealed override PackedDescription GetDescriptionOverride(AcCommonObject acCommonObject) {
                return GetDescriptionOverride((T)acCommonObject);
            }

            protected abstract void PackOverride(T t);
            protected abstract PackedDescription GetDescriptionOverride(T t);
        }

        protected virtual AcCommonObjectPacker CreatePacker() {
            throw new NotSupportedException();
        }

        public class PackParams {
            public bool
        }

        private void Pack(Stream outputZipStream, IProgress<string> progress) {
            var packer = CreatePacker();
            packer.SetProgress(progress);
            packer.Pack(outputZipStream, this);
        }

        public static void Pack<T>(IEnumerable<T> objs, Stream outputZipStream, IProgress<string> progress) where T : AcCommonObject {
            var list = objs.ToList();
            if (list.Count == 0) return;

            var packer = list.First().CreatePacker();
            packer.SetProgress(progress);
            packer.Pack(outputZipStream, list);
        }

        private AsyncCommand _packCommand;

        public AsyncCommand PackCommand => _packCommand ?? (_packCommand = new AsyncCommand(async () => {
            try {
                using (var waiting = WaitingDialog.Create("Packing…")) {
                    await Task.Run(() => {
                        var destination = Path.Combine(Location,
                                $"{Id}-{(this as IAcObjectVersionInformation)?.Version ?? "0"}-{DateTime.Now.ToUnixTimestamp()}.zip");
                        using (var output = File.Create(destination)) {
                            Pack(output, new Progress<string>(x => waiting.Report(AsyncProgressEntry.FromStringIndetermitate($"Packing: {x}…"))));
                        }

                        WindowsHelper.ViewFile(destination);
                    });
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t pack car", e);
            }
        }, CanBePacked));
    }
}
