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
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.ContentTools {
    public partial class Uv2ModelConverter {
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

            private IDisposable _watcher1;
            private IDisposable _watcher2;

            public KnownEntry(string origin, string destination, string refKn5Filename) {
                Origin = origin;
                Destination = destination;
                _watcher1 = SimpleDirectoryWatcher.WatchFile(origin, async () => {
                    await Task.Delay(TimeSpan.FromSeconds(5d));
                    RefreshCommand.ExecuteAsync().Ignore();
                });
                _watcher2 = SimpleDirectoryWatcher.WatchFile(refKn5Filename, async () => {
                    await Task.Delay(TimeSpan.FromSeconds(5d));
                    RefreshCommand.ExecuteAsync().Ignore();
                });
            }

            private AsyncCommand _refreshCommand;

            public AsyncCommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand(async () => {
                try {
                    LastError = null;
                    await Task.Run(() => AcUv2ModelConverter.Convert(Origin, Destination));
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
                DisposeHelper.Dispose(ref _watcher1);
                DisposeHelper.Dispose(ref _watcher2);
            }
        }

        public ChangeableObservableCollection<KnownEntry> KnownEntries { get; } = new ChangeableObservableCollection<KnownEntry>();

        private AsyncCommand _convertCommand;

        public AsyncCommand ConvertCommand => _convertCommand ?? (_convertCommand = new AsyncCommand(async () => {
            var input = FileRelatedDialogs.Open(new OpenDialogParams {
                DirectorySaveKey = "uv2modelorigin",
                Filters = { DialogFilterPiece.FbxFiles, DialogFilterPiece.AllFiles },
                Title = "Select FBX with UV2"
            });
            if (input == null) return;

            var stored = CacheStorage.Get<string>($"uv2output.{input}");
            var output = FileRelatedDialogs.Save(new SaveDialogParams {
                DirectorySaveKey = "uv2modeloutput",
                InitialDirectory = string.IsNullOrEmpty(stored) ? null : Path.GetDirectoryName(stored),
                RestoreDirectory = string.IsNullOrEmpty(stored),
                Filters = { new DialogFilterPiece("Converted models", "*.kn5"), DialogFilterPiece.AllFiles },
                Title = "Select converted model",
                DefaultFileName =  string.IsNullOrEmpty(stored) ? Path.GetFileNameWithoutExtension(input) + ".kn5" : Path.GetFileName(stored),
                OverwritePrompt = false
            });
            if (output == null) return;

            CacheStorage.Set($"uv2output.{input}", output);
            KnownEntries.Add(new KnownEntry(input, FileUtils.ReplaceExtension(output, @".uv2"), output));
            KnownEntries[KnownEntries.Count - 1].RefreshCommand.ExecuteAsync().Ignore();
        }));
    }
}