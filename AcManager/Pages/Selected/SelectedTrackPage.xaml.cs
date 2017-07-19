using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Pages.Lists;
using AcManager.Tools;
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

            private TrackObjectBase _selectedTrackConfiguration;

            public TrackObjectBase SelectedTrackConfiguration {
                get { return _selectedTrackConfiguration; }
                set {
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

            private CommandBase _driveCommand;

            public ICommand DriveCommand => _driveCommand ?? (_driveCommand = new DelegateCommand(() => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !QuickDrive.Run(track: SelectedTrackConfiguration)) {
                    DriveOptionsCommand.Execute(null);
                }
            }, () => SelectedTrackConfiguration.Enabled));

            private CommandBase _driveOptionsCommand;

            public ICommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(() => {
                QuickDrive.Show(track: SelectedTrackConfiguration);
            }, () => SelectedTrackConfiguration.Enabled));

            public HierarchicalItemsView QuickDrivePresets {
                get => _quickDrivePresets;
                set {
                    if (Equals(value, _quickDrivePresets)) return;
                    _quickDrivePresets = value;
                    OnPropertyChanged();
                }
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
                    QuickDrivePresets = _helper.Create(new PresetsCategory(QuickDrive.PresetableKeyValue), p => {
                        QuickDrive.RunPreset(p.Filename, track: SelectedTrackConfiguration);
                    });
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
                            v => !v || SelectedTrackConfiguration.AiLaneFastExists));

            private AsyncCommand<string> _outlineSettingsCommand;

            public AsyncCommand<string> OutlineSettingsCommand => _outlineSettingsCommand ?? (_outlineSettingsCommand = new AsyncCommand<string>(async layoutId => {
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
                switch (type) {
                    case "author":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedTrackConfiguration.Author)
                                ? @"author-" : $@"author:{Filter.Encode(SelectedTrackConfiguration.Author)}");
                        break;

                    case "country":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedTrackConfiguration.Country)
                                ? @"country-" : $@"country:{Filter.Encode(SelectedTrackConfiguration.Country)}");
                        break;

                    case "city":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedTrackConfiguration.City)
                                ? @"city-" : $@"city:{Filter.Encode(SelectedTrackConfiguration.City)}");
                        break;

                    case "year":
                        NewFilterTab(SelectedTrackConfiguration.Year.HasValue ? $@"year:{SelectedTrackConfiguration.Year}" : @"year-");
                        break;

                    case "decade":
                        if (!SelectedTrackConfiguration.Year.HasValue) {
                            NewFilterTab(@"year-");
                        }

                        var start = (int)Math.Floor((SelectedTrackConfiguration.Year ?? 0) / 10d) * 10;
                        NewFilterTab($@"year>{start - 1} & year<{start + 10}");
                        break;

                    case "length":
                        FilterRange("length", SelectedTrackConfiguration.SpecsLength);
                        break;

                    case "width":
                        FilterRange("width", SelectedTrackConfiguration.SpecsWidth);
                        break;

                    case "pits":
                        FilterRange("pits", SelectedTrackConfiguration.SpecsPitboxes);
                        break;

                    case "driven":
                        FilterDistance("driven", SelectedTrackConfiguration.TotalDrivenDistance, roundTo: 0.1, range: 0.3);
                        break;
                }
            }

            private static int CountPits(Kn5 kn5) {
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
                try {
                    using (WaitingDialog.Create("Loading model…")) {
                        int value;

                        var modelsFilename = SelectedTrackConfiguration.ModelsFilename;
                        if (!File.Exists(modelsFilename)) {
                            value = await LoadAndCountPits(Path.Combine(SelectedTrackConfiguration.Location, SelectedTrackConfiguration.Id + ".kn5"));
                        } else {
                            value = 0;
                            foreach (var kn5Filename in new IniFile(modelsFilename).GetSections("MODEL")
                                                                                   .Select(x => x.GetNonEmpty("FILE"))
                                                                                   .NonNull()
                                                                                   .Select(x => Path.Combine(SelectedTrackConfiguration.Location, x))) {
                                value += await LoadAndCountPits(kn5Filename);
                            }
                        }

                        SelectedTrackConfiguration.SpecsPitboxes = SpecsFormat(AppStrings.TrackSpecs_Pitboxes_FormatTooltip, value);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t recalculate pitboxes", e);
                }
            }));

            private AsyncCommand _recalculateLengthCommand;

            public AsyncCommand RecalculateLengthCommand => _recalculateLengthCommand ?? (_recalculateLengthCommand = new AsyncCommand(async () => {
                try {
                    var filename = SelectedTrackConfiguration.AiLaneFastFilename;
                    if (!File.Exists(filename)) {
                        throw new InformativeException("Can’t recalculate length", "AI fast lane file is missing");
                    }

                    using (WaitingDialog.Create("Loading AI lane…")) {
                        var length = (await Task.Run(() => AiLane.FromFile(filename).CalculateLength())).ToString("F0");
                        SelectedTrackConfiguration.SpecsLength = SpecsFormat(AppStrings.TrackSpecs_Length_FormatTooltip, length);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t recalculate length", e);
                }
            }, () => SelectedTrackConfiguration.AiLaneFastExists));

            private AsyncCommand _recalculateWidthCommand;

            public AsyncCommand RecalculateWidthCommand => _recalculateWidthCommand ?? (_recalculateWidthCommand = new AsyncCommand(async () => {
                try {
                    var filename = SelectedTrackConfiguration.AiLaneFastFilename;
                    if (!File.Exists(filename)) {
                        throw new InformativeException("Can’t recalculate width", "AI fast lane file is missing");
                    }

                    using (WaitingDialog.Create("Loading AI lane…")) {
                        var width = await Task.Run(() => AiLane.FromFile(filename).CalculateWidth());
                        if (width.Item2 < 1f) {
                            throw new InformativeException("Can’t recalculate width", "It appears AI fast lane has no width");
                        }

                        var minWidth = width.Item1.ToString("F0");
                        var maxWidth = width.Item2.ToString("F0");
                        var value = minWidth == maxWidth ? maxWidth : $"{minWidth}–{maxWidth}";
                        SelectedTrackConfiguration.SpecsWidth = SpecsFormat(AppStrings.TrackSpecs_Width_FormatTooltip, value);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t recalculate width", e);
                }
            }, () => SelectedTrackConfiguration.AiLaneFastExists));

            private void InitializeSpecs() {
                RegisterSpec("length", AppStrings.TrackSpecs_Length_FormatTooltip, () => SelectedTrackConfiguration.SpecsLength,
                        v => SelectedTrackConfiguration.SpecsLength = v);
                RegisterSpec("width", AppStrings.TrackSpecs_Width_FormatTooltip, () => SelectedTrackConfiguration.SpecsWidth,
                        v => SelectedTrackConfiguration.SpecsWidth = v);
                RegisterSpec("pits", AppStrings.TrackSpecs_Pitboxes_FormatTooltip, () => SelectedTrackConfiguration.SpecsPitboxes,
                        v => SelectedTrackConfiguration.SpecsPitboxes = v);
            }

            private DelegateCommand _manageSkinsCommand;

            public DelegateCommand ManageSkinsCommand => _manageSkinsCommand ?? (_manageSkinsCommand = new DelegateCommand(() => {
                TrackSkinsListPage.Open(SelectedObject);
            }));

            private DelegateCommand _viewSkinsResultCommand;

            public DelegateCommand ViewSkinsResultCommand => _viewSkinsResultCommand ?? (_viewSkinsResultCommand = new DelegateCommand(() => {
                WindowsHelper.ViewDirectory(SelectedObject.DefaultSkinDirectory);
            }));
        }

        protected override void OnVersionInfoBlockClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new VersionInfoEditor(_model.SelectedTrackConfiguration).ShowDialog();
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
            var wrapper = context as TrackObjectBase;
            if (wrapper == null) return;

            new ContextMenu {
                Items = {
                    new MenuItem {
                        Header = "Update Outline",
                        Command = _model.UpdateOutlineCommand,
                        CommandParameter = wrapper.LayoutId
                    }
                }
            }.IsOpen = true;
        }
    }
}
