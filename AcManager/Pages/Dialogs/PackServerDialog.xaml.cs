using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using System.Windows.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.AcdFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;

namespace AcManager.Pages.Dialogs {
    public enum PackMode {
        [Description("Windows")]
        Windows,

        [Description("Linux")]
        Linux
    }

    public partial class PackServerDialog {
        public PackServerDialog(ServerPresetObject server) {
            DataContext = new ViewModel(null, server);
            InitializeComponent();
            Buttons = new [] {
                CreateExtraDialogButton("Pack", Model.PackCommand, true),
                CancelButton
            };
        }

        private ViewModel Model => (ViewModel)DataContext;

        public partial class ViewModel : NotifyPropertyChanged, IUserPresetable {
            private class SaveableData {
                public PackMode Mode = PackMode.Windows;
                public bool IncludeExecutable = true;
                public bool PackIntoSingle;
            }

            private void SaveLater() {
                if (_saveable.SaveLater()) {
                    Changed?.Invoke(this, new EventArgs());
                }
            }

            private readonly ISaveHelper _saveable;

            public ViewModel([CanBeNull] string serializedPreset, ServerPresetObject server) {
                Server = server;

                _saveable = new SaveHelper<SaveableData>("__PackServer", () => new SaveableData {
                    Mode = Mode,
                    IncludeExecutable = IncludeExecutable,
                    PackIntoSingle = PackIntoSingle,
                }, o => {
                    Mode = o.Mode;
                    IncludeExecutable = o.IncludeExecutable;
                    PackIntoSingle = o.PackIntoSingle;
                });

                if (string.IsNullOrEmpty(serializedPreset)) {
                    _saveable.Initialize();
                } else {
                    _saveable.Reset();
                    _saveable.FromSerializedString(serializedPreset);
                }
            }

            #region Read-only stuff
            public ServerPresetObject Server { get; }

            public PackMode[] Modes { get; } = EnumExtension.GetValues<PackMode>();
            #endregion

            #region Properies
            private PackMode _mode;

            public PackMode Mode {
                get => _mode;
                set {
                    if (Equals(value, _mode)) return;
                    _mode = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (Mode == PackMode.Linux) {
                        PackIntoSingle = false;
                    }
                }
            }

            private bool _includeExecutable;

            public bool IncludeExecutable {
                get => _includeExecutable;
                set {
                    if (Equals(value, _includeExecutable)) return;
                    _includeExecutable = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (!value) {
                        PackIntoSingle = false;
                    }
                }
            }

            private bool _packIntoSingle;

            public bool PackIntoSingle {
                get => _packIntoSingle;
                set {
                    if (Equals(value, _packIntoSingle)) return;
                    _packIntoSingle = value;
                    OnPropertyChanged();
                    SaveLater();

                    if (value) {
                        IncludeExecutable = true;
                    }
                }
            }
            #endregion

            #region Presetable
            public const string PresetableKeyValue = "Pack Server";
            public bool CanBeSaved => true;
            string IUserPresetable.PresetableKey => PresetableKeyValue;
            PresetsCategory IUserPresetable.PresetableCategory => new PresetsCategory(PresetableKeyValue);

            public string ExportToPresetData() {
                return _saveable.ToSerializedString();
            }

            public event EventHandler Changed;

            public void ImportFromPresetData(string data) {
                _saveable.FromSerializedString(data);
            }
            #endregion

            #region Actions
            [ItemCanBeNull]
            private async Task<string> PackAsync(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                var linuxMode = Mode == PackMode.Linux;

                progress.Report(AsyncProgressEntry.FromStringIndetermitate("Packing…"));
                if (!linuxMode && PackIntoSingle) {
                    return await PackIntoSingleAsync(progress, cancellation);
                }

                var result = FileUtils.EnsureUnique(FilesStorage.Instance.GetTemporaryFilename(
                        FileUtils.EnsureFileNameIsValid($@"Packed {Server.DisplayName}.{(linuxMode ? "tar.gz" : "zip")}")));

                var list = await Server.PackServerData(IncludeExecutable, linuxMode, false, cancellation);
                if (cancellation.IsCancellationRequested || list == null) return null;

                await Task.Run(() => {
                    try {
                        using (var memory = new MemoryStream()) {
                            using (var writer = WriterFactory.Open(memory,
                                    linuxMode ? ArchiveType.Tar : ArchiveType.Zip,
                                    linuxMode ? CompressionType.None : CompressionType.Deflate)) {
                                for (var i = 0; i < list.Count; i++) {
                                    var e = list[i];
                                    progress.Report(new AsyncProgressEntry(e.Key, i, list.Count));
                                    if (cancellation.IsCancellationRequested) return;

                                    var data = e.GetContent();
                                    if (data != null) {
                                        writer.WriteBytes(e.Key, data);
                                    } else {
                                        throw new InformativeException("Can’t pack server", $"File “{e.Key}” not found.");
                                    }
                                }
                            }

                            if (linuxMode) {
                                memory.Position = 0;

                                using (var actualMemory = new MemoryStream()) {
                                    using (var writer = new GZipWriter(actualMemory)) {
                                        writer.Write(@"tar.tar", memory);
                                    }

                                    File.WriteAllBytes(result, actualMemory.ToArray());
                                }
                            } else {
                                File.WriteAllBytes(result, memory.ToArray());
                            }
                        }
                    } finally {
                        // not needed here actually, but if it’s IDisposable, let’s keep it clean
                        list.DisposeEverything();
                    }
                });

                return result;
            }

            private AsyncCommand _packCommand;

            public AsyncCommand PackCommand => _packCommand ?? (_packCommand = new AsyncCommand(async () => {
                try {
                    using (var waiting = new WaitingDialog()) {
                        await EnsurePacked(Server, waiting);
                        if (waiting.CancellationToken.IsCancellationRequested) return;

                        var result = await PackAsync(waiting, waiting.CancellationToken);
                        if (waiting.CancellationToken.IsCancellationRequested || result == null) return;

                        WindowsHelper.ViewFile(result);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t pack server", e);
                }
            }));
            #endregion

            #region Extra
            private DelegateCommand<string> _navigateCommand;

            public DelegateCommand<string> NavigateCommand => _navigateCommand ?? (_navigateCommand = new DelegateCommand<string>(o => {
                WindowsHelper.ViewInBrowser(o?.ToString());
            }));
            #endregion
        }

        #region Utils
        [Localizable(false)]
        private static string GetPackedDataFixReadMe(string serverName, IEnumerable<string> notPacked) {
            return $@"Packed data for server {serverName}

    Cars:
{notPacked.Select(x => $"    - {x};").JoinToString("\n").ApartFromLast(";") + "."}

    Installation:
    - Find AC's content/cars directory;
    - Unpack there folders to it.

    Don't forget to make a backup if you already have data packed and it's
different.";
        }

        public static async Task EnsurePacked(ServerPresetObject server, WaitingDialog waiting) {
            var notPacked = server.CarIds.Select(CarsManager.Instance.GetById)
                                  .Where(x => x?.AcdData?.IsPacked == false).Select(x => x.DisplayName).ToList();

            if (notPacked.Any() &&
                    ShowMessage(string.Format(ToolsStrings.ServerPreset_UnpackedDataWarning, notPacked.JoinToReadableString()),
                            ToolsStrings.ServerPreset_UnpackedDataWarning_Title, MessageBoxButton.YesNo, "serverpreset_unpackedDataWarning") ==
                            MessageBoxResult.Yes) {
                using (var memory = new MemoryStream()) {
                    using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                        var i = 0;
                        foreach (var car in server.CarIds.Select(CarsManager.Instance.GetById).Where(x => x.AcdData?.IsPacked == false)) {
                            waiting.Report(new AsyncProgressEntry(car.DisplayName, i++, notPacked.Count));

                            await Task.Delay(50, waiting.CancellationToken);
                            if (waiting.CancellationToken.IsCancellationRequested) return;

                            var dataDirectory = Path.Combine(car.Location, "data");
                            var destination = Path.Combine(car.Location, "data.acd");
                            Acd.FromDirectory(dataDirectory).Save(destination);

                            writer.Write(Path.Combine(car.Id, "data.acd"), destination);
                        }

                        writer.WriteString(@"ReadMe.txt", GetPackedDataFixReadMe(server.DisplayName, notPacked));
                    }

                    var temporary = FileUtils.EnsureUnique(FilesStorage.Instance.GetTemporaryFilename(
                            FileUtils.EnsureFileNameIsValid($@"Fix for {server.DisplayName}.zip")));

                    waiting.Report("Saving archive…");
                    await FileUtils.WriteAllBytesAsync(temporary, memory.ToArray(), waiting.CancellationToken);
                    WindowsHelper.ViewFile(temporary);
                }
            }
        }
        #endregion
    }
}
