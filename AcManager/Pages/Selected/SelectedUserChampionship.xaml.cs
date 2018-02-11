using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Tools;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Microsoft.Win32;
using SharpCompress.Common;
using SharpCompress.Writers;
using UIElement = System.Windows.UIElement;

namespace AcManager.Pages.Selected {
    public class RoundsListDataTemplateSelector : DataTemplateSelector {
        public DataTemplate RoundDataTemplate { get; set; }

        public DataTemplate NewRoundDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is UserChampionshipRoundExtended ? RoundDataTemplate : NewRoundDataTemplate;
        }
    }

    public class NumberToColumnsConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var v = value.As<int>();
            var max = parameter.As<int>();
            if (max < 1 || v <= max) return v;

            var from = max / 2;
            return Enumerable.Range(from, max - from + 1).Select(x => new {
                Colums = x,
                Rows = (int)Math.Ceiling((double)v / x)
            }).MinEntry(x => (x.Colums * x.Rows - v) * from - x.Colums).Colums;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }
    }

    public partial class SelectedUserChampionship : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public class PlacePoints : NotifyPropertyChanged {
            public int Place { get; }

            public PlacePoints(int place) {
                Place = place;
            }

            private int _value;

            public int Value {
                get => _value;
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public class ViewModel : SelectedAcObjectViewModel<UserChampionshipObject> {
            public Game.JumpStartPenaltyType[] JumpStartPenaltyTypes { get; } = {
                Game.JumpStartPenaltyType.None,
                Game.JumpStartPenaltyType.Pits,
                Game.JumpStartPenaltyType.DriveThrough
            };

            public IReadOnlyList<Game.TrackPropertiesPreset> TrackPropertiesPresets => Game.DefaultTrackPropertiesPresets;

            public IReadOnlyList<string> DifficultyList => new [] {
                "Easy", "Average", "Hard", "Very hard"
            };

            public BetterObservableCollection<PlacePoints> Points { get; }

            public RaceGridViewModel RaceGridViewModel { get; }

            public ViewModel([NotNull] UserChampionshipObject acObject) : base(acObject) {
                Points = new BetterObservableCollection<PlacePoints>();
                UpdatePointsArray();

                acObject.PropertyChanged += OnAcObjectPropertyChanged;

                RaceGridViewModel = new RaceGridViewModel(true);
                LoadRaceGrid();
                RaceGridViewModel.Changed += OnRaceGridViewModelChanged;
            }

            private void UpdatePointsArray() {
                var count = SelectedObject.Drivers.Count;
                if (Points.Count == count) return;

                Points.ReplaceEverythingBy_Direct(Enumerable.Range(1, count).Select((x, i) => {
                    var result = new PlacePoints(x) {
                        Value = SelectedObject.Rules.Points.ArrayElementAtOrDefault(i)
                    };
                    result.PropertyChanged += OnPlacePointsPropertyChanged;
                    return result;
                }));
            }

            private void OnPlacePointsPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(PlacePoints.Value)) {
                    var array = SelectedObject.Rules.Points;
                    if (array.Length < Points.Count) {
                        SelectedObject.Rules.Points = new int[Points.Count];
                        array.CopyTo(SelectedObject.Rules.Points, 0);
                        array = SelectedObject.Rules.Points;
                    }

                    var points = (PlacePoints)sender;
                    array[points.Place - 1] = points.Value;
                }
            }

            private void OnAcObjectPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(UserChampionshipObject.Rules):
                        UpdatePointsArray();
                        break;
                    case nameof(UserChampionshipObject.Drivers):
                        UpdatePointsArray();
                        LoadRaceGridLaterAsync().Forget();
                        break;
                    case nameof(UserChampionshipObject.SerializedRaceGridData):
                        LoadRaceGridLaterAsync().Forget();
                        break;
                    case nameof(UserChampionshipObject.PlayerCar):
                        RaceGridViewModel.PlayerCar = SelectedObject.PlayerCar;
                        break;
                    case nameof(UserChampionshipObject.MaxCars):
                        RaceGridViewModel.TrackPitsNumber = SelectedObject.MaxCars;
                        break;
                    case nameof(UserChampionshipObject.CoherentTime):
                        OnPropertyChanged(nameof(AllowCustomTime));
                        break;
                    case nameof(UserChampionshipObject.RealConditions):
                        OnPropertyChanged(nameof(AllowCustomTime));
                        OnPropertyChanged(nameof(ShowTimeControls));
                        OnPropertyChanged(nameof(ShowWeatherControls));
                        break;
                    case nameof(UserChampionshipObject.RealConditionsManualTime):
                        OnPropertyChanged(nameof(AllowCustomTime));
                        OnPropertyChanged(nameof(ShowTimeControls));
                        break;
                }
            }

            public bool AllowCustomTime => (!SelectedObject.RealConditions || SelectedObject.RealConditionsManualTime) && !SelectedObject.CoherentTime;

            public bool ShowTimeControls => !SelectedObject.RealConditions || SelectedObject.RealConditionsManualTime;

            public bool ShowWeatherControls => !SelectedObject.RealConditions;

            private bool _loadingRaceGrid;

            private async Task LoadRaceGridLaterAsync() {
                if (_loadingRaceGrid) return;
                _loadingRaceGrid = true;

                try {
                    await Task.Delay(10);
                    LoadRaceGrid();
                } finally {
                    _loadingRaceGrid = false;
                }
            }

            private void LoadRaceGrid() {
                try {
                    if (SelectedObject.SerializedRaceGridData != null) {
                        RaceGridViewModel.ImportFromPresetData(SelectedObject.SerializedRaceGridData);
                    } else {
                        RaceGridViewModel.LoadingFromOutside = true;

                        var list = SelectedObject.Drivers;
                        RaceGridViewModel.NonfilteredList.Clear();
                        RaceGridViewModel.Mode = BuiltInGridMode.Custom;
                        RaceGridViewModel.ShuffleCandidates = false;

                        RaceGridViewModel.AiLevelArrangeRandom = 0;
                        RaceGridViewModel.AiLevelArrangeReverse = false;
                        RaceGridViewModel.AiLevel = 100;
                        RaceGridViewModel.AiLevelMin = 100;

                        foreach (var driver in list) {
                            if (driver.CarId == null) continue;

                            var car = CarsManager.Instance.GetById(driver.CarId);
                            if (car == null) {
                                SelectedObject.AddError(AcErrorType.Data_UserChampionshipCarIsMissing, driver.CarId);
                                continue;
                            }

                            var skin = driver.SkinId == null ? null : car.GetSkinById(driver.SkinId);
                            if (skin == null) {
                                SelectedObject.AddError(AcErrorType.Data_UserChampionshipCarSkinIsMissing, driver.CarId, driver.SkinId);
                            }

                            if (!driver.IsPlayer) {
                                RaceGridViewModel.NonfilteredList.Add(new RaceGridEntry(car) {
                                    CarSkin = skin,
                                    Name = driver.Name
                                });
                            }
                        }

                        RaceGridViewModel.OpponentsNumber = RaceGridViewModel.NonfilteredList.Count;
                        RaceGridViewModel.StartingPosition = 0;
                        RaceGridViewModel.FinishLoading();
                    }

                    RaceGridViewModel.PlayerCar = SelectedObject.PlayerCar;
                    RaceGridViewModel.TrackPitsNumber = SelectedObject.MaxCars;
                } finally {
                    RaceGridViewModel.LoadingFromOutside = false;
                }
            }

            private void OnRaceGridViewModelChanged(object sender, EventArgs e) {
                UpdateSerializedRaceGridDataLaterAsync().Forget();
            }

            private bool _updatingSerializedRaceGridData;

            public bool UpdatingSerializedRaceGridData {
                get => _updatingSerializedRaceGridData;
                set {
                    if (Equals(value, _updatingSerializedRaceGridData)) return;
                    _updatingSerializedRaceGridData = value;
                    OnPropertyChanged();
                }
            }

            private CancellationTokenSource _updatingSerializedRaceGridDataTokenSource;

            private void SetSerializedRaceGridData(string data) {
                try {
                    SelectedObject.PropertyChanged -= OnAcObjectPropertyChanged;
                    SelectedObject.SerializedRaceGridData = data;
                } finally {
                    SelectedObject.PropertyChanged += OnAcObjectPropertyChanged;
                }
            }

            private void SetDrivers(UserChampionshipDriver[] drivers) {
                try {
                    SelectedObject.PropertyChanged -= OnAcObjectPropertyChanged;
                    SelectedObject.Drivers = drivers;
                    UpdatePointsArray();
                } finally {
                    SelectedObject.PropertyChanged += OnAcObjectPropertyChanged;
                }
            }

            private async Task UpdateSerializedRaceGridDataLaterAsync() {
                if (SelectedObject.IsStarted) return;

                if (UpdatingSerializedRaceGridData) {
                    if (_updatingSerializedRaceGridDataTokenSource != null) {
                        _updatingSerializedRaceGridDataTokenSource.Cancel();
                    } else return;
                }

                UpdatingSerializedRaceGridData = true;
                CancellationTokenSource tokenSource = null;

                try {
                    using (tokenSource = new CancellationTokenSource()) {
                        _updatingSerializedRaceGridDataTokenSource = tokenSource;

                        await Task.Delay(300, tokenSource.Token);
                        if (tokenSource.IsCancellationRequested) return;

                        SetSerializedRaceGridData(RaceGridViewModel.ExportToPresetData());

                        var generated = await RaceGridViewModel.GenerateGameEntries(tokenSource.Token);
                        if (generated == null) return;

                        SetDrivers(generated.Select(x => new UserChampionshipDriver(x.DriverName, x.CarId, x.SkinId) {
                            AiLevel = x.AiLevel,
                            AiAggression = x.AiAggression,
                            Ballast = x.Ballast,
                            Restrictor = x.Restrictor,
                            Nationality = x.Nationality
                        }).Prepend(new UserChampionshipDriver(UserChampionshipDriver.PlayerName, SelectedObject.PlayerCarId,
                                SelectedObject.PlayerCarSkinId)).ToArray());
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t create race grid", e);
                } finally {
                    if (ReferenceEquals(tokenSource, _updatingSerializedRaceGridDataTokenSource)) {
                        _updatingSerializedRaceGridDataTokenSource = null;
                        UpdatingSerializedRaceGridData = false;
                    }
                }
            }

            public override void Unload() {
                base.Unload();
                RaceGridViewModel.Dispose();
                SelectedObject.PropertyChanged -= OnAcObjectPropertyChanged;
            }

            private DelegateCommand _addNewRoundCommand;

            public DelegateCommand AddNewRoundCommand => _addNewRoundCommand ?? (_addNewRoundCommand = new DelegateCommand(() => {
                var last = SelectedObject.ExtendedRounds.LastOrDefault();
                var track = last?.Track ?? TracksManager.Instance.GetDefault();
                if (!SelectTrackDialog.Show(ref track)) return;

                var lastLength = last?.Track?.GuessApproximateLapDuration().TotalSeconds * last?.LapsCount;
                var lapsCount = lastLength.HasValue ? (int)(lastLength.Value / track.GuessApproximateLapDuration().TotalSeconds) : 2;
                SelectedObject.ExtendedRounds.Add(new UserChampionshipRoundExtended (track){
                    Weather = last?.Weather ?? WeatherManager.Instance.GetDefault(),
                    TrackProperties = last?.TrackProperties ?? Game.DefaultTrackPropertiesPresets[0],
                    LapsCount = lapsCount
                });
            }));

            private ICommand _changeCarCommand;

            public ICommand ChangeCarCommand => _changeCarCommand ?? (_changeCarCommand = new DelegateCommand(() => {
                var dialog = new SelectCarDialog(SelectedObject.PlayerCar) {
                    SelectedSkin = SelectedObject.PlayerCarSkin
                };

                dialog.ShowDialog();
                if (dialog.IsResultOk && dialog.SelectedCar != null) {
                    SelectedObject.SetPlayerCar(dialog.SelectedCar, dialog.SelectedSkin);
                }
            }));

            private CommandBase _updatePreviewDirectCommand;

            public ICommand UpdatePreviewDirectCommand => _updatePreviewDirectCommand ?? (_updatePreviewDirectCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.ImagesFilter,
                    Title = AppStrings.Common_SelectImageForPreview,
                    InitialDirectory = AcPaths.GetDocumentsScreensDirectory(),
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() == true) {
                    try {
                        ImageUtils.ApplyPreview(dialog.FileName, SelectedObject.PreviewImage,
                                CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight, null, true);
                    } catch (Exception e) {
                        NonfatalError.Notify(ControlsStrings.AcObject_CannotUpdatePreview, e);
                    }

                    _setCropParamsCommand?.RaiseCanExecuteChanged();
                }
            }));

            private DelegateCommand _setCropParamsCommand;

            public DelegateCommand SetCropParamsCommand => _setCropParamsCommand ?? (_setCropParamsCommand = new DelegateCommand(() => {
                var source = ImageEditor.Proceed(SelectedObject.PreviewImage, new Size(206, 206), false, SelectedObject.PreviewCrop, out Int32Rect rect);
                if (source != null) {
                    SelectedObject.PreviewCrop = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
                }
            }, () => File.Exists(SelectedObject.PreviewImage)));

            private const long SharingSizeLimit = 512 * 1024;

            private AsyncCommand _shareCommand;

            public AsyncCommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(async () => {
                byte[] data = null;

                try {
                    await Task.Run(() => {
                        using (var memory = new MemoryStream()) {
                            using (var writer = WriterFactory.Open(memory, ArchiveType.Zip, CompressionType.Deflate)) {
                                foreach (var filename in new [] {
                                    SelectedObject.Location,
                                    SelectedObject.ExtendedFilename,
                                    SelectedObject.PreviewImage
                                }.Where(File.Exists)) {
                                    writer.Write(Path.GetFileName(filename), filename);
                                }
                            }

                            data = memory.ToArray();
                        }
                    });

                    if (data.Length > SharingSizeLimit) {
                        NonfatalError.Notify("Can’t share championship",
                                string.Format(AppStrings.Weather_CannotShare_Commentary, SharingSizeLimit.ToReadableSize()));
                        return;
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t share championship", e);
                    return;
                }

                if (data == null) return;
                await SharingUiHelper.ShareAsync(SharedEntryType.UserChampionship, SelectedObject.Name, null, data);
            }));
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        bool IImmediateContent.ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = UserChampionshipsManager.Instance.GetById(id);
            if (obj == null) return false;

            _id = id;
            _object = obj;
            SetModel();
            return true;
        }

        private UserChampionshipObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await UserChampionshipsManager.Instance.GetByIdAsync(_id);

            if (_object != null) {
                foreach (var driver in _object.Drivers) {
                    if (driver.CarId == null) continue;
                    await CarsManager.Instance.GetByIdAsync(driver.CarId);
                }

                foreach (var round in _object.Rounds) {
                    if (round.TrackId == null) continue;
                    await TracksManager.Instance.GetByIdAsync(round.TrackId);
                }
            }
        }

        void ILoadableContent.Load() {
            _object = UserChampionshipsManager.Instance.GetById(_id);
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            SetModel();
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            this.AddWidthCondition((Tab.GetLinkListWidth() ?? 360d) + 250d).Add(v => {
                Base.HeaderPadding = v ? new Thickness(0, 0, Tab.GetLinkListWidth() ?? 360, 0) : default(Thickness);
                Tab.Margin = v ? new Thickness(0, -30, 0, 0) : default(Thickness);
            });
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new [] {
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(_model.UpdatePreviewDirectCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.AddNewRoundCommand, new KeyGesture(Key.N, ModifierKeys.Control))
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {}

        private void OnRoundTrackClick(object sender, MouseButtonEventArgs e) {
            if (e.Handled) return;
            if (((FrameworkElement)sender).DataContext is UserChampionshipRoundExtended round) {
                round.Track = SelectTrackDialog.Show(round.Track);
                e.Handled = true;
            }
        }

        private void OnCarBlockDrop(object sender, DragEventArgs e) {
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;

            if (raceGridEntry == null && carObject == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            _model.SelectedObject.SetPlayerCar(carObject ?? raceGridEntry.Car, raceGridEntry?.CarSkin ?? carObject?.SelectedSkin);
            e.Effects = DragDropEffects.Copy;
        }

        private void OnCarBlockClick(object sender, RoutedEventArgs e) {
            if (e.Handled) return;
            _model.ChangeCarCommand.Execute(null);
        }

        private void OnVersionInfoBlockClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;

                new ModernPopup {
                    Content = new PopupAuthor((ISelectedAcObjectViewModel)DataContext),
                    PlacementTarget = sender as UIElement
                }.IsOpen = true;
            }
        }

        private void OnCroppedPreviewClick(object sender, MouseButtonEventArgs e) {
            _model.SetCropParamsCommand.Execute();
        }

        private void OnAddNewRoundButtonClick(object sender, RoutedEventArgs e) {
            _model.AddNewRoundCommand.Execute();
        }

        private void OnItemsControlDrop(object sender, DragEventArgs e) {
            if (!(e.Data.GetData(UserChampionshipRoundExtended.DraggableFormat) is UserChampionshipRoundExtended roundObject)) {
                e.Effects = DragDropEffects.None;
                return;
            }

            var newIndex = ((ItemsControl)sender).GetMouseItemIndex();
            var list = _model.SelectedObject.ExtendedRounds;
            var oldIndex = list.IndexOf(roundObject);
            if (newIndex != oldIndex) {

                if (oldIndex != -1) {
                    if (newIndex == -1 || newIndex >= list.Count) {
                        list.Move(oldIndex, list.Count - 1);
                    } else {
                        list.Move(oldIndex, newIndex);
                    }
                } else {
                    if (newIndex == -1 || newIndex >= list.Count) {
                        list.Add(roundObject);
                    } else {
                        list.Insert(newIndex, roundObject);
                    }
                }
            }

            e.Effects = DragDropEffects.Move;
        }

        private void OnRoundDescriptionDoubleClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) return;
            if (((FrameworkElement)sender).DataContext is UserChampionshipRoundExtended round) {
                round.Description = Prompt.Show("Round description:", "Round Description", round.Description, round.Track?.Description, multiline: true) ??
                        round.Description;
            }
        }
    }
}
