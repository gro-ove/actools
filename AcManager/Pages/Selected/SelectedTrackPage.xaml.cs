using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Controls.UserControls;
using AcManager.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Pages.Lists;
using AcManager.Tools;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools.AiFile;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Selected {
    public partial class SelectedTrackPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<TrackObject> {
            public ViewModel([NotNull] TrackObject acObject) : base(acObject) {
                SelectedTrackConfiguration = acObject.SelectedLayout;
                InitializeSpecs();
            }

            [CanBeNull]
            private TrackObjectBase _selectedTrackConfiguration;

            [CanBeNull]
            public TrackObjectBase SelectedTrackConfiguration {
                get => _selectedTrackConfiguration;
                set {
                    if (value == null) return;
                    if (Equals(value, _selectedTrackConfiguration)) return;
                    _selectedTrackConfiguration?.UnsubscribeWeak(OnSelectedLayoutPropertyChanged);
                    _selectedTrackConfiguration = value;
                    OnPropertyChanged();

                    SelectedObject.SelectedLayout = value;
                    _trackMapUpdateCommand?.RaiseCanExecuteChanged();

                    value.SubscribeWeak(OnSelectedLayoutPropertyChanged);
                }
            }

            private void OnSelectedLayoutPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
                switch (propertyChangedEventArgs.PropertyName) {
                    case nameof(TrackObjectBase.AiLaneFastExists):
                        _trackMapUpdateCommand?.RaiseCanExecuteChanged();
                        _recalculateWidthCommand?.RaiseCanExecuteChanged();
                        _recalculatePitboxesCommand?.RaiseCanExecuteChanged();
                        break;
                }
            }

            private AsyncCommand _driveCommand;

            public AsyncCommand DriveCommand => _driveCommand ?? (_driveCommand = new AsyncCommand(async () => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !await QuickDrive.RunAsync(track: SelectedTrackConfiguration)) {
                    DriveOptionsCommand.Execute();
                }
            }, () => SelectedTrackConfiguration?.Enabled == true));

            private DelegateCommand _driveOptionsCommand;

            public DelegateCommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(
                    () => QuickDrive.Show(track: SelectedTrackConfiguration),
                    () => SelectedTrackConfiguration?.Enabled == true));

            public HierarchicalItemsView QuickDrivePresets {
                get => _quickDrivePresets;
                set => Apply(value, ref _quickDrivePresets);
            }

            private static HierarchicalItemsView _quickDrivePresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public override void Unload() {
                base.Unload();
                _helper.Dispose();
                _selectedTrackConfiguration?.UnsubscribeWeak(OnSelectedLayoutPropertyChanged);
            }

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(new PresetsCategory(QuickDrive.PresetableKeyValue),
                            p => QuickDrive.RunAsync(track: SelectedTrackConfiguration, presetFilename: p.VirtualFilename).Forget());
                }
            }

            private AsyncCommand _updatePreviewCommand;

            public AsyncCommand UpdatePreviewCommand => _updatePreviewCommand ??
                    (_updatePreviewCommand = new AsyncCommand(() => TrackPreviewsCreator.ShotAndApply(SelectedTrackConfiguration),
                            () => SelectedObject.Enabled));

            private AsyncCommand _updatePreviewDirectCommand;

            public AsyncCommand UpdatePreviewDirectCommand => _updatePreviewDirectCommand ??
                    (_updatePreviewDirectCommand = new AsyncCommand(() => TrackPreviewsCreator.ApplyExisting(SelectedTrackConfiguration)));

            private AsyncCommand<bool> _trackMapUpdateCommand;

            public AsyncCommand<bool> UpdateMapCommand => _trackMapUpdateCommand ?? (_trackMapUpdateCommand =
                    new AsyncCommand<bool>(v => TrackMapRendererWrapper.Run(SelectedTrackConfiguration, v),
                            v => !v || SelectedTrackConfiguration?.AiLaneFastExists == true));

            private AsyncCommand _bakeShadersCommand;

            public AsyncCommand BakeShadersCommand => _bakeShadersCommand ?? (_bakeShadersCommand = new AsyncCommand(async () => {
                try {
                    var fxc = FileRelatedDialogs.Open(new OpenDialogParams {
                        DirectorySaveKey = "fxclocation",
                        Filters = { new DialogFilterPiece("Shaders compiler", "fxc.exe"), DialogFilterPiece.Applications },
                        InitialDirectory = "C:/Program Files (x86)/Microsoft DirectX SDK (June 2010)/Utilities/bin/x64",
                        Title = "Select shaders compiler",
                        UseCachedIfAny = true
                    });
                    if (fxc == null) return;

                    var cbuffersCommon = FileRelatedDialogs.Open(new OpenDialogParams {
                        DirectorySaveKey = "cbuffersCommon",
                        Filters = { new DialogFilterPiece("CBuffers common file", "cbuffers_common.fx") },
                        Title = "Select cbuffers_common.fx from shaders repo"
                    });
                    if (cbuffersCommon == null) return;

                    using (var waiting = WaitingDialog.Create("Baking shaders…")) {
                        var cancellation = waiting.CancellationToken;
                        Logging.Here();

                        waiting.Report("Loading KN5s with list of materials…");
                        var materials = await Task.Run(() => {
                            return ((IEnumerable<TrackObjectBase>)SelectedObject.MultiLayouts ?? new[] { SelectedObject.MainTrackObject })
                                    .SelectMany(x => File.Exists(x.ModelsFilename)
                                            ? GetModelsNames(new IniFile(x.ModelsFilename)).Select(y => Path.Combine(x.Location, y))
                                            : new[] { Path.Combine(x.Location, x.Id + ".kn5") })
                                    .Distinct()
                                    .Where(File.Exists)
                                    .Select(x => cancellation.IsCancellationRequested ? null : Kn5.FromFile(x, SkippingTextureLoader.Instance))
                                    .NonNull()
                                    .SelectMany(x => x.Materials.Values)
                                    .GroupBy(x => x.Name)
                                    .Select(x => x.First())
                                    .ToList();
                        });
                        if (cancellation.IsCancellationRequested) return;

                        Logging.Debug("Materials: " + materials.Select(x => x.Name).JoinToString(", "));
                        var destination = Path.Combine(SelectedObject.MainTrackObject.Location, "cache");
                        FileUtils.EnsureDirectoryExists(destination);

                        var recreated = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(cbuffersCommon)));

                        var j = 0;
                        for (var i = 0; i < materials.Count; i++) {
                            var material = materials[i];
                            Logging.Debug("material: " + material.Name);
                            Logging.Debug("shader: " + material.ShaderName);

                            foreach (var property in material.ShaderProperties) {
                                if (property.Name == "ksEmissive") {
                                    Logging.Debug("property: " + property.Name + "=" + property.ValueC.JoinToString(", "));
                                } else {
                                    Logging.Debug("property: " + property.Name + "=" + property.ValueA);
                                }
                            }

                            var baked = $@"{recreated}\include_new\base\cbuffers_baked.fx";
                            File.WriteAllLines(baked, material.ShaderProperties.Select(x => {
                                if (x.Name == "ksEmissive") {
                                    return $"#define {x.Name} float3({x.ValueC.JoinToString(", ")})";
                                } else {
                                    return $"#define {x.Name} {x.ValueA}";
                                }
                            }));

                            foreach (var mode in new[] {
                                "MAIN_FX", "MAIN_NOFX", "SIMPLIFIED_FX", "SIMPLIFIED_NOFX"
                            }) {
                                foreach (var type in new[] {
                                    "ps", "vs"
                                }) {
                                    if (cancellation.IsCancellationRequested) return;
                                    waiting.Report(
                                            $"Compiling “{material.Name}” ({(type == "ps" ? "pixel shader" : "vertex shader")}, mode: {mode.ToLowerInvariant()})…",
                                            j++, materials.Count * 4 * 2);

                                    var shader = $@"{recreated}\{material.ShaderName}_{type}.fx";
                                    var compiled = Path.Combine(destination, $"{mode.ToLowerInvariant()}_{material.Name}_{type}.fxo");
                                    await ProcessExtension.Start(fxc, new[] {
                                        "/T", $"{type}_5_0", "/Ni", "/nologo", "/O3", "/E", "main", shader, "/Fo" + compiled, $"/DMODE_{mode}=1",
                                        "/DMODE_BAKED=1"
                                    }, new ProcessStartInfo {
                                        WindowStyle = ProcessWindowStyle.Hidden,
                                    }).WaitForExitAsync();
                                }
                                File.Copy($@"{AcRootDirectory.Instance.RequireValue}\system\shaders\win\{material.ShaderName}_meta.ini",
                                        Path.Combine(destination, $"{mode.ToLowerInvariant()}_{material.Name}_meta.ini"));
                            }
                        }
                    }

                    IEnumerable<string> GetModelsNames(IniFile file) {
                        return file.GetSections("MODEL").Concat(file.GetSections("DYNAMIC_OBJECT")).Select(x => x.GetNonEmpty("FILE")).NonNull();
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t load color grading texture", "Make sure it’s a volume DDS texture.", e);
                }
            }));

            private AsyncCommand<string> _outlineSettingsCommand;

            public AsyncCommand<string> OutlineSettingsCommand
                => _outlineSettingsCommand ?? (_outlineSettingsCommand = new AsyncCommand<string>(async layoutId => {
                    if (layoutId == null) {
                        await TrackOutlineRendererWrapper.Run(SelectedTrackConfiguration);
                    } else {
                        var layout = SelectedObject.GetLayoutByLayoutId(layoutId);
                        if (layout == null) return;

                        await TrackOutlineRendererWrapper.UpdateAsync(layout);
                    }
                }));

            private AsyncCommand<string> _updateOutlineCommand;

            public AsyncCommand<string> UpdateOutlineCommand => _updateOutlineCommand ?? (_updateOutlineCommand = new AsyncCommand<string>(async layoutId => {
                var layout = layoutId == null ? SelectedTrackConfiguration : SelectedObject.GetLayoutByLayoutId(layoutId);
                if (layout == null) return;

                await TrackOutlineRendererWrapper.UpdateAsync(layout);
            }));

            protected override void FilterExec(string type) {
                var cfg = SelectedTrackConfiguration;
                if (cfg == null) return;
                switch (type) {
                    case "author":
                        NewFilterTab(string.IsNullOrWhiteSpace(cfg.Author)
                                ? @"author-" : $@"author:{Filter.Encode(cfg.Author)}");
                        break;

                    case "country":
                        NewFilterTab(string.IsNullOrWhiteSpace(cfg.Country)
                                ? @"country-" : $@"country:{Filter.Encode(cfg.Country)}");
                        break;

                    case "city":
                        NewFilterTab(string.IsNullOrWhiteSpace(cfg.City)
                                ? @"city-" : $@"city:{Filter.Encode(cfg.City)}");
                        break;

                    case "year":
                        NewFilterTab(cfg.Year.HasValue ? $@"year:{cfg.Year}" : @"year-");
                        break;

                    case "decade":
                        if (!cfg.Year.HasValue) {
                            NewFilterTab(@"year-");
                        }

                        var start = (int)Math.Floor((cfg.Year ?? 0) / 10d) * 10;
                        NewFilterTab($@"year>{start - 1} & year<{start + 10}");
                        break;

                    case "length":
                        FilterDistance("length", cfg.SpecsLengthValue, roundTo: 0.1, range: 0.3);
                        break;

                    case "width":
                        FilterRange("width", cfg.SpecsWidth, postfix: "m");
                        break;

                    case "pits":
                        FilterRange("pits", cfg.SpecsPitboxes);
                        break;

                    case "driven":
                        FilterDistance("driven", cfg.TotalDrivenDistance, roundTo: 0.1, range: 0.3);
                        break;

                    case "notes":
                        NewFilterTab(SelectedObject.HasNotes ? @"notes+" : @"notes-");
                        break;
                }
            }

            private static int CountPits(IKn5 kn5) {
                return LinqExtension.RangeFrom().TakeWhile(x => kn5.RootNode.GetByName($"AC_PIT_{x}") != null).Count();
            }

            private static async Task<int> LoadAndCountPits(string kn5Filename) {
                if (!File.Exists(kn5Filename)) {
                    throw new InformativeException("Can’t count pits",
                            $"File “{FileUtils.GetRelativePath(kn5Filename, AcRootDirectory.Instance.RequireValue)}” not found.");
                }

                var kn5 = await Task.Run(() => Kn5.FromFile(kn5Filename, SkippingTextureLoader.Instance, SkippingMaterialLoader.Instance,
                        HierarchyOnlyNodeLoader.Instance)).ConfigureAwait(false);
                return CountPits(kn5);
            }

            private AsyncCommand _recalculatePitboxesCommand;

            public AsyncCommand RecalculatePitboxesCommand => _recalculatePitboxesCommand ?? (_recalculatePitboxesCommand = new AsyncCommand(async () => {
                var cfg = SelectedTrackConfiguration;
                if (cfg == null) return;
                try {
                    using (WaitingDialog.Create("Loading model…")) {
                        int value;

                        var modelsFilename = cfg.ModelsFilename;
                        if (!File.Exists(modelsFilename)) {
                            value = await LoadAndCountPits(Path.Combine(cfg.Location, cfg.Id + ".kn5"));
                        } else {
                            value = 0;
                            foreach (var kn5Filename in new IniFile(modelsFilename).GetSections("MODEL")
                                    .Select(x => x.GetNonEmpty("FILE"))
                                    .NonNull()
                                    .Select(x => Path.Combine(cfg.Location, x))) {
                                value += await LoadAndCountPits(kn5Filename);
                            }
                        }

                        cfg.SpecsPitboxes = SpecsFormat(AppStrings.TrackSpecs_Pitboxes_FormatTooltip, value);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t recalculate pitboxes", e);
                }
            }));

            private AsyncCommand _recalculateLengthCommand;

            public AsyncCommand RecalculateLengthCommand => _recalculateLengthCommand ?? (_recalculateLengthCommand = new AsyncCommand(async () => {
                var cfg = SelectedTrackConfiguration;
                if (cfg == null) return;
                try {
                    var filename = cfg.AiLaneFastFilename;
                    if (!File.Exists(filename)) {
                        throw new InformativeException("Can’t recalculate length", "AI fast lane file is missing");
                    }

                    using (WaitingDialog.Create("Loading AI lane…")) {
                        var length = (await Task.Run(() => AiSpline.FromFile(filename).CalculateLength())).ToString("F0");
                        cfg.SpecsLength = SpecsFormat(AppStrings.TrackSpecs_Length_FormatTooltip, length);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t recalculate length", e);
                }
            }, () => SelectedTrackConfiguration?.AiLaneFastExists == true));

            private AsyncCommand _recalculateWidthCommand;

            public AsyncCommand RecalculateWidthCommand => _recalculateWidthCommand ?? (_recalculateWidthCommand = new AsyncCommand(async () => {
                var cfg = SelectedTrackConfiguration;
                if (cfg == null) return;
                try {
                    var filename = cfg.AiLaneFastFilename;
                    if (!File.Exists(filename)) {
                        throw new InformativeException("Can’t recalculate width", "AI fast lane file is missing");
                    }

                    using (WaitingDialog.Create("Loading AI lane…")) {
                        var width = await Task.Run(() => AiSpline.FromFile(filename).CalculateWidth());
                        if (width.Item2 < 1f) {
                            throw new InformativeException("Can’t recalculate width", "It appears AI fast lane has no width");
                        }

                        var minWidth = width.Item1.ToString("F0");
                        var maxWidth = width.Item2.ToString("F0");
                        var value = minWidth == maxWidth ? maxWidth : $"{minWidth}–{maxWidth}";
                        cfg.SpecsWidth = SpecsFormat(AppStrings.TrackSpecs_Width_FormatTooltip, value);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t recalculate width", e);
                }
            }, () => SelectedTrackConfiguration?.AiLaneFastExists == true));

            private void InitializeSpecs() {
                var cfg = SelectedTrackConfiguration;
                if (cfg == null) return;
                RegisterSpec("length", AppStrings.TrackSpecs_Length_FormatTooltip, () => cfg.SpecsLength,
                        v => cfg.SpecsLength = v);
                RegisterSpec("width", AppStrings.TrackSpecs_Width_FormatTooltip, () => cfg.SpecsWidth,
                        v => cfg.SpecsWidth = v);
                RegisterSpec("pits", AppStrings.TrackSpecs_Pitboxes_FormatTooltip, () => cfg.SpecsPitboxes,
                        v => cfg.SpecsPitboxes = v);
            }

            private DelegateCommand _manageSkinsCommand;

            public DelegateCommand ManageSkinsCommand
                => _manageSkinsCommand ?? (_manageSkinsCommand = new DelegateCommand(() => { TrackSkinsListPage.Open(SelectedObject); }));

            private DelegateCommand _viewSkinsResultCommand;

            public DelegateCommand ViewSkinsResultCommand
                =>
                        _viewSkinsResultCommand
                                ?? (_viewSkinsResultCommand = new DelegateCommand(() => { WindowsHelper.ViewDirectory(SelectedObject.DefaultSkinDirectory); }));

            public AsyncCommand ClearStatsCommand => SelectedObject.ClearStatsCommand;
            public AsyncCommand ClearStatsAllCommand => SelectedObject.ClearStatsAllCommand;
        }

        protected override void OnVersionInfoBlockClick(object sender, MouseButtonEventArgs e) {
            /*if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new VersionInfoEditor(_model.SelectedTrackConfiguration).ShowDialog();
            }*/
            if (SelectedAcJsonObject.Author == AcCommonObject.AuthorKunos) {
                if (SelectedAcJsonObject?.Dlc != null) {
                    WindowsHelper.ViewInBrowser(SelectedAcJsonObject.Dlc.Url);
                }
                return;
            }

            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;

                if (Keyboard.Modifiers != ModifierKeys.Control) {
                    new ModernPopup {
                        Content = new PopupAuthor((ISelectedAcObjectViewModel)DataContext),
                        PlacementTarget = sender as UIElement,
                        StaysOpen = false
                    }.IsOpen = true;
                } else if (SelectedAcJsonObject.Url != null) {
                    WindowsHelper.ViewInBrowser(SelectedAcJsonObject.Url);
                }
            }
        }

        private void OnGeoTagsClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new TrackGeoTagsDialog(_model.SelectedTrackConfiguration).ShowDialog();
            }
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private TrackObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await TracksManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = TracksManager.Instance.GetById(_id);
        }

        public void Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.UpdatePreviewDirectCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Alt)),
                new InputBinding(_model.UpdateOutlineCommand, new KeyGesture(Key.U, ModifierKeys.Control)),
                new InputBinding(_model.OutlineSettingsCommand, new KeyGesture(Key.U, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.DriveOptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.FixFormatCommand, new KeyGesture(Key.F, ModifierKeys.Alt)),
                new InputBinding(_model.ManageSkinsCommand, new KeyGesture(Key.K, ModifierKeys.Control)),
            });
            InitializeComponent();
            this.AddWidthCondition(640).Add(SkinsColumn);
        }

        private ViewModel _model;

        private void ToolbarButtonQuickDrive_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }

        private void OnOutlineRightClick(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            var context = ((FrameworkElement)sender).DataContext;
            if (context is TrackObjectBase wrapper) {
                new ContextMenu {
                    Items = {
                        new MenuItem {
                            Header = "Update outline",
                            Command = _model.UpdateOutlineCommand,
                            CommandParameter = wrapper.LayoutId
                        }
                    }
                }.IsOpen = true;
            }
        }
    }
}