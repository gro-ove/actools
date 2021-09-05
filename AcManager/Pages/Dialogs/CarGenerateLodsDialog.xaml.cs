using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.Graphs;
using AcManager.Controls.Helpers;
using AcManager.CustomShowroom;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.ExtraKn5Utils.Kn5Utils;
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
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;
using StringBasedFilter;
using StringBasedFilter.Parsing;

namespace AcManager.Pages.Dialogs {
    public partial class CarGenerateLodsDialog {
        public static long OptionCacheSize = 100 * 1024 * 1024;
        public static readonly string TemporaryDirectoryName = "CarLodGenerator";
        public static readonly string PresetableKey = "Car LODs Generation";
        public static readonly PresetsCategory PresetableKeyCategory = new PresetsCategory(PresetableKey);
        public static readonly string PreviousSettingsPresetsName = "Previous car settings";

        public static PluginsRequirement Plugins { get; } = new PluginsRequirement(KnownPlugins.FbxConverter);

        private ViewModel Model => (ViewModel)DataContext;

        private bool _closingGracefully;

        private CarGenerateLodsDialog(CarObject target) {
            DataContext = new ViewModel(target);
            Model.Finished += (sender, args) => {
                _closingGracefully = true;
                CloseWithResult(MessageBoxResult.OK);
            };
            InitializeComponent();
            InputBindings.AddRange(new[] {
                new InputBinding(Model.OpenDirectoryCommand, new KeyGesture(Key.F, ModifierKeys.Control)),
                new InputBinding(Model.OpenShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt)),
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control))
            });
            Buttons = new Control[0];
            Loaded += (s, a) => Model.LoadedWindow = this;
            this.AddWidthCondition(720).Add(x => LodsGrid.FindVisualChild<SpacingUniformGrid>()?.SetValue(SpacingUniformGrid.ColumnsProperty, x ? 2 : 1));
            this.OnActualUnload(Model);
        }

        protected override void OnClosingOverride(CancelEventArgs e) {
            if (_closingGracefully) return;
            e.Cancel = e.Cancel
                    || (!Model.GenerateCommand.IsAbleToExecute || Model.HasDataToSave) && ShowMessage(Model.HasDataToSave
                            ? "Are you sure you want to abandon generated models?"
                            : "Are you sure you want to terminate generation?",
                            ControlsStrings.Common_AreYouSure, MessageBoxButton.YesNo) != MessageBoxResult.Yes;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) {
            CloseWithResult(MessageBoxResult.Cancel);
        }

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class StageParams : Displayable, IWithId {
            public CarLodGeneratorStageParams Stage { get; }

            public string Id { get; }

            public string Filename { get; }

            public string SimplygonConfigurationFilename => FilesStorage.Instance.GetContentFile(ContentCategory.CarLodsGeneration,
                    $@"StageRules.{Id}.json").Filename;

            public static string GetId(string filename) {
                return Path.GetFileNameWithoutExtension(filename).ApartFromFirst(@"Stage.");
            }

            public StageParams(string filename, JObject definitions, Dictionary<string, string> userDefined) {
                Id = GetId(filename);
                Filename = filename;
                Stage = new CarLodGeneratorStageParams(Id, filename, definitions, userDefined);
                DisplayName = Stage.Name ?? Id;
                TrianglesCount = Stage.Triangles?.Count ?? 10000;
                TrianglesRecommendedCount = Tuple.Create(Stage.Triangles?.RecommendedParameters?.RecommendedMin ?? 0,
                        Stage.Triangles?.RecommendedParameters?.RecommendedMax ?? 0);
            }

            public void RefreshStage() {
                Stage.Refresh(Filename);
                TrianglesCount = Stage.Triangles?.Count ?? 10000;
                TrianglesRecommendedCount = Tuple.Create(Stage.Triangles?.RecommendedParameters?.RecommendedMin ?? 0,
                        Stage.Triangles?.RecommendedParameters?.RecommendedMax ?? 0);
                IsUserActive = true;
                OnPropertyChanged(nameof(IsUserActive));
                OnPropertyChanged(nameof(ApplyWeldingFix));
                OnPropertyChanged(nameof(MergeExceptions));
                OnPropertyChanged(nameof(MergeParents));
                OnPropertyChanged(nameof(MergeAsBlack));
                OnPropertyChanged(nameof(ElementsToRemove));
                OnPropertyChanged(nameof(EmptyNodesToKeep));
                OnPropertyChanged(nameof(ElementsPriorities));
                OnPropertyChanged(nameof(OffsetsAlongNormal));
                OnPropertyChanged(nameof(KeepTemporaryFiles));
            }

            private void Apply<T>([CanBeNull] T[] value, [CanBeNull] ref T[] backendValue, Action onChangeCallback = null,
                    [CallerMemberName] string propertyName = null) {
                if (backendValue == null || value == null ? value != backendValue : !backendValue.SequenceEqual(value)) {
                    backendValue = value;
                    OnPropertyChanged(propertyName);
                    onChangeCallback?.Invoke();
                }
            }

            private bool _isAvailable;

            public bool IsAvailable {
                get => _isAvailable;
                set => Apply(value, ref _isAvailable, () => OnPropertyChanged(nameof(IsActive)));
            }

            private string _unavailableReason;

            public string UnavailableReason {
                get => _unavailableReason;
                set => Apply(value, ref _unavailableReason, () => OnPropertyChanged(nameof(ErrorMessage)));
            }

            public string ErrorMessage => UnavailableReason ?? GenerationErrorMessage;

            private bool _isActive;

            [JsonProperty("isActive")]
            public bool IsUserActive {
                get => _isActive;
                set => Apply(value, ref _isActive);
            }

            public bool IsAvailableAndActive => IsAvailable && IsUserActive;

            [JsonProperty("trianglesCount")]
            public int TrianglesCount {
                get => Stage.Triangles.Count;
                set => Apply(value.Round(100), ref Stage.Triangles.Count);
            }

            private Tuple<int, int> _trianglesRecommendedCount;

            public Tuple<int, int> TrianglesRecommendedCount {
                get => _trianglesRecommendedCount;
                set => Apply(value, ref _trianglesRecommendedCount);
            }

            public string DisplayTrianglesHint => $"AC Pipeline recommends {TrianglesRecommendedCount.Item1}…{TrianglesRecommendedCount.Item2} triangles";

            [JsonProperty("applyWeldingFix")]
            public bool ApplyWeldingFix {
                get => Stage.ApplyWeldingFix;
                set => Apply(value, ref Stage.ApplyWeldingFix);
            }

            [JsonProperty("mergeExceptions")]
            public string MergeExceptions {
                get => Stage.MergeExceptions?.JoinToString("\n");
                set => Apply(value?.Split('\n'), ref Stage.MergeExceptions);
            }

            [JsonProperty("mergeParents")]
            public string MergeParents {
                get => Stage.MergeParents?.JoinToString("\n");
                set => Apply(value?.Split('\n'), ref Stage.MergeParents);
            }

            [JsonProperty("mergeAsBlack")]
            public string MergeAsBlack {
                get => Stage.MergeAsBlack?.JoinToString("\n");
                set => Apply(value?.Split('\n'), ref Stage.MergeAsBlack);
            }

            [JsonProperty("elementsToRemove")]
            public string ElementsToRemove {
                get => Stage.ElementsToRemove?.JoinToString("\n");
                set => Apply(value?.Split('\n'), ref Stage.ElementsToRemove);
            }

            [JsonProperty("emptyNodesToKeep")]
            public string EmptyNodesToKeep {
                get => Stage.EmptyNodesToKeep?.JoinToString("\n");
                set => Apply(value?.Split('\n'), ref Stage.EmptyNodesToKeep);
            }

            [JsonProperty("elementsPriorities")]
            public string ElementsPriorities {
                get => Stage.ElementsPriorities?.Select(x => $@"{x.Filter} = {x.Priority}").JoinToString("\n");
                set => Apply(value?.Split('\n').Select(x => {
                    var i = x.LastIndexOf('=');
                    return i == -1 ? null : new CarLodGeneratorStageParams.ElementsPriority {
                        Filter = x.Substring(0, i).Trim(),
                        Priority = x.Substring(i + 1).As(double.NaN)
                    };
                }).Where(x => x?.Filter != null && !double.IsNaN(x.Priority)).ToArray(), ref Stage.ElementsPriorities);
            }

            [JsonProperty("offsetsAlongNormal")]
            public string OffsetsAlongNormal {
                get => Stage.OffsetsAlongNormal?.Select(x => $@"{x.Filter} = {x.Priority}").JoinToString("\n");
                set => Apply(value?.Split('\n').Select(x => {
                    var i = x.LastIndexOf('=');
                    return i == -1 ? null : new CarLodGeneratorStageParams.ElementsPriority {
                        Filter = x.Substring(0, i).Trim(),
                        Priority = x.Substring(i + 1).As(double.NaN)
                    };
                }).Where(x => x?.Filter != null && !double.IsNaN(x.Priority)).ToArray(), ref Stage.OffsetsAlongNormal);
            }

            [JsonProperty("keepTemporaryFiles")]
            public bool KeepTemporaryFiles {
                get => Stage.KeepTemporaryFiles;
                set => Apply(value, ref Stage.KeepTemporaryFiles);
            }

            private bool _generatingNow;

            public bool GeneratingNow {
                get => _generatingNow;
                set => Apply(value, ref _generatingNow);
            }

            private double _generationProgress;

            public double GenerationProgress {
                get => _generationProgress;
                set => Apply(value, ref _generationProgress);
            }

            [ItemCanBeNull]
            public BetterObservableCollection<CustomShowroomLodDefinition> GeneratedModels { get; } =
                new BetterObservableCollection<CustomShowroomLodDefinition>();

            private CustomShowroomLodDefinition _selectedGeneratedModel;

            [CanBeNull]
            public CustomShowroomLodDefinition SelectedGeneratedModel {
                get => _selectedGeneratedModel;
                set => Apply(value, ref _selectedGeneratedModel,
                        () => GeneratedModels.NonNull().ForEach(x => x.IsSelected = x == value));
            }

            private string _generationErrorMessage;

            [CanBeNull]
            public string GenerationErrorMessage {
                get => _generationErrorMessage;
                set => Apply(value, ref _generationErrorMessage, () => OnPropertyChanged(nameof(ErrorMessage)));
            }

            public void AddLodDefinition([CanBeNull] CustomShowroomLodDefinition filename) {
                GeneratedModels.Add(filename);
                SelectedGeneratedModel = filename;
            }

            private DelegateCommand _viewSimplygonConfigurationCommand;

            public DelegateCommand ViewSimplygonConfigurationCommand => _viewSimplygonConfigurationCommand ?? (_viewSimplygonConfigurationCommand =
                    new DelegateCommand(() => WindowsHelper.ViewFile(SimplygonConfigurationFilename)));

            public string Serialize() {
                return JsonConvert.SerializeObject(this);
            }

            public void Deserialize(string data) {
                JsonConvert.PopulateObject(data, this);
            }

            public static bool IsSavedProperty(string propertyName) {
                switch (propertyName) {
                    case nameof(IsUserActive):
                    case nameof(TrianglesCount):
                    case nameof(ApplyWeldingFix):
                    case nameof(MergeExceptions):
                    case nameof(MergeParents):
                    case nameof(MergeAsBlack):
                    case nameof(ElementsToRemove):
                    case nameof(EmptyNodesToKeep):
                    case nameof(ElementsPriorities):
                    case nameof(OffsetsAlongNormal):
                    case nameof(KeepTemporaryFiles):
                        return true;
                    default:
                        return false;
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged, IUserPresetable, IDisposable {
            public CarObject Car { get; }
            private readonly Dictionary<string, string> _userDefined = new Dictionary<string, string>();
            private bool _availabilityChecked;
            private bool _disposed;

            public Window LoadedWindow;

            public StoredValue<string> SimplygonLocation { get; } = Stored.Get("LodsGenerator.SimplygonLocation",
                    @"C:\Program Files\Simplygon\9\SimplygonBatch.exe");

            public ChangeableObservableCollection<StageParams> Stages { get; }

            public ChangeableObservableCollection<CustomShowroomLodDefinition> LodDefinitions { get; }

            private static JObject LoadCommonDefinitions() {
                try {
                    return JObject.Parse(File.ReadAllText(FilesStorage.Instance.GetContentFile(ContentCategory.CarLodsGeneration,
                            @"CommonDefinitions.json").Filename));
                } catch (Exception e) {
                    Logging.Warning(e);
                    return new JObject();
                }
            }

            public sealed class UserDefinedValue : Displayable {
                [NotNull]
                public string Key { get; }

                public UserDefinedValue([NotNull] string key, JObject data) {
                    Key = key;
                    DisplayName = data.GetStringValueOnly("name");
                    Description = data.GetStringValueOnly("description");
                    DefaultValue = data.GetStringValueOnly("default");
                }

                private string _description;

                [CanBeNull]
                public string Description {
                    get => _description;
                    set => Apply(value, ref _description);
                }

                private string _value;

                [CanBeNull]
                public string Value {
                    get => _value;
                    set => Apply(value?.Or(null), ref _value);
                }

                private string _defaultValue;

                [CanBeNull]
                public string DefaultValue {
                    get => _defaultValue;
                    set => Apply(value, ref _defaultValue);
                }

                private DelegateCommand _fillDefaultValueCommand;

                public DelegateCommand FillDefaultValueCommand
                    => _fillDefaultValueCommand ?? (_fillDefaultValueCommand = new DelegateCommand(() => { Value = DefaultValue; }));
            }

            public ChangeableObservableCollection<UserDefinedValue> UserDefinedValues { get; }
                = new ChangeableObservableCollection<UserDefinedValue>();

            private void UpdateUserDefined() {
                foreach (var value in UserDefinedValues) {
                    _userDefined[value.Key] = value.Value.Or(value.DefaultValue);
                }
                ScanForPotentialIssues();
            }

            public IReadOnlyList<Link> SettingsLinks { get; } = new[] {
                new Link {
                    DisplayName = "Base settings",
                    Key = "Base"
                },
                new Link {
                    DisplayName = "To remove",
                    Key = "ElementsToRemove",
                    Tag =
                        "Nodes and meshes listed here will be removed completely, leaving more triangles for the rest of the model. Best way to improve overall quality is to add something here, like some suspension details or stuff under the bonnet."
                },
                new Link {
                    DisplayName = "Priorities",
                    Key = "ElementsPriorities",
                    Tag =
                        "Higher priority means more triangles. Since there are only so many triangles to go around, it can be helpful to seriously cut down on number of triangles wasted on interior, for example. Can’t see it from outside anyway."
                },
                new Link {
                    DisplayName = "Merge exceptions",
                    Key = "MergeExceptions",
                    Tag =
                        "LOD generator will try to merge as many meshes as possible, separating them into groups based on materials and transparent flag. Of course though not every mesh needs merging. Here you can set all the exceptions."
                },
                new Link {
                    DisplayName = "Merge parents",
                    Key = "MergeParents",
                    Tag =
                        "When merging meshes, LOD generator takes into account nearest merge parent and groups meshes around it. After all, we wouldn’t want left and right tyre, for example, merged together."
                },
                new Link {
                    DisplayName = "Merge as black",
                    Key = "MergeAsBlack",
                    Tag =
                        "Option for particularly hardcore optimization. Everything listed here will get a new pitch black material, allowing to merge meshes much better. Generally used for fourth LOD, coloring black any material other than car paint."
                },
                new Link {
                    DisplayName = "Offsets along normal",
                    Key = "OffsetsAlongNormal",
                    Tag =
                        "During preparation LOD generator would move vertices for meshes listed here along normal (in other words, perpendicular to surface). Positive values will make meshes “grow”, negative — shrink. Mainly meant for stickers to expand a bit to prevent them from clipping through underlying surface."
                },
            };

            private Link _selectedSettingsSection;

            public Link SelectedSettingsSection {
                get => _selectedSettingsSection ?? (_selectedSettingsSection = SettingsLinks.First());
                set => Apply(value, ref _selectedSettingsSection);
            }

            private readonly ISaveHelper _saveable;

            private class SaveableData {
                [CanBeNull]
                public Dictionary<string, string> UserDefinedValues;

                [CanBeNull]
                public Dictionary<string, string> Stages;
            }

            private bool _cacheInformationReady;

            public bool CacheInformationReady {
                get => _cacheInformationReady;
                set => Apply(value, ref _cacheInformationReady);
            }

            public long CacheFileLimitMb => OptionCacheSize / 1024 / 1024;

            private long _cacheFileSize;

            public long CacheFileSize {
                get => _cacheFileSize;
                set => Apply(value, ref _cacheFileSize);
            }

            public async Task ScanCacheAsync() {
                await Task.Run(() => {
                    var files = new DirectoryInfo(FilesStorage.Instance.GetTemporaryDirectory(TemporaryDirectoryName))
                            .GetFiles().Where(x => x.Name.EndsWith(".kn5") || x.Name.EndsWith(".fbx")).ToList();
                    var totalSize = files.Sum(x => x.Length);
                    if (totalSize > OptionCacheSize) {
                        var spaceToFree = totalSize - OptionCacheSize + 16 * 1024 * 1024;
                        var toRemove = files.OrderBy(x => x.Name.EndsWith("kn5") ? 0 : 1)
                                .ThenBy(x => x.LastWriteTime)
                                .TakeUntil(0L, x => x > spaceToFree, (x, t) => t + x.Length).ToList();
                        totalSize -= toRemove.Where(x => FileUtils.TryToDelete(x.FullName)).Sum(x => x.Length);
                    }
                    ActionExtension.InvokeInMainThreadAsync(() => {
                        CacheInformationReady = totalSize > 0;
                        CacheFileSize = totalSize;
                    });
                }).ConfigureAwait(false);
            }

            private AsyncCommand _ClearCacheCommand;

            public AsyncCommand ClearCacheCommand => _ClearCacheCommand ?? (_ClearCacheCommand = new AsyncCommand(async () => {
                await Task.Run(() => {
                    var files = Directory.GetFiles(FilesStorage.Instance.GetTemporaryDirectory(TemporaryDirectoryName))
                            .Where(x => x.EndsWith(".kn5") || x.EndsWith(".fbx"));
                    foreach (var fileInfo in files) {
                        FileUtils.TryToDelete(fileInfo);
                    }
                });
                await ScanCacheAsync();
            }));

            public ViewModel(CarObject target) {
                Car = target;

                PresetsManager.Instance.ClearBuiltInPresets(PresetableCategory);
                PresetsManager.Instance.RegisterBuiltInPreset(new byte[0], PresetableCategory, "Default");

                if (File.Exists(CarSettingsFilename)) {
                    try {
                        PresetsManager.Instance.RegisterBuiltInPreset(File.ReadAllBytes(CarSettingsFilename), PresetableCategory, PreviousSettingsPresetsName);
                        HasPreviousSettings = true;
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                }

                var definitions = LoadCommonDefinitions();
                UserDefinedValues.ReplaceEverythingBy_Direct((definitions[@"userDefined"] as JObject)?
                        .Select((KeyValuePair<string, JToken> x) => Tuple.Create(x.Key, x.Value as JObject))
                        .Where(x => x.Item2 != null)
                        .Select(x => new UserDefinedValue(x.Item1, x.Item2)) ?? new UserDefinedValue[0]);
                UserDefinedValues.ItemPropertyChanged += OnUserDefinedValuePropertyChanged;
                UpdateUserDefined();

                Stages = new ChangeableObservableCollection<StageParams>(
                        FilesStorage.Instance.GetContentFilesFiltered(@"Stage.*.json", ContentCategory.CarLodsGeneration)
                                .Select(x => new StageParams(x.Filename, definitions, _userDefined)));
                Stages.ItemPropertyChanged += OnStagePropertyChanged;
                if (Stages.Count == 0) {
                    throw new InformativeException("Stage defitions are missing", "Make sure to have latest app data installed.");
                }

                LodDefinitions = new ChangeableObservableCollection<CustomShowroomLodDefinition>(LoadBaseLodDefinitions());
                LodDefinitions.ItemPropertyChanged += OnLodDefinitionPropertyChanged;
                RefreshLodDetails();

                SimplygonLocation.PropertyChanged += (s, e) => CheckSimplygonLocation();
                MonitorLocation().Ignore();

                for (var i = 0; i < Stages.Count; ++i) {
                    Stages[i].AddLodDefinition(LodDefinitions.ElementAtOrDefault(i));
                }

                ScanForPotentialIssues();
                FilesStorage.Instance.Watcher(ContentCategory.CarLodsGeneration).Update += OnDataUpdate;

                _saveable = new SaveHelper<SaveableData>("_carLodGenerator", () => new SaveableData {
                    UserDefinedValues = UserDefinedValues.ToDictionary(x => x.Key, x => x.Value),
                    Stages = Stages.ToDictionary(x => x.Id, x => x.Serialize())
                }, o => {
                    foreach (var value in UserDefinedValues) {
                        var data = o?.UserDefinedValues?.GetValueOrDefault(value.Key);
                        value.Value = data;
                    }

                    foreach (var stage in Stages) {
                        var data = o?.Stages?.GetValueOrDefault(stage.Id);
                        if (data != null) {
                            stage.Deserialize(data);
                        } else {
                            stage.RefreshStage();
                        }
                    }
                });
                _saveable.Initialize();
                ScanCacheAsync().Ignore();
            }

            private void OnLodDefinitionPropertyChanged(object s, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(CustomShowroomLodDefinition.IsSelected) && s is CustomShowroomLodDefinition definition && definition.IsSelected) {
                    foreach (var stage in Stages) {
                        if (stage.GeneratedModels.Contains(definition)) {
                            stage.SelectedGeneratedModel = definition;
                        }
                    }
                }
            }

            #region Presetable
            public bool CanBeSaved => true;
            public PresetsCategory PresetableCategory => PresetableKeyCategory;
            string IUserPresetable.PresetableKey => PresetableKey;

            public string ExportToPresetData() {
                return _saveable.ToSerializedString();
            }

            public event EventHandler Changed;

            public void ImportFromPresetData(string data) {
                _saveable.FromSerializedString(data);
            }

            private ICommand _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

            private bool _hasPreviousSettings;

            public bool HasPreviousSettings {
                get => _hasPreviousSettings;
                set => Apply(value, ref _hasPreviousSettings);
            }

            private DelegateCommand _openDirectoryCommand;

            public DelegateCommand OpenDirectoryCommand
                => _openDirectoryCommand ?? (_openDirectoryCommand = new DelegateCommand(() => WindowsHelper.ViewDirectory(Car.Location)));

            private async Task Share() {
                var data = ExportToPresetData();
                if (data == null) return;
                await SharingUiHelper.ShareAsync(SharedEntryType.CarLodsGenerationPreset,
                        Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(PresetableKey)), null, data);
            }
            #endregion

            private void UpdateSaveable() {
                if (_saveable.SaveLater() && LoadedWindow != null) {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }

            private void OnStagePropertyChanged(object s, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(StageParams.SelectedGeneratedModel)) {
                    UpdateHasDataToSave();
                } else if (StageParams.IsSavedProperty(e.PropertyName)) {
                    UpdateSaveable();
                }
            }

            private void OnUserDefinedValuePropertyChanged(object s, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(UserDefinedValue.Value)) {
                    UpdateUserDefined();
                    UpdateSaveable();
                }
            }

            private void OnDataUpdate(object sender, EventArgs e) {
                var data = _saveable.ToSerializedString();
                var definitions = LoadCommonDefinitions();
                UserDefinedValues.ReplaceEverythingBy_Direct((definitions[@"userDefined"] as JObject)?
                        .Select((KeyValuePair<string, JToken> x) => Tuple.Create(x.Key, x.Value as JObject))
                        .Where(x => x.Item2 != null)
                        .Select(x => new UserDefinedValue(x.Item1, x.Item2)) ?? new UserDefinedValue[0]);
                var newStages = FilesStorage.Instance.GetContentFilesFiltered(@"Stage.*.json", ContentCategory.CarLodsGeneration)
                        .Select(x => {
                            var existing = Stages.GetByIdOrDefault(StageParams.GetId(x.Filename));
                            if (existing != null) {
                                existing.RefreshStage();
                                return existing;
                            }
                            return new StageParams(x.Filename, definitions, _userDefined);
                        })
                        .ToList();
                if (newStages.Count == 0) {
                    NonfatalError.Notify("Stage definitions are missing", "Make sure to have latest app data installed.");
                    Finished?.Invoke(this, EventArgs.Empty);
                }
                Stages.ReplaceEverythingBy(newStages);
                if (data != null) {
                    _saveable.FromSerializedStringWithoutSaving(data);
                }
                UpdateUserDefined();
            }

            [CanBeNull]
            private PlotModel CollectStats([CanBeNull] StageParams stage, IKn5 kn5) {
                if (stage == null || kn5 == null || Car.AcdData == null) return null;

                var filterContext = new Kn5NodeFilterContext(stage.Stage.DefinitionsData, _userDefined, Car.Location, Car.AcdData, kn5);
                var statGroups = (stage.Stage.DefinitionsData[@"statGroups"] as JArray)?
                        .OfType<JObject>()
                        .Select(x => new {
                            Name = x.GetStringValueOnly("name"),
                            Filter = filterContext.CreateFilter(x.GetStringValueOnly("filter") ?? string.Empty)
                        })
                        .Where(x => x.Name != null)
                        .ToList();

                var groups = new Dictionary<string, int>();
                Iterate(kn5.RootNode, "Rest");

                var color = Application.Current.MainWindow?.ToOxyColor(@"WindowText") ?? Colors.White.ToOxyColor();
                return groups.Count == 0 ? null : new PlotModel {
                    TextColor = color,
                    Background = Colors.Transparent.ToOxyColor(),
                    Series = {
                        new PieSeriesExt {
                            FontSize = 10d,
                            AreInsideLabelsAngled = false,
                            StrokeThickness = 0,
                            InsideLabelPosition = 0.8d,
                            AngleSpan = 360,
                            StartAngle = 180,
                            InsideLabelColor = Colors.White.ToOxyColor(),
                            TextColor = color,
                            Diameter = 0.8,
                            InnerDiameter = 0.4,
                            Slices = groups.OrderBy(x => x.Key).Select((x, i) => new PieSlice(x.Key, x.Value) {
                                Fill = GetSliceColor(GetIndex(x.Key)).ToOxyColor()
                            }).ToList()
                        }
                    }
                };

                int GetIndex(string key) {
                    return key.Sum(x => x);
                }

                Color GetSliceColor(int index) {
                    var colors = new[] {
                        Colors.SeaGreen,
                        Colors.Peru,
                        Colors.SteelBlue,
                        Colors.Brown,
                        Colors.DarkBlue,
                        Colors.DarkGoldenrod,
                        Colors.Indigo,
                        Colors.LightSeaGreen,
                        Colors.DarkMagenta,
                        Colors.OrangeRed,
                        Colors.PowderBlue,
                    };
                    return colors[index % colors.Length];
                }

                void Iterate(Kn5Node node, string group) {
                    group = statGroups.FirstOrDefault(x => x.Filter.Test(node))?.Name ?? group;
                    if (node.NodeClass == Kn5NodeClass.Mesh || node.NodeClass == Kn5NodeClass.SkinnedMesh) {
                        groups[group] = groups.GetValueOrDefault(group) + node.Indices.Length / 3;
                    } else {
                        foreach (var child in node.Children.Where(child => child.Active)) {
                            Iterate(child, group);
                        }
                    }
                }
            }

            private void CheckAvailability(IKn5 kn5) {
                foreach (var stage in Stages) {
                    var requiredNode = stage.Stage.InlineGeneration?.Source;
                    Kn5Node node;
                    if (requiredNode == null || (node = kn5.FirstByName(requiredNode))?.TotalTrianglesCount > 0) {
                        stage.IsAvailable = true;
                        stage.UnavailableReason = null;
                    } else if (node == null) {
                        stage.IsAvailable = false;
                        stage.UnavailableReason = $"Hi-res {stage.Stage.InlineGeneration.Description?.ToSentenceMember() ?? "?"} is missing";
                    } else {
                        stage.IsAvailable = false;
                        stage.UnavailableReason = $"Hi-res {stage.Stage.InlineGeneration.Description?.ToSentenceMember() ?? "?"} is empty";
                    }
                }
                IsInitialCheckComplete = true;
            }

            private void RefreshLodDetails() {
                LodDefinitions.ReplaceIfDifferBy(LodDefinitions.OrderBy(x => x.LodIndex).ThenBy(x => x.Order));
                var itemsToFill = LodDefinitions.Where(x => x.Details == null).ToList();
                if (itemsToFill.Count == 0) return;

                Task.Run(() => {
                    foreach (var item in itemsToFill) {
                        string message;
                        try {
                            var stage = Stages[item.LodIndex];
                            var kn5 = Kn5.FromFile(item.Filename, SkippingTextureLoader.Instance);

                            if (!_availabilityChecked) {
                                _availabilityChecked = true;
                                ActionExtension.InvokeInMainThreadAsync(() => CheckAvailability(kn5));
                            }

                            if (stage.Stage.InlineGeneration?.Source != null) {
                                var cockpitHr = kn5.FirstByName(stage.Stage.InlineGeneration.Source);
                                var cockpitLr = kn5.FirstByName(stage.Stage.InlineGeneration.Destination);
                                if (cockpitLr == null) {
                                    message = cockpitHr != null ? $"Hi-res only: {CalculateStats(cockpitHr)}"
                                            : $"{stage.Stage.InlineGeneration.Description} is missing";
                                } else {
                                    message = $"Low-res {stage.Stage.InlineGeneration.Description?.ToSentenceMember() ?? "?"}: {CalculateStats(cockpitLr)}";
                                }
                            } else {
                                message = CalculateStats(kn5.RootNode);
                            }
                        } catch (Exception e) {
                            Logging.Warning(e);
                            message = "<Failed to get data>";

                            if (!_availabilityChecked) {
                                ActionExtension.InvokeInMainThreadAsync(() => {
                                    foreach (var stage in Stages) {
                                        stage.IsAvailable = false;
                                        stage.UnavailableReason = $"Failed to read original model";
                                    }
                                    IsInitialCheckComplete = true;
                                });
                            }
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

            private string CollectDistributionStats(IKn5 kn5) {
                var sb = new StringBuilder();
                sb.Append("Materials:\n");
                foreach (var material in kn5.Materials.Values.Select((x, i) =>
                        new { x, t = kn5.Nodes.Where(y => y.NodeClass != Kn5NodeClass.Base && y.MaterialId == i).Sum(y => y.TotalTrianglesCount) })
                        .Where(x => x.t > 0)
                        .OrderByDescending(x => x.t)) {
                    sb.Append($"• {material.x.Name}: shader: {material.x.ShaderName}, [color=#ffff00]{material.t}[/color] triangles\n");
                }

                sb.Append("\nNodes:\n");
                sb.Append(CollectNodeStats(kn5.RootNode, 0).Item1);
                return sb.ToString();

                Tuple<string, int> CollectNodeStats(Kn5Node node, int level, bool withNode = false) {
                    if (node.NodeClass != Kn5NodeClass.Base) {
                        var nodeType = (withNode ? "node & " : "") + (node.NodeClass == Kn5NodeClass.SkinnedMesh ? "skinned" : "mesh");
                        return Tuple.Create($"{new string(' ', 2 * level)}• {node.Name}: {nodeType}, "
                                + $"material: {kn5.GetMaterial(node.MaterialId)?.Name ?? @"?"}, "
                                + $"[color=#ffff00]{node.TotalTrianglesCount}[/color] triangles", node.TotalTrianglesCount);
                    }

                    if (node.Children.Count == 1 && node.Children[0].Name == node.Name && node.Children[0].NodeClass != Kn5NodeClass.Base) {
                        return CollectNodeStats(node.Children[0], level, true);
                    }

                    var s = new StringBuilder();
                    var t = 0;
                    foreach (var child in node.Children
                            .Select(x => CollectNodeStats(x, level + 1)).OrderByDescending(x => x.Item2).NonNull()) {
                        s.Append('\n').Append(child.Item1);
                        t += child.Item2;
                    }
                    if (t == 0) return null;
                    return Tuple.Create($"{new string(' ', 2 * level)}• {node.Name}: node, [color=#ffff00]{t}[/color] triangles{s}", t);
                }
            }

            private ICommand ViewLodDetailsFactory(IKn5 kn5) {
                return new DelegateCommand(() => MessageDialog.Show(CollectDistributionStats(kn5), "Triangles distribution", MessageDialogButton.OK));
            }

            private IEnumerable<CustomShowroomLodDefinition> LoadBaseLodDefinitions() {
                var ret = new List<CustomShowroomLodDefinition>();
                var lodsIni = Car.AcdData?.GetIniFile("lods.ini");
                if (lodsIni != null) {
                    ret.AddRange(lodsIni.GetSections("LOD")
                            .Select((x, i) => new { i, file = x.GetNonEmpty("FILE") })
                            .Select(section => new CustomShowroomLodDefinition {
                                DisplayName = "Original",
                                Filename = Path.Combine(Car.Location, section.file),
                                Order = section.i,
                                LodIndex = section.i,
                                UseCockpitLrByDefault = section.i == 0,
                                StatsFactory = kn5 => CollectStats(Stages.ElementAtOrDefault(section.i), kn5),
                                ViewDetailsFactory = ViewLodDetailsFactory
                            })
                            .Where(x => File.Exists(x.Filename)));
                }
                if (ret.Count == 0) {
                    ret.Add(new CustomShowroomLodDefinition {
                        DisplayName = "Original",
                        Filename = AcPaths.GetMainCarFilename(Car.Location, Car.AcdData, false),
                        Order = 0,
                        LodIndex = 0,
                        UseCockpitLrByDefault = true,
                        StatsFactory = kn5 => CollectStats(Stages.ElementAtOrDefault(0), kn5),
                        ViewDetailsFactory = ViewLodDetailsFactory
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

            public void CheckSimplygonLocation() {
                try {
                    SimplygonAvailable = !string.IsNullOrWhiteSpace(SimplygonLocation.Value) && File.Exists(SimplygonLocation.Value);
                } catch (Exception e) {
                    Logging.Warning(e);
                    SimplygonAvailable = false;
                }
            }

            private bool _simplygonAvailable;

            public bool SimplygonAvailable {
                get => _simplygonAvailable;
                set => Apply(value, ref _simplygonAvailable, () => _generateCommand?.RaiseCanExecuteChanged());
            }

            private bool _isInitialCheckComplete;

            public bool IsInitialCheckComplete {
                get => _isInitialCheckComplete;
                set => Apply(value, ref _isInitialCheckComplete);
            }

            private AsyncCommand<StageParams> _viewResultCommand;

            public AsyncCommand<StageParams> OpenShowroomCommand => _viewResultCommand ?? (_viewResultCommand = new AsyncCommand<StageParams>(async p => {
                await CustomShowroomWrapper.StartAsync(Car,
                        p?.SelectedGeneratedModel?.Filename ?? AcPaths.GetMainCarFilename(Car.Location, false),
                        LodDefinitions);
            }));

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

            public void Dispose() {
                FilesStorage.Instance.Watcher(ContentCategory.CarLodsGeneration).Update -= OnDataUpdate;
                _disposed = true;

                var temporaryDirectory = FilesStorage.Instance.GetTemporaryDirectory(TemporaryDirectoryName);
                var filesToDelete = LodDefinitions.Where(x => x.IsGenerated && x.Filename != null
                        && FileUtils.IsAffectedBy(x.Filename, temporaryDirectory)).Select(x => x.Filename).ToList();
                Task.Delay(TimeSpan.FromSeconds(3d)).ContinueWith(r => Task.Run(() => {
                    foreach (var filename in filesToDelete) {
                        FileUtils.TryToDelete(filename);
                    }
                }));
            }

            private IKn5 _mainKn5;

            [NotNull]
            private IKn5 LoadMainKn5() {
                if (_mainKn5 == null) {
                    var kn5Filename = AcPaths.GetMainCarFilename(Car.Location, Car.AcdData, false);
                    try {
                        _mainKn5 = kn5Filename != null && File.Exists(kn5Filename)
                                ? Kn5.FromFile(kn5Filename, SkippingTextureLoader.Instance) : Kn5.CreateEmpty();
                    } catch (Exception e) {
                        Logging.Warning(e);
                        _mainKn5 = Kn5.CreateEmpty();
                    }
                }
                return _mainKn5;
            }

            private IEnumerable<string> CollectPotentialIssues() {
                if (Car.AcdData == null) {
                    return new[] { "Failed to access car data" };
                }

                if (LoadMainKn5().RootNode.Children.Count == 0) {
                    return new[] { "Car model is missing or damaged" };
                }

                var definitions = Stages.FirstOrDefault()?.Stage.DefinitionsData;
                if (definitions == null) {
                    return new string[0];
                }

                var filterContext = new Kn5NodeFilterContext(definitions, _userDefined, Car.Location, Car.AcdData, LoadMainKn5());
                return (definitions[@"issueChecks"] as JArray)?
                        .Select(x => new { x, filter = filterContext.CreateFilter(x.GetStringValueOnly("filter") ?? string.Empty) })
                        .Where(x => IssueDetectTriggerFactory.Create(x.x.GetStringValueOnly("trigger") ?? string.Empty).Test(_mainKn5.Nodes.Where(x.filter.Test)
                                .SelectMany(y => y.NodeClass == Kn5NodeClass.Base
                                        ? y.AllChildren().Where(z => z.NodeClass != Kn5NodeClass.Base) : new[] { y })
                                .Distinct().ToList()))
                        .Select(x => x.x.GetStringValueOnly("name") ?? @"?") ?? new string[0];
            }

            private static readonly FilterFactory<List<Kn5Node>> IssueDetectTriggerFactory = FilterFactory.Create<List<Kn5Node>>(
                    (obj, key, value) => {
                        switch (key) {
                            case "meshes":
                                return value.Test(obj.Count);
                            case "triangles":
                                return value.Test(obj.Sum(x => x.TotalTrianglesCount));
                            case "materials":
                                return value.Test(obj.Select(x => x.MaterialId).Distinct().Count());
                            case null:
                                return obj.Any(x => value.Test(x.Name));
                            default:
                                return false;
                        }
                    },
                    FilterParams.DefaultStrictNoChildKeys);

            public BetterObservableCollection<string> PotentialIssues { get; } = new BetterObservableCollection<string>();

            public string DisplayPotentialIssues => PotentialIssues.Count > 0
                    ? $"Potential {PluralizingConverter.Pluralize(PotentialIssues.Count, "issue")} detected:\n{PotentialIssues.Select(x => $"• {x}").JoinToString(";\n")}"
                    : null;

            private Busy _potentialIssuesBusy = new Busy();

            private void ScanForPotentialIssues() {
                _potentialIssuesBusy.TaskDelay(async () => {
                    var issues = await Task.Run(() => CollectPotentialIssues().ToList());
                    PotentialIssues.ReplaceEverythingBy(issues);
                    OnPropertyChanged(nameof(DisplayPotentialIssues));
                }, 500);
            }

            private bool _warningShownOnce;

            private async Task GenerateAsync([CanBeNull] IProgress<double> progress, CancellationToken cancellationToken) {
                try {
                    Kn5.FbxConverterLocation = PluginsManager.Instance.GetPluginFilename(KnownPlugins.FbxConverter, "FbxConverter.exe");
                    if (!File.Exists(Kn5.FbxConverterLocation)) {
                        throw new Exception("FbxConverter is not available");
                    }

                    if (Car.AcdData != null && !_warningShownOnce) {
                        var issues = await Task.Run(() => CollectPotentialIssues().ToList());
                        if (issues.Count > 0) {
                            if (MessageDialog.Show($@"Potential {PluralizingConverter.Pluralize(issues.Count, "issue")} detected:
{issues.Select(x => $"• {x}").JoinToString(";\n")}.

Resulting LODs quality gets noticeably better if mesh roles are taken into account. For example, interior meshes can be optimized much further without major visual impact, while car paint meshes need to remain more detailed. {(UserDefinedValues
        .All(x => x.Value == null)
        ? "Generator tries to guess those roles automatically, but some cars are made differently, so you might want to help it by specifying filters manually."
        : "")}

Would you like to continue as is?", "Warning", new MessageDialogButton {
    [MessageBoxResult.Yes] = "Yes, ignore and continue",
    [MessageBoxResult.No] = "No, go back"
}, "carLodGeneratorWarning") != MessageBoxResult.Yes) {
                                return;
                            }
                            _warningShownOnce = true;
                        }
                    }

                    foreach (var stage in Stages.Where(x => x.IsAvailableAndActive)) {
                        stage.GenerationProgress = 0d;
                        stage.GenerationErrorMessage = null;
                        stage.GeneratingNow = true;
                    }

                    var stagesAll = Stages.ToArray();
                    if (Car.AcdData?.GetIniFile("lods.ini").GetSections("LOD").Count() > stagesAll.Length) {
                        throw new Exception($"Unsupported LODs arrangement, {stagesAll.Length} LODs required");
                    }

                    using (var taskbarProgress = TaskbarService.Create("Generating LODs", 1e5)) {
                        taskbarProgress?.Set(TaskbarState.Normal, 0.001d);
                        var generator = new CarLodGenerator(Stages.Where(x => x.IsAvailableAndActive).Select(x => x.Stage),
                                new CarLodSimplygonService(SimplygonLocation.Value, Stages), Car.Location,
                                FilesStorage.Instance.GetTemporaryDirectory(TemporaryDirectoryName));
                        var totalStages = Stages.Count(x => x.IsAvailableAndActive);
                        var initialStates = Stages.Select(x => new { Stage = x, x.IsAvailableAndActive, x.ApplyWeldingFix }).ToList();
                        await generator.RunAsync(
                                (key, exception) =>
                                        ActionExtension.InvokeInMainThreadAsync(() => Stages.GetById(key).GenerationErrorMessage = exception.Message),
                                (key, filename, checksum) => ActionExtension.InvokeInMainThreadAsync(() => {
                                    var stage = Stages.GetById(key);
                                    stage.GeneratingNow = false;

                                    var existing = LodDefinitions.FirstOrDefault(x => x.Filename == filename || x.Checksum == checksum);
                                    if (existing != null) {
                                        if (!File.Exists(existing.Filename) && File.Exists(filename)) {
                                            existing.Filename = filename;
                                        } else {
                                            FileUtils.TryToDelete(filename);
                                        }
                                        stage.SelectedGeneratedModel = existing;
                                    } else {
                                        var index = Stages.IndexOf(stage);
                                        var generatedCount = LodDefinitions.Count(x => x.LodIndex == index && x.IsGenerated);
                                        var countPostfix = generatedCount == 0 ? "" : $", {generatedCount + 1}";
                                        var generatedName = $"Generated ({(initialStates[index].ApplyWeldingFix ? "welding" : "no welding")}{countPostfix})";
                                        var definition = new CustomShowroomLodDefinition {
                                            DisplayName = generatedName,
                                            Filename = filename,
                                            Order = 10 * (generatedCount + 1) + index,
                                            UseCockpitLrByDefault = stage.Stage.InlineGeneration?.Source == "COCKPIT_HR",
                                            LodIndex = index,
                                            StatsFactory = kn5 => CollectStats(stage, kn5),
                                            ViewDetailsFactory = ViewLodDetailsFactory,
                                            Checksum = checksum
                                        };
                                        LodDefinitions.Add(definition);
                                        stage.AddLodDefinition(definition);
                                        RefreshLodDetails();
                                    }
                                }),
                                new Progress<CarLodGeneratorProgressUpdate>(msg => {
                                    var stage = Stages.GetById(msg.Key);
                                    if (stage.GenerationProgress != 1d) {
                                        if (msg.Value.HasValue) stage.GenerationProgress = msg.Value.Value;
                                        var progressValue = initialStates.Where(x => x.IsAvailableAndActive)
                                                .Sum(x => x.Stage.GenerationProgress) / totalStages;
                                        progress?.Report(progressValue);
                                        taskbarProgress?.Set(TaskbarState.Normal, progressValue);
                                    }
                                }), cancellationToken);
                    }
                } catch (Exception e) {
                    if (!e.IsCancelled()) {
                        NonfatalError.Notify("Failed to generate LODs", e);
                    }
                } finally {
                    foreach (var stage in Stages) {
                        stage.GeneratingNow = false;
                    }
                }
            }

            private void UpdateHasDataToSave() {
                HasDataToSave = Stages.Any(x => x.SelectedGeneratedModel?.IsGenerated == true);
            }

            private bool _hasDataToSave;

            public bool HasDataToSave {
                get => _hasDataToSave;
                set => Apply(value, ref _hasDataToSave, () => _finalSaveCommand?.RaiseCanExecuteChanged());
            }

            private AsyncCommand<Tuple<IProgress<double>, CancellationToken>> _generateCommand;

            public AsyncCommand<Tuple<IProgress<double>, CancellationToken>> GenerateCommand => _generateCommand ?? (_generateCommand
                    = new AsyncCommand<Tuple<IProgress<double>, CancellationToken>>(t => GenerateAsync(t.Item1, t.Item2),
                            t => SimplygonAvailable));

            public async Task ViewDebugModelAsync(StageParams stage) {
                var debugFilename = FilesStorage.Instance.GetTemporaryFilename(TemporaryDirectoryName, "debug.kn5");
                await new CarLodGenerator(new[] { stage.Stage },
                        new CarLodSimplygonService(SimplygonLocation.Value, Stages), Car.Location,
                        FilesStorage.Instance.GetTemporaryDirectory(TemporaryDirectoryName)).SaveInputModelAsync(stage.Stage, debugFilename);
                await CustomShowroomWrapper.StartAsync(Car, debugFilename);
            }

            private static bool Warn(CarObject car) {
                if (ShowMessage(
                        "LOD generator needs to save updated “lods.ini” to apply changes, but doing so will cause online integrity check to fail for this car. Should it continue? Select “No” and updated file instead will be saved in data folder for manual replacement.",
                        "You’re about to modify car’s data", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return false;
                DataUpdateWarning.BackupData(car);
                return true;
            }

            private void NormalizeLods(IniFile file) {
                var prevDistance = 0d;
                var list = new List<Tuple<double, IniFileSection>>();
                foreach (var l in file.Where(f => Regex.IsMatch(f.Key, @"^LOD_\d+$")).ToList()) {
                    file.Remove(l.Key);
                    if (l.Value.ContainsKey("FILE") && FlexibleParser.TryParseDouble(l.Value.GetNonEmpty("IN"), out var inValue)
                            && !list.Any(x => Math.Abs(x.Item1 - inValue) < 0.1)) {
                        prevDistance = Math.Max(prevDistance, l.Value.GetDouble("OUT", 0d));
                        list.Add(Tuple.Create(inValue, l.Value));
                    }
                }
                foreach (var l in list.OrderByDescending(x => x.Item1).Select((x, i) => new { x, i = list.Count - 1 - i })) {
                    var section = file[$"LOD_{l.i}"] = l.x.Item2;
                    section.Set("IN", l.i == 0 ? 0d : l.x.Item1);
                    section.Set("OUT", prevDistance);
                    prevDistance = l.x.Item1;
                }
            }

            private string CarSettingsFilename => Path.Combine(Car.Location, @"ui", @"cm_lods_generation.json");

            private async Task SaveChangesAsync() {
                try {
                    var data = _saveable.ToSerializedString();
                    if (!string.IsNullOrWhiteSpace(data)) {
                        FileUtils.EnsureFileDirectoryExists(CarSettingsFilename);
                        File.WriteAllText(CarSettingsFilename, data);
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }

                var carData = Car.AcdData;
                var modelOriginal = AcPaths.GetMainCarFilename(Car.Location, carData, false);
                var realLodsIni = carData?.GetIniFile("lods.ini") ?? new IniFile();
                var alteredLodsIni = realLodsIni.Clone();
                var lodsIniNeedsSaving = false;
                foreach (var stage in Stages) {
                    if (stage.SelectedGeneratedModel?.IsGenerated == true) {
                        var baseName = Path.GetFileNameWithoutExtension(modelOriginal);
                        lodsIniNeedsSaving |= alteredLodsIni.Merge(IniFile.Parse(string.Format(stage.Stage.ConfigSectionFormat, baseName)));
                    }
                }

                if (lodsIniNeedsSaving) {
                    NormalizeLods(alteredLodsIni);
                    var savePacked = Car.AcdData?.IsPacked == true && Warn(Car);
                    if (savePacked) {
                        realLodsIni.SetFrom(alteredLodsIni);
                    }

                    await Task.Run(() => {
                        if (savePacked) {
                            realLodsIni.Save(true);
                        } else {
                            var destination = Path.Combine(Car.Location, @"data", @"lods.ini");
                            FileUtils.EnsureFileDirectoryExists(destination);
                            alteredLodsIni.Save(destination, true);
                        }
                    });
                }

                await Task.Run(() => {
                    for (var index = 0; index < Stages.Count; index++) {
                        var stage = Stages[index];
                        var filename = stage.SelectedGeneratedModel?.Filename;
                        if (stage.SelectedGeneratedModel?.IsGenerated != true || filename == null) continue;

                        var pieces = stage.Stage.ModelNamePath.Split('/');
                        var name = alteredLodsIni[pieces[0]].GetNonEmpty(pieces.ElementAtOr(1, ""));
                        if (name == null) {
                            Logging.Warning($"Unexpected LODs arrangement, using fallback name");
                            name = $@"_lod_gen_{index}.kn5";
                        }

                        var destination = Path.Combine(Car.Location, name);
                        using (var replacement = FileUtils.RecycleOriginal(destination)) {
                            FileUtils.Move(filename, replacement.Filename);
                        }
                    }
                });
            }

            public event EventHandler Finished;

            private AsyncCommand _finalSaveCommand;

            public AsyncCommand FinalSaveCommand => _finalSaveCommand ?? (_finalSaveCommand = new AsyncCommand(async () => {
                try {
                    await SaveChangesAsync();
                    Finished?.Invoke(this, EventArgs.Empty);
                } catch (Exception e) {
                    NonfatalError.Notify("Failed to save LODs", e);
                }
            }, () => HasDataToSave));

            public class CarLodSimplygonService : ICarLodGeneratorService {
                private readonly string _simplygonExecutable;
                private readonly IReadOnlyList<StageParams> _stages;

                public CarLodSimplygonService(string simplygonExecutable, IReadOnlyList<StageParams> stages) {
                    _simplygonExecutable = simplygonExecutable;
                    _stages = stages;
                }

                private static async Task RunProcessAsync(string filename, [Localizable(false)] IEnumerable<string> args, bool checkErrorCode,
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

                        var errorData = new StringBuilder();
                        process.ErrorDataReceived += (sender, eventArgs) => errorData.Append(eventArgs.Data);
                        process.BeginErrorReadLine();

                        if (progress != null) {
                            process.OutputDataReceived += (sender, eventArgs) => progress.Report(eventArgs.Data.As<double?>() / 100d);
                            process.BeginOutputReadLine();
                        }

                        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                        if (checkErrorCode && process.ExitCode != 0) {
                            var errorMessage = errorData.ToString().Trim();
                            if (string.IsNullOrEmpty(errorMessage)) {
                                errorMessage = $@"Failed to run process: {process.ExitCode}";
                            } else {
                                var separator = errorMessage.LastIndexOf(@": ", StringComparison.Ordinal);
                                if (separator != -1) {
                                    errorMessage = errorMessage.Substring(separator + 2);
                                }
                            }
                            throw new Exception(errorMessage);
                        }
                    } finally {
                        if (!process.HasExitedSafe()) {
                            process.Kill();
                        }
                        process.Dispose();
                    }
                }

                public async Task<string> GenerateLodAsync(string stageId, string inputFile, string modelChecksum,
                        IProgress<double?> progress, CancellationToken cancellationToken) {
                    var stage = _stages.GetById(stageId);
                    var rulesFilename = stage.SimplygonConfigurationFilename;
                    string intermediateFilename = null;

                    try {
                        string cacheKey = null;
                        try {
                            var rules = JObject.Parse(File.ReadAllText(rulesFilename));
                            rules[@"Settings"][@"ReductionProcessor"][@"ReductionSettings"][@"ReductionTargetTriangleCount"] = stage.TrianglesCount;
                            rules[@"Settings"][@"ReductionProcessor"][@"RepairSettings"][@"UseWelding"] = stage.ApplyWeldingFix;
                            rules[@"Settings"][@"ReductionProcessor"][@"RepairSettings"][@"UseTJunctionRemover"] = stage.ApplyWeldingFix;

                            var rulesData = rules.ToString();
                            var combinedChecksum = (modelChecksum + rulesData).GetChecksum();
                            cacheKey = $@"simplygon:{combinedChecksum}";
                            var existing = CacheStorage.Get<string>(cacheKey);
                            if (existing != null && File.Exists(existing)) {
                                progress?.Report(1d);
                                return existing;
                            }

                            var newRulesFilename = FileUtils.EnsureUnique($@"{inputFile.ApartFromLast(@".fbx")}_rules.json");
                            File.WriteAllText(newRulesFilename, rulesData);
                            rulesFilename = newRulesFilename;
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }

                        // Simplygon can’t parse FBX generated from COLLADA by FbxConverter. But it can parse FBX generated from FBX from COLLADA! So, let’s
                        // convert it once more:
                        intermediateFilename = FileUtils.EnsureUnique($@"{inputFile.ApartFromLast(@".fbx")}_fixed.fbx");
                        await RunProcessAsync(Kn5.FbxConverterLocation, new[] { inputFile, intermediateFilename, "/sffFBX", "/dffFBX", "/f201300" },
                                true, null, cancellationToken).ConfigureAwait(false);

                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(0.01);

                        var outputFile = FileUtils.EnsureUnique($@"{inputFile.ApartFromLast(@".fbx")}_simplygon.fbx");
                        await RunProcessAsync(_simplygonExecutable, new[] {
                            "-Progress", rulesFilename, intermediateFilename, outputFile
                        }, false, progress.SubrangeDouble(0.01, 1d), cancellationToken).ConfigureAwait(false);
                        if (cacheKey != null) {
                            CacheStorage.Set(cacheKey, outputFile);
                        }
                        return outputFile;
                    } finally {
                        if (!stage.KeepTemporaryFiles) {
                            if (!FileUtils.ArePathsEqual(rulesFilename, stage.SimplygonConfigurationFilename)) FileUtils.TryToDelete(rulesFilename);
                            if (intermediateFilename != null) FileUtils.TryToDelete(intermediateFilename);
                        }
                    }
                }
            }
        }

        public static Task<bool> RunAsync(CarObject target) {
            try {
                var dialog = new CarGenerateLodsDialog(target);
                dialog.ShowDialog();
                return Task.FromResult(dialog.IsResultOk);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t generate LODs", e);
                return Task.FromResult(false);
            }
        }

        private void OnShowroomClick(object sender, RoutedEventArgs e) {
            if ((sender as FrameworkElement)?.DataContext is StageParams stageParams) {
                Model.OpenShowroomCommand.ExecuteAsync(stageParams).Ignore();
            }
        }

        private void OnViewPreparedModelClick(object sender, RoutedEventArgs e) {
            if ((sender as FrameworkElement)?.DataContext is StageParams stageParams) {
                Model.ViewDebugModelAsync(stageParams).Ignore();
            }
        }

        private void OnApplyPreviousSettingsClick(object sender, RoutedEventArgs e) {
            UserPresetsControl.LoadBuiltInPreset(PresetableKey, PreviousSettingsPresetsName);
        }
    }
}