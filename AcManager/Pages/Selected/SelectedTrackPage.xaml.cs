using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.About;
using AcManager.Controls;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;
using StringBasedFilter;

namespace AcManager.Pages.Selected {
    public partial class SelectedTrackPage : ILoadableContent, IParametrizedUriContent {
        public class ViewModel : SelectedAcObjectViewModel<TrackObject> {
            public ViewModel([NotNull] TrackObject acObject) : base(acObject) {
                SelectedTrackConfiguration = acObject.SelectedLayout;
            }

            private TrackObjectBase _selectedTrackConfiguration;

            public TrackObjectBase SelectedTrackConfiguration {
                get { return _selectedTrackConfiguration; }
                set {
                    if (Equals(value, _selectedTrackConfiguration)) return;
                    _selectedTrackConfiguration = value;
                    OnPropertyChanged();

                    SelectedObject.SelectedLayout = value;
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
                get { return _quickDrivePresets; }
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
            }

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(QuickDrive.PresetableKeyValue, p => {
                        QuickDrive.RunPreset(p.Filename, track: SelectedTrackConfiguration);
                    });
                }
            }

            private const string KeyUpdatePreviewMessageShown = "SelectTrackPage.UpdatePreviewMessageShown";

            private AsyncCommand _updatePreviewCommand;

            public AsyncCommand UpdatePreviewCommand => _updatePreviewCommand ?? (_updatePreviewCommand = new AsyncCommand(async () => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    UpdatePreviewDirectCommand.Execute(null);
                    return;
                }

                if (!ValuesStorage.GetBool(KeyUpdatePreviewMessageShown) && ModernDialog.ShowMessage(
                        ImportantTips.Entries.GetByIdOrDefault(@"trackPreviews")?.Content, AppStrings.Common_HowTo_Title, MessageBoxButton.OK) !=
                        MessageBoxResult.OK) {
                    return;
                }

                var directory = FileUtils.GetDocumentsScreensDirectory();
                var shots = FileUtils.GetFilesSafe(directory);

                await QuickDrive.RunAsync(track: SelectedTrackConfiguration);
                if (ScreenshotsConverter.CurrentConversion?.IsCompleted == false) {
                    await ScreenshotsConverter.CurrentConversion;
                }

                var newShots = FileUtils.GetFilesSafe(directory)
                                        .Where(x => !shots.Contains(x) && Regex.IsMatch(x, @"\.(jpe?g|png|bmp)$", RegexOptions.IgnoreCase)).ToList();
                if (!newShots.Any()) {
                    NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, ControlsStrings.AcObject_CannotUpdatePreview_TrackCommentary);
                    return;
                }

                ValuesStorage.Set(KeyUpdatePreviewMessageShown, true);

                var shot = new ImageViewer(newShots) {
                    Model = {
                        MaxImageHeight = CommonAcConsts.TrackPreviewWidth,
                        MaxImageWidth = CommonAcConsts.TrackPreviewHeight
                    }
                }.ShowDialogInSelectFileMode();
                if (shot == null) return;

                try {
                    ImageUtils.ApplyPreview(shot, SelectedTrackConfiguration.PreviewImage, CommonAcConsts.TrackPreviewHeight, CommonAcConsts.TrackPreviewWidth);
                } catch (Exception e) {
                    NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, e);
                }
            }, () => SelectedObject.Enabled));

            private CommandBase _updatePreviewDirectCommand;

            public ICommand UpdatePreviewDirectCommand => _updatePreviewDirectCommand ?? (_updatePreviewDirectCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.ImagesFilter,
                    Title = AppStrings.Common_SelectImageForPreview,
                    InitialDirectory = FileUtils.GetDocumentsScreensDirectory(),
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() == true) {
                    try {
                        ImageUtils.ApplyPreview(dialog.FileName, SelectedTrackConfiguration.PreviewImage, CommonAcConsts.TrackPreviewHeight, CommonAcConsts.TrackPreviewWidth);
                    } catch (Exception e) {
                        NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, e);
                    }
                }
            }));

            private DelegateCommand _trackMapUpdateCommand;

            public DelegateCommand UpdateMapCommand => _trackMapUpdateCommand ?? (_trackMapUpdateCommand = new DelegateCommand(() => {
                TrackMapRendererWrapper.Run(SelectedTrackConfiguration).Forget();
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
                }
            }
        }

        protected override void VersionInfoBlock_OnMouse(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new VersionInfoEditor(_model.SelectedTrackConfiguration).ShowDialog();
            }
        }

        private void SpecsInfoBlock_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                // new TrackSpecsEditor(SelectedTrack).ShowDialog();
            }
        }

        private void GeoTags_KeyDown(object sender, MouseButtonEventArgs e) {
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
                new InputBinding(_model.DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.DriveOptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift))
            });
            InitializeComponent();
        }

        private ViewModel _model;

        private void ToolbarButtonQuickDrive_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }
    }
}
