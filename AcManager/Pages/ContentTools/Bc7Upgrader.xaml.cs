using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using Displayable = FirstFloor.ModernUI.Presentation.Displayable;

namespace AcManager.Pages.ContentTools {
    public partial class Bc7Upgrader {
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

            private readonly string _tmpFilename;
            private readonly long _originalSize;

            private AsyncProgressEntry _progressValue;

            public AsyncProgressEntry ProgressValue {
                get => _progressValue;
                set => Apply(value, ref _progressValue);
            }

            private string _status;

            public string Status {
                get => _status;
                set => Apply(value, ref _status);
            }

            private bool _processingNow;

            public bool ProcessingNow {
                get => _processingNow;
                set => Apply(value, ref _processingNow, () => {
                    if (value) {
                        ++_ignoreChanges;
                    } else {
                        Task.Delay(TimeSpan.FromSeconds(1d)).ContinueWith(r => --_ignoreChanges).Ignore();
                    }
                    _forgetCommand?.RaiseCanExecuteChanged();
                    _revertCommand?.RaiseCanExecuteChanged();
                });
            }

            private int _ignoreChanges;
            private bool _restarting;
            private IDisposable _watcher1;

            public KnownEntry(string origin) {
                Origin = origin;
                _tmpFilename = FileUtils.EnsureUnique(FilesStorage.Instance.GetTemporaryFilename("BC7 Originals", Path.GetFileName(origin)));
                try {
                    _originalSize = new FileInfo(origin).Length;
                } catch {
                    // ignored
                }
                _watcher1 = SimpleDirectoryWatcher.WatchFile(origin, async () => {
                    if (_ignoreChanges == 0) return;
                    await Task.Delay(TimeSpan.FromSeconds(5d));
                    if (_ignoreChanges == 0) return;
                    FileUtils.TryToDelete(_tmpFilename);
                    RefreshCommand.ExecuteAsync().Ignore();
                });
            }

            private void Restart() {
                if (_restarting) return;
                if (_processingCancellation != null) {
                    _restarting = true;
                    _processingCancellation.Cancel();
                    Task.Delay(100).ContinueWithInMainThread(_ => {
                        _restarting = false;
                        Restart();
                    });
                } else {
                    RefreshCommand.ExecuteAsync().Ignore();
                }
            }

            private CancellationTokenSource _processingCancellation;
            private AsyncCommand _refreshCommand;

            public AsyncCommand RefreshCommand => _refreshCommand ?? (_refreshCommand = new AsyncCommand(async () => {
                if (_processingNow) return;
                using (var cancellation = new CancellationTokenSource()) {
                    _processingCancellation = cancellation;
                    ProcessingNow = true;
                    ProgressValue = AsyncProgressEntry.Indetermitate;
                    try {
                        LastError = null;
                        var converted = await Bc7Encoder.EncodeKn5Async(Origin, _tmpFilename, new Bc7Encoder.EncodeParams {
                            ConvertAny = ConvertAny,
                            ConvertBc = ConvertBc,
                            ResizeMode = ResizeMode,
                        }, new Progress<AsyncProgressEntry>(progress => { ProgressValue = progress; }), cancellation.Token);
                        var status = converted == 0 ? "No suitable textures to convert."
                                : PluralizingConverter.PluralizeExt(converted, "{0} {texture} converted");
                        try {
                            var newFileSize = new FileInfo(Origin).Length;
                            if (_originalSize > newFileSize) {
                                status += $", {(_originalSize - newFileSize).ToReadableSize()} saved.";
                            } else {
                                status += ".";
                            }
                        } catch {
                            // ignored
                        }
                        Status = status;
                    } catch (Exception e) when (e.IsCancelled()) {
                        Status = null;
                        LastError = null;
                    } catch (Exception e) {
                        Status = null;
                        LastError = e.Message;
                    } finally {
                        ProcessingNow = false;
                    }
                    ProgressValue = AsyncProgressEntry.Finished;
                    if (ReferenceEquals(_processingCancellation, cancellation)) {
                        _processingCancellation = null;
                    }
                }
            }));

            private DelegateCommand _forgetCommand;

            public DelegateCommand ForgetCommand => _forgetCommand ?? (_forgetCommand = new DelegateCommand(() => {
                Dispose();
                Forgotten = true;
            }, () => !_processingNow));

            private DelegateCommand _revertCommand;

            public DelegateCommand RevertCommand => _revertCommand ?? (_revertCommand = new DelegateCommand(() => {
                if (File.Exists(_tmpFilename)) {
                    File.Move(_tmpFilename, Origin);
                }
                Dispose();
                Forgotten = true;
            }, () => !_processingNow));

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

            public List<Bc7Encoder.ResizeMode> ResizeModes { get; } = EnumExtension.GetValues<Bc7Encoder.ResizeMode>().ToList();

            private Bc7Encoder.ResizeMode _resizeMode = Bc7Encoder.ResizeMode.Point;

            public Bc7Encoder.ResizeMode ResizeMode {
                get => _resizeMode;
                set => Apply(value, ref _resizeMode, Restart);
            }

            private bool _convertBc;

            public bool ConvertBc {
                get => _convertBc;
                set => Apply(value, ref _convertBc, Restart);
            }

            private bool _convertAny;

            public bool ConvertAny {
                get => _convertAny;
                set => Apply(value, ref _convertAny, Restart);
            }

            public void Dispose() {
                DisposeHelper.Dispose(ref _watcher1);
                FileUtils.TryToDelete(_tmpFilename);
            }
        }

        public ChangeableObservableCollection<KnownEntry> KnownEntries { get; } = new ChangeableObservableCollection<KnownEntry>();

        private DelegateCommand _convertCommand;

        public DelegateCommand ConvertCommand => _convertCommand ?? (_convertCommand = new DelegateCommand(() => {
            var input = FileRelatedDialogs.OpenMultiple(new OpenDialogParams {
                DirectorySaveKey = "bc7origin",
                Filters = { DialogFilterPiece.Kn5Files, DialogFilterPiece.AllFiles },
                Title = "Select KN5 with textures to upgrade",
            });
            foreach (var s in input ?? new string[0]) {
                if (KnownEntries.Any(x => x.Origin == s)) continue;
                KnownEntries.Add(new KnownEntry(s));
                KnownEntries[KnownEntries.Count - 1].RefreshCommand.ExecuteAsync().Ignore();
            }
        }));
    }
}