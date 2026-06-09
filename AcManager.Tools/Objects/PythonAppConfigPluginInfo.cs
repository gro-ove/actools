using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace AcManager.Tools.Objects {
    public sealed class PythonAppConfigPluginInfo : NotifyPropertyChanged, IWithId {
        public string Location { get; }

        public PythonAppConfigPluginInfo(string directory) {
            Location = directory;
            Id = Path.GetFileName(directory);
        }

        private string _displayName;
        private string _description;
        private string _author;
        private string _version;

        private void LoadInformation() {
            if (_displayName != null) return;
            try {
                var manifest = new IniFile(Path.Combine(Location, "manifest.ini"))["ABOUT"];
                _displayName = manifest.GetNonEmpty("NAME") ?? Id.ToTitle();
                _description = manifest.GetNonEmpty("DESCRIPTION");
                _author = manifest.GetNonEmpty("AUTHOR");
                _version = manifest.GetNonEmpty("VERSION");
                if (_version != null) {
                    _version = Regex.Replace(_version, @"^[vV](?=\d)", "");
                }
            } catch (Exception e) {
                Logging.Error(e);
                _displayName = Id.ToTitle();
                _description = null;
                _author = null;
                _version = null;
            }
        }

        public string DisplayName {
            get {
                LoadInformation();
                return _displayName;
            }
        }

        public string Description {
            get {
                LoadInformation();
                return _description;
            }
        }

        public string Author {
            get {
                LoadInformation();
                return _author;
            }
        }

        public string Version {
            get {
                LoadInformation();
                return _version;
            }
        }

        public string AuthorVersionLine {
            get {
                LoadInformation();
                if (string.IsNullOrWhiteSpace(Author) && string.IsNullOrWhiteSpace(Version)) {
                    return null;
                }
                return new[] {
                    $"By {Author}", $"v{Version}"
                }.JoinToString(", ");
            }
        }

        public string Id { get; }

        public void Reload() {
            _displayName = null;
            LoadInformation();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(AuthorVersionLine));
        }

        private bool Equals(PythonAppConfigPluginInfo other) {
            return Id == other.Id;
        }

        public override bool Equals(object obj) {
            return ReferenceEquals(this, obj) || obj is PythonAppConfigPluginInfo other && Equals(other);
        }

        public override int GetHashCode() {
            return (Id != null ? Id.GetHashCode() : 0);
        }

        private static string GetPackedFilename(string baseName, string extension, string version) {
            var last = $@"-{DateTime.Now:yyyyMMdd-HHmmss}{extension}";
            var name = $"{baseName}-{version ?? "0"}{last}";
            if (name.Length > 160) {
                name = name.Substring(0, 160 - last.Length) + last;
            }

            return FileRelatedDialogs.Save(new SaveDialogParams {
                Title = $"Pack {baseName}",
                Filters = {
                    extension == @".zip" ? DialogFilterPiece.ZipFiles : extension == @".exe" ? DialogFilterPiece.Applications :
                            extension == @".tar.gz" ? DialogFilterPiece.TarGZipFiles : DialogFilterPiece.Archives
                },
                DetaultExtension = extension,
                DirectorySaveKey = "packDir",
                DefaultFileName = name
            });
        }

        private DelegateCommand _viewInExplorerCommand;

        public DelegateCommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new DelegateCommand(
                () => { WindowsHelper.ViewDirectory(Location); }));

        private AsyncCommand _packCommand;

        public AsyncCommand PackCommand => _packCommand ?? (_packCommand = new AsyncCommand(async () => {
            try {
                var destination = GetPackedFilename(DisplayName, @".zip", Version);
                if (destination == null) return;

                using (var waiting = new WaitingDialog($"Packing {DisplayName}…")) {
                    var cancellation = waiting.CancellationToken;
                    waiting.Report(AsyncProgressEntry.Indetermitate);
                    await Task.Run(() => {
                        using (var output = File.Create(destination)) {
                            var root = AcRootDirectory.Instance.RequireValue;
                            using (var writer = WriterFactory.Open(output, ArchiveType.Zip, CompressionType.Deflate)) {
                                var files = FileUtils.GetFilesRecursive(Location)
                                        .Where(x => !Regex.IsMatch(x, @"[\\/]\.|\.(?:bak|tmp|psd)$")).ToList();
                                for (var i = 0; i < files.Count; i++) {
                                    var file = files[i];
                                    var relativePath = FileUtils.GetRelativePath(file, root);
                                    waiting.Report(new AsyncProgressEntry(relativePath, i, files.Count));
                                    writer.Write(relativePath, file);
                                }
                                if (cancellation.IsCancellationRequested) return;
                            }
                            output.AddZipDescription(PackedDescription.ToString(new[] {
                                new PackedDescription(Id, DisplayName, new Dictionary<string, string> {
                                    [@"Version"] = Version,
                                    [@"Made by"] = Author,
                                }, Path.GetDirectoryName(Location), true)
                            }));
                        }
                    });

                    if (cancellation.IsCancellationRequested) return;
                    WindowsHelper.ViewFile(destination);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t pack server", e);
            }
        }));
    }
}