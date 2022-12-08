using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.ExtraKn5Utils.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.ContentTools {
    public partial class TreeModelConverter {
        protected override Task<bool> LoadAsyncOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            return Task.FromResult(true);
        }

        protected override void InitializeOverride(Uri uri) {
            KnownEntries.ItemPropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(KnownEntry.Forgotten)) {
                    KnownEntries.Remove(sender as KnownEntry);
                }
            };
            this.OnActualUnload(() => {
                foreach (var entry in KnownEntries) {
                    entry.Dispose();
                }
            });
        }

        public class KnownEntry : Displayable, IDisposable {
            public string Origin { get; }

            public string Destination { get; }

            private IDisposable _watcher;

            public KnownEntry(string origin, string destination) {
                Origin = origin;
                Destination = destination;
                TreeParams = @"[SHADING]
SPECULAR=1.0
SUBSCATTERING=1.0
REFLECTIVITY=1.0
BRIGHTNESS=1.0
USE_AO_CHANNEL=0

[NORMALS]
BOOST=0.9
SPHERE=1.0

[LEAVES]
MULT=20.0
OFFSET=0.0

[FAKE_SHADOW]
SIZE=1.0
OPACITY=1.0";

                try {
                    if (File.Exists(destination)) {
                        using (var zip = ZipFile.OpenRead(destination)) {
                            var manifest = zip.GetEntry(@"tree.ini")?.Open().ReadAsStringAndDispose();
                            if (manifest != null) {
                                var cfg = IniFile.Parse(manifest);
                                foreach (var s in cfg.Keys.ApartFrom(@"SHADING", @"NORMALS", @"LEAVES", @"FAKE_SHADOW").ToList()) {
                                    cfg.Remove(s);
                                }
                                TreeParams = cfg.ToString();
                            }
                        }
                    }
                } catch (Exception e) {
                    Logging.Warning($"Failed to restore tree config: {e}");
                }

                _watcher = SimpleDirectoryWatcher.WatchFile(origin, async () => {
                    await Task.Delay(TimeSpan.FromSeconds(5d));
                    RefreshCommand.ExecuteAsync().Ignore();
                });
            }

            private string _treeParams;

            public string TreeParams {
                get => _treeParams;
                set => Apply(value, ref _treeParams);
            }

            private AsyncCommand _refreshCommand;

            public AsyncCommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand(async () => {
                try {
                    LastError = null;
                    await Task.Run(() => AcTreeModelConverter.Convert(Origin, Destination, TreeParams));
                } catch (Exception e) {
                    LastError = e.Message;
                }
            }));

            private DelegateCommand _forgetCommand;

            public DelegateCommand ForgetCommand => _forgetCommand ?? (_forgetCommand = new DelegateCommand(() => {
                Dispose();
                Forgotten = true;
            }));

            private bool _forgotten;

            public bool Forgotten {
                get => _forgotten;
                set => Apply(value, ref _forgotten);
            }

            private string _lastError;

            public string LastError {
                get => _lastError;
                set => Apply(value, ref _lastError);
            }

            public void Dispose() {
                DisposeHelper.Dispose(ref _watcher);
            }
        }

        public ChangeableObservableCollection<KnownEntry> KnownEntries { get; } = new ChangeableObservableCollection<KnownEntry>();

        private AsyncCommand _convertCommand;

        public AsyncCommand ConvertCommand => _convertCommand ?? (_convertCommand = new AsyncCommand(async () => {
            var input = FileRelatedDialogs.Open(new OpenDialogParams {
                DirectorySaveKey = "treemodelorigin",
                Filters = { DialogFilterPiece.FbxFiles, DialogFilterPiece.AllFiles },
                Title = "Select FBX with tree meshes (mesh per LOD)"
            });
            if (input == null) return;

            var stored = CacheStorage.Get<string>($"treeoutput.{input}");
            var output = FileRelatedDialogs.Save(new SaveDialogParams {
                DirectorySaveKey = "treemodeloutput",
                InitialDirectory = string.IsNullOrEmpty(stored) ? null : Path.GetDirectoryName(stored),
                RestoreDirectory = string.IsNullOrEmpty(stored),
                Filters = { new DialogFilterPiece("Converted models", "*.bin"), DialogFilterPiece.AllFiles },
                Title = "Select destination for converted model",
                DefaultFileName =  string.IsNullOrEmpty(stored) ? Path.GetFileNameWithoutExtension(input) + ".bin" : Path.GetFileName(stored)
            });
            if (output == null) return;

            CacheStorage.Set($"treeoutput.{input}", output);
            KnownEntries.Add(new KnownEntry(input, output));
            KnownEntries[KnownEntries.Count - 1].RefreshCommand.ExecuteAsync().Ignore();
        }));
    }
}