using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcTools.ExtraKn5Utils.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
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
                _watcher = SimpleDirectoryWatcher.WatchFile(origin, async () => {
                    await Task.Delay(TimeSpan.FromSeconds(5d));
                    RefreshCommand.ExecuteAsync().Ignore();
                });
            }

            private AsyncCommand _refreshCommand;

            public AsyncCommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand(async () => {
                try {
                    LastError = null;
                    await Task.Run(() => AcTreeModelConverter.Convert(Origin, Destination));
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

            var output = FileRelatedDialogs.Save(new SaveDialogParams {
                DirectorySaveKey = "treemodeloutput",
                Filters = { new DialogFilterPiece("Converted models", "*.bin"), DialogFilterPiece.AllFiles },
                Title = "Select destination for converted model",
                DefaultFileName = Path.GetFileNameWithoutExtension(input) + ".bin"
            });
            if (output == null) return;

            KnownEntries.Add(new KnownEntry(input, output));
            KnownEntries[KnownEntries.Count - 1].RefreshCommand.ExecuteAsync().Ignore();
        }));
    }
}