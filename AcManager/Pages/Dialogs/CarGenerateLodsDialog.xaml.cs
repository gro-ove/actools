using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Controls;
using AcManager.CustomShowroom;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.ExtraKn5Utils.LodGenerator;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class CarGenerateLodsDialog {
        public static PluginsRequirement Plugins { get; } = new PluginsRequirement(KnownPlugins.FbxConverter);
        private ViewModel Model => (ViewModel)DataContext;

        private CarGenerateLodsDialog(CarObject target) {
            DataContext = new ViewModel(target);
            Model.Finished += (sender, args) => CloseWithResult(MessageBoxResult.OK);
            InitializeComponent();
            this.OnActualUnload(Model);

            Buttons = new Control[] {
                CreateExtraDialogButton(UiStrings.Close, new DelegateCommand(() => {
                    if (!Model.IsBusy || ShowMessage("Are you sure you want to terminate generation?",
                            ControlsStrings.Common_AreYouSure, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                        CloseWithResult(MessageBoxResult.Cancel);
                    }
                }))
            };
        }

        public class ViewModel : NotifyPropertyChanged, IDisposable {
            private readonly CarObject _car;
            private readonly List<CustomShowroomLodDefinition> _baseLods;
            private CarLodGenerator _generator;
            private bool _disposed;

            public StoredValue<string> SimplygonLocation { get; } = Stored.Get("LodsGenerator.SimplygonLocation",
                    @"C:\Program Files\Simplygon\9\SimplygonBatch.exe");

            public ViewModel(CarObject target) {
                _car = target;
                PhaseCockpitLr = new PhaseParams(target);
                PhaseLodB = new PhaseParams(target);
                PhaseLodC = new PhaseParams(target);
                PhaseLodD = new PhaseParams(target);
                SimplygonLocation.PropertyChanged += (s, e) => CheckSimplygonLocation();
                MonitorLocation().Ignore();

                _baseLods = LoadBaseLodDefinitions();
                LodDefinitions.ReplaceEverythingBy_Direct(_baseLods);
                RefreshLodDetails();
            }

            private void RefreshLodDetails() {
                LodDefinitions.ReplaceIfDifferBy(LodDefinitions.OrderBy(x => x.Order));
                var itemsToFill = LodDefinitions.Where(x => x.Details == null).ToList();
                Task.Run(() => {
                    foreach (var item in itemsToFill) {
                        string message;
                        try {
                            var kn5 = Kn5.FromFile(item.Filename);
                            if (item.DisplayName.Contains("LOD")) {
                                message = CalculateStats(kn5.RootNode);
                            } else {
                                var cockpitHr = kn5.FirstByName("COCKPIT_HR");
                                var cockpitLr = kn5.FirstByName("COCKPIT_LR");
                                if (cockpitLr == null) {
                                    message = cockpitHr != null
                                            ? "Hi-res cockpit only: " + CalculateStats(cockpitHr)
                                            : "Cockpits are missing";
                                } else {
                                    message = "Low-res cockpit: " + CalculateStats(cockpitLr);
                                }
                            }
                        } catch (Exception e) {
                            Logging.Warning(e);
                            message = "<Failed to get data>";
                        }
                        ActionExtension.InvokeInMainThreadAsync(() => item.Details = message);
                    }

                    string CalculateStats(Kn5Node node) {
                        var meshes = 0;
                        var triangles = 0;
                        Iterate(node);
                        return new[] {
                            PluralizingConverter.PluralizeExt(meshes, "{0} mesh"),
                            PluralizingConverter.PluralizeExt(triangles, "{0} triangle"),
                        }.JoinToString(", ");

                        void Iterate(Kn5Node parent) {
                            if (parent.NodeClass == Kn5NodeClass.Mesh || parent.NodeClass == Kn5NodeClass.SkinnedMesh) {
                                ++meshes;
                                triangles += parent.Indices.Length / 3;
                            } else {
                                foreach (var child in parent.Children.Where(child => child.Active)) {
                                    Iterate(child);
                                }
                            }
                        }
                    }
                }).Ignore();
            }

            private List<CustomShowroomLodDefinition> LoadBaseLodDefinitions() {
                var ret = new List<CustomShowroomLodDefinition>();
                var lodsIni = _car.AcdData?.GetIniFile("lods.ini");
                if (lodsIni != null) {
                    ret.AddRange(lodsIni.GetSections("LOD")
                            .Select((x, i) => new { i, file = x.GetNonEmpty("FILE") })
                            .Select(section => new CustomShowroomLodDefinition {
                                DisplayName = section.i == 0 ? "Main model" : $"LOD {((char)('A' + section.i)).ToString()}",
                                Filename = Path.Combine(_car.Location, section.file),
                                Order = section.i
                            })
                            .Where(x => File.Exists(x.Filename)));
                }
                if (ret.Count == 0) {
                    ret.Add(new CustomShowroomLodDefinition {
                        DisplayName = "Main model",
                        Filename = AcPaths.GetMainCarFilename(_car.Location, _car.AcdData, false),
                        Order = 0
                    });
                }
                return ret;
            }

            private async Task MonitorLocation() {
                while (!_disposed && !SimplygonAvailable) {
                    CheckSimplygonLocation();
                    await Task.Delay(TimeSpan.FromSeconds(3d));
                }
            }

            private void InitializeGenerator() {
                _generator?.Dispose();
                try {
                    _generator = new CarLodGenerator(new CarLodSimplygonService(SimplygonLocation.Value), _car.Location,
                            FilesStorage.Instance.GetTemporaryDirectory("CarLodGenerator"));
                    CarHasCockpitHr = _generator.HasCockpitHr;

                    _carBodyMaterials = new TagsCollection(_generator.BodyMaterials);
                    CarBodyMaterials.CollectionChanged += (sender, args) => SyncCarBodyMaterials();
                    OnPropertyChanged(nameof(CarBodyMaterials));

                    _useExtremeLodDMerge = _generator.BodyMaterials != null;
                    OnPropertyChanged(nameof(UseExtremeLodDMerge));

                    GeneratorFailure = null;
                    _generateCommand?.RaiseCanExecuteChanged();
                } catch (Exception e) {
                    Logging.Warning(e);
                    GeneratorFailure = e.Message;
                }
            }

            public void CheckSimplygonLocation() {
                try {
                    SimplygonAvailable = !string.IsNullOrWhiteSpace(SimplygonLocation.Value) && File.Exists(SimplygonLocation.Value);
                } catch (Exception e) {
                    Logging.Warning(e);
                    SimplygonAvailable = false;
                }
            }

            private string _generatorFailure;

            public string GeneratorFailure {
                get => _generatorFailure;
                set => Apply(value, ref _generatorFailure);
            }

            private bool _simplygonAvailable;

            public bool SimplygonAvailable {
                get => _simplygonAvailable;
                set => Apply(value, ref _simplygonAvailable, () => {
                    if (value) {
                        InitializeGenerator();
                    }
                });
            }

            private bool _carHasCockpitHr;

            public bool CarHasCockpitHr {
                get => _carHasCockpitHr;
                set => Apply(value, ref _carHasCockpitHr);
            }

            private bool _useExtremeLodDMerge;

            public bool UseExtremeLodDMerge {
                get => _useExtremeLodDMerge;
                set => Apply(value, ref _useExtremeLodDMerge, SyncCarBodyMaterials);
            }

            private TagsCollection _carBodyMaterials;

            public TagsCollection CarBodyMaterials {
                get => _carBodyMaterials;
                set => Apply(value, ref _carBodyMaterials, SyncCarBodyMaterials);
            }

            public class PhaseParams : NotifyPropertyChanged {
                private CarObject _car;

                private bool _enabled = true;

                public bool Enabled {
                    get => _enabled;
                    set => Apply(value, ref _enabled);
                }

                private double? _progress;

                public double? Progress {
                    get => _progress;
                    set => Apply(value, ref _progress);
                }

                private bool _finished;

                public bool Finished {
                    get => _finished;
                    set => Apply(value, ref _finished);
                }

                private bool _useMerge;

                public bool UseMerge {
                    get => _useMerge;
                    set => Apply(value, ref _useMerge);
                }

                private string _modelFilename;

                [CanBeNull]
                public string ModelFilename {
                    get => _modelFilename;
                    set => Apply(value, ref _modelFilename);
                }

                public PhaseParams(CarObject car) {
                    _car = car;
                }
            }

            public BetterObservableCollection<CustomShowroomLodDefinition> LodDefinitions { get; } =
                new BetterObservableCollection<CustomShowroomLodDefinition>();

            private AsyncCommand<PhaseParams> _viewResultCommand;

            public AsyncCommand<PhaseParams> ViewResultCommand => _viewResultCommand ?? (_viewResultCommand = new AsyncCommand<PhaseParams>(async p => {
                if (p?.ModelFilename == null) return;
                if (p.ModelFilename != null) {
                    await CustomShowroomWrapper.StartAsync(_car, p.ModelFilename, LodDefinitions);
                }
            }));

            public PhaseParams PhaseCockpitLr { get; }
            public PhaseParams PhaseLodB { get; }
            public PhaseParams PhaseLodC { get; }
            public PhaseParams PhaseLodD { get; }

            private bool _cockpitLrSkipTransparent;

            public bool CockpitLrSkipTransparent {
                get => _cockpitLrSkipTransparent;
                set => Apply(value, ref _cockpitLrSkipTransparent);
            }

            private bool _cockpitLrRemoveLights;

            public bool CockpitLrRemoveLights {
                get => _cockpitLrRemoveLights;
                set => Apply(value, ref _cockpitLrRemoveLights);
            }

            private void SyncCarBodyMaterials() {
                _generator.BodyMaterials = UseExtremeLodDMerge ? CarBodyMaterials?.ToList() : null;
            }

            private DelegateCommand _simplygonLocateCommand;

            public DelegateCommand SimplygonLocateCommand => _simplygonLocateCommand ?? (_simplygonLocateCommand = new DelegateCommand(() => {
                SimplygonLocation.Value = FileRelatedDialogs.Open(new OpenDialogParams {
                    DirectorySaveKey = "simplygon",
                    InitialDirectory = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles")?.Replace(@" (x86)", "")
                            ?? @"C:\Program Files", @"Simplygon\9"),
                    Filters = {
                        new DialogFilterPiece("Simplygon Batch Tool", "SimplygonBatch.exe"),
                        DialogFilterPiece.Applications,
                        DialogFilterPiece.AllFiles,
                    },
                    Title = "Select Simplygon Batch tool",
                    DefaultFileName = Path.GetFileName(SimplygonLocation.Value),
                }) ?? SimplygonLocation.Value;
            }));

            private bool _isBusy;

            public bool IsBusy {
                get => _isBusy;
                set => Apply(value, ref _isBusy);
            }

            public void Dispose() {
                _disposed = true;
                _generator?.Dispose();
            }

            private PhaseParams GetPhase(string id) {
                switch (id) {
                    case "CockpitLr":
                        return PhaseCockpitLr;
                    case "LodB":
                        return PhaseLodB;
                    case "LodC":
                        return PhaseLodC;
                    case "LodD":
                        return PhaseLodD;
                    default:
                        Logging.Warning("Unknown phase: " + id);
                        return null;
                }
            }

            private int GetResultIndex(string id) {
                switch (id) {
                    case "CockpitLr":
                        return 0;
                    case "LodB":
                        return 1;
                    case "LodC":
                        return 2;
                    case "LodD":
                        return 3;
                    default:
                        return 4;
                }
            }

            private string GetResultFilename(string id) {
                return _generator.NewModels.ElementAtOrDefault(GetResultIndex(id));
            }

            private string GetResultName(string id) {
                switch (id) {
                    case "CockpitLr":
                        return "Main model";
                    case "LodB":
                        return "LOD B";
                    case "LodC":
                        return "LOD C";
                    case "LodD":
                        return "LOD D";
                    default:
                        return id;
                }
            }

            private bool _readyToSave;

            public bool ReadyToSave {
                get => _readyToSave;
                set => Apply(value, ref _readyToSave);
            }

            private AsyncCommand _generateCommand;

            public AsyncCommand GenerateCommand => _generateCommand ?? (_generateCommand = new AsyncCommand(async () => {
                IsBusy = true;
                try {
                    Kn5.FbxConverterLocation = PluginsManager.Instance.GetPluginFilename(KnownPlugins.FbxConverter, "FbxConverter.exe");
                    if (!File.Exists(Kn5.FbxConverterLocation)) {
                        throw new Exception("FbxConverter is not available");
                    }

                    _generator.GenerateCockpitLr = PhaseCockpitLr.Enabled;
                    _generator.GenerateLodB = PhaseLodB.Enabled;
                    _generator.GenerateLodC = PhaseLodC.Enabled;
                    _generator.GenerateLodD = PhaseLodD.Enabled;

                    await _generator.RunAsync(new Progress<CarLodGeneratorProgressUpdate>(msg => {
                        var phase = GetPhase(msg.Key);
                        if (phase != null && !phase.Finished) {
                            phase.Progress = msg.Value;
                            phase.Finished = msg.Value == 1d;
                            phase.ModelFilename = GetResultFilename(msg.Key);

                            if (phase.Finished) {
                                LodDefinitions.Add(new CustomShowroomLodDefinition {
                                    DisplayName = "New: " + GetResultName(msg.Key).ToSentenceMember(),
                                    Filename = GetResultFilename(msg.Key),
                                    Order = 10 + GetResultIndex(msg.Key)
                                });
                                RefreshLodDetails();
                            }
                        }
                    }));
                    ReadyToSave = true;
                } catch (Exception e) {
                    NonfatalError.Notify("Failed to generate LODs", e);
                    IsBusy = false;
                    ReadyToSave = false;
                }
            }, () => _generator != null));

            public static bool Warn(CarObject car) {
                if (ShowMessage(
                        "To apply LODs car data needs to be changed, but changing will fail online integrity check. Should CM change it? Choose “No” and updated “lods.ini” will be saved in data folder for manual replacement.",
                        "You’re about to modify car’s data", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return false;
                DataUpdateWarning.BackupData(car);
                return true;
            }

            public event EventHandler Finished;

            private DelegateCommand _finalSaveCommand;

            public DelegateCommand FinalSaveCommand => _finalSaveCommand ?? (_finalSaveCommand = new DelegateCommand(() => {
                if (_generator.LodsIniNeedsSaving) {
                    _generator.SaveLodsIni(_car.AcdData?.IsPacked != false && !Warn(_car));
                }
                _generator.ApplyLods();
                Finished?.Invoke(this, EventArgs.Empty);
            }, () => ReadyToSave));

            private DelegateCommand _finalCancelCommand;

            public DelegateCommand FinalCancelCommand => _finalCancelCommand ?? (_finalCancelCommand = new DelegateCommand(() => {
                PhaseCockpitLr.Finished = false;
                PhaseCockpitLr.ModelFilename = null;
                PhaseLodB.Finished = false;
                PhaseLodB.ModelFilename = null;
                PhaseLodC.Finished = false;
                PhaseLodC.ModelFilename = null;
                PhaseLodD.Finished = false;
                PhaseLodD.ModelFilename = null;
                ReadyToSave = false;
                IsBusy = false;
                LodDefinitions.ReplaceEverythingBy_Direct(_baseLods);
            }, () => ReadyToSave));

            public class CarLodSimplygonService : ICarLodGeneratorService {
                private readonly string _simplygonExecutable;

                public CarLodSimplygonService(string simplygonExecutable) {
                    _simplygonExecutable = simplygonExecutable;
                }

                private async Task RunProcessAsync(string filename, IEnumerable<string> args,
                        IProgress<double?> progress, CancellationToken cancellationToken) {
                    var process = ProcessExtension.Start(filename, args, new ProcessStartInfo {
                        UseShellExecute = false,
                        RedirectStandardOutput = progress != null,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    });
                    try {
                        ChildProcessTracker.AddProcess(process);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (progress != null) {
                            process.OutputDataReceived += (sender, eventArgs) => {
                                if (!string.IsNullOrWhiteSpace(eventArgs.Data)) {
                                    progress.Report(eventArgs.Data.As<double?>() / 100d);
                                }
                            };
                        }
                        process.ErrorDataReceived += (sender, eventArgs) => {
                            if (!string.IsNullOrWhiteSpace(eventArgs.Data)) {
                                Logging.Warning(eventArgs.Data);
                            }
                        };
                        if (progress != null) {
                            process.BeginOutputReadLine();
                        }
                        process.BeginErrorReadLine();
                        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                        if (process.ExitCode != 0) {
                            throw new Exception("Failed to run process: " + process.ExitCode);
                        }
                    } finally {
                        if (!process.HasExitedSafe()) {
                            process.Kill();
                        }
                        process.Dispose();
                    }
                }

                public async Task GenerateLodAsync(string configuration, string inputFile, string outputFile,
                        IProgress<double?> progress, CancellationToken cancellationToken) {
                    // Amazingly, we need to convert input FBX to pretty much the same FBX. That will remove some animation-related metadata added by
                    // FBXConverter when it was converting model from COLLADA format.

                    var intermediateFile = $@"{inputFile.ApartFromLast(".fbx")}_fixed.fbx";
                    await RunProcessAsync(Kn5.FbxConverterLocation, new[] { inputFile, intermediateFile, "/sffFBX", "/dffFBX", "/f201300" },
                            null, cancellationToken).ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.05);

                    /*await RunProcessAsync(_simplygonExecutable, new[] {
                        "-Progress",
                        FilesStorage.Instance.GetContentFile(ContentCategory.Miscellaneous, $"SimplygonPresets/{configuration}.json").Filename,
                        intermediateFile, outputFile
                    }, progress.SubrangeDouble(0.05, 1.0), cancellationToken).ConfigureAwait(false);*/
                }
            }
        }

        public static Task<bool> RunAsync(CarObject target) {
            try {
                var dialog = new CarGenerateLodsDialog(target);
                dialog.ShowDialog();
                return Task.FromResult(dialog.IsResultOk);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t replace tyres", e);
                return Task.FromResult(false);
            }
        }
    }
}