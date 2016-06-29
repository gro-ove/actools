using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.About;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;
using StringBasedFilter;

namespace AcManager.Pages.Selected {
    public partial class SelectedTrackPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedTrackPageViewModel : SelectedAcObjectViewModel<TrackObject> {
            public SelectedTrackPageViewModel([NotNull] TrackObject acObject) : base(acObject) {
                SelectedTrackConfiguration = acObject;
            }

            private TrackBaseObject _selectedTrackConfiguration;

            public TrackBaseObject SelectedTrackConfiguration {
                get { return _selectedTrackConfiguration; }
                private set {
                    if (Equals(value, _selectedTrackConfiguration)) return;
                    _selectedTrackConfiguration = value;
                    OnPropertyChanged();
                }
            }

            private RelayCommand _driveCommand;

            public RelayCommand DriveCommand => _driveCommand ?? (_driveCommand = new RelayCommand(o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !QuickDrive.Run(track: SelectedTrackConfiguration)) {
                    DriveOptionsCommand.Execute(null);
                }
            }, o => SelectedTrackConfiguration.Enabled));

            private RelayCommand _driveOptionsCommand;

            public RelayCommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new RelayCommand(o => {
                QuickDrive.Show(track: SelectedTrackConfiguration);
            }, o => SelectedTrackConfiguration.Enabled));

            public ObservableCollection<MenuItem> QuickDrivePresets {
                get { return _quickDrivePresets; }
                private set {
                    if (Equals(value, _quickDrivePresets)) return;
                    _quickDrivePresets = value;
                    OnPropertyChanged();
                }
            }

            private static ObservableCollection<MenuItem> _quickDrivePresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public override void Unload() {
                base.Unload();
                _helper.Dispose();
            }

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(QuickDrive.PresetableKeyValue, p => {
                        QuickDrive.RunPreset(p, track: SelectedTrackConfiguration);
                    });
                }
            }

            private const string KeyUpdatePreviewMessageShown = "SelectTrackPage.UpdatePreviewMessageShown";

            private AsyncCommand _updatePreviewCommand;

            public AsyncCommand UpdatePreviewCommand => _updatePreviewCommand ?? (_updatePreviewCommand = new AsyncCommand(async o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    UpdatePreviewDirectCommand.Execute(o);
                    return;
                }

                if (!ValuesStorage.GetBool(KeyUpdatePreviewMessageShown) && ModernDialog.ShowMessage(
                        ImportantTips.Entries.GetByIdOrDefault("trackPreviews")?.Content, "How-To", MessageBoxButton.OK) !=
                        MessageBoxResult.OK) {
                    return;
                }

                var directory = FileUtils.GetDocumentsScreensDirectory();
                var shots = Directory.GetFiles(directory);

                await QuickDrive.RunAsync(track: SelectedTrackConfiguration);
                var newShots = Directory.GetFiles(directory).Where(x => !shots.Contains(x) && (
                        x.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                x.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                x.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))).ToList();

                if (!newShots.Any()) {
                    NonfatalError.Notify("Can’t update preview", "You were supposed to make at least one screenshot.");
                    return;
                }

                ValuesStorage.Set(KeyUpdatePreviewMessageShown, true);

                var shot = new ImageViewer(newShots) {
                    Model = {
                        MaxImageHeight = 200d,
                        MaxImageWidth = 355d
                    }
                }.ShowDialogInSelectFileMode();
                if (shot == null) return;

                try {
                    ImageUtils.ApplyPreview(shot, SelectedTrackConfiguration.PreviewImage, 355d, 200d);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t update preview", e);
                }
            }, o => SelectedObject.Enabled));

            private RelayCommand _updatePreviewDirectCommand;

            public RelayCommand UpdatePreviewDirectCommand => _updatePreviewDirectCommand ?? (_updatePreviewDirectCommand = new RelayCommand(o => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.ImagesFilter,
                    Title = "Select New Preview Image",
                    InitialDirectory = FileUtils.GetDocumentsScreensDirectory(),
                    RestoreDirectory = true
                };
                if (dialog.ShowDialog() == true) {
                    try {
                        ImageUtils.ApplyPreview(dialog.FileName, SelectedTrackConfiguration.PreviewImage, 355d, 200d);
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t update preview", e);
                    }
                }
            }));

            protected override void FilterExec(string type) {
                switch (type) {
                    case "author":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedTrackConfiguration.Author)
                                ? "author-" : $"author:{Filter.Encode(SelectedTrackConfiguration.Author)}");
                        break;

                    case "country":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedTrackConfiguration.Country)
                                ? "country-" : $"country:{Filter.Encode(SelectedTrackConfiguration.Country)}");
                        break;

                    case "city":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedTrackConfiguration.City)
                                ? "city-" : $"city:{Filter.Encode(SelectedTrackConfiguration.City)}");
                        break;

                    case "year":
                        NewFilterTab(SelectedTrackConfiguration.Year.HasValue ? $"year:{SelectedTrackConfiguration.Year}" : "year-");
                        break;

                    case "decade":
                        if (!SelectedTrackConfiguration.Year.HasValue) {
                            NewFilterTab("year-");
                        }

                        var start = (int)Math.Floor((SelectedTrackConfiguration.Year ?? 0) / 10d) * 10;
                        NewFilterTab($"year>{start - 1} & year<{start + 10}");
                        break;
                }
            }
        }

        protected override void VersionInfoBlock_OnMouseDown(object sender, MouseButtonEventArgs e) {
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
                throw new Exception("ID is missing");
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
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(_model = new SelectedTrackPageViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.UpdatePreviewDirectCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Alt)),

                new InputBinding(_model.DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.DriveOptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift))
            });
            InitializeComponent();
        }

        private SelectedTrackPageViewModel _model;

        private void ToolbarButtonQuickDrive_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }
    }
}
