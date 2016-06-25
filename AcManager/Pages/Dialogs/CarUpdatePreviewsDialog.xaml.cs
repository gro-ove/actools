using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Annotations;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Addons;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;
using Application = System.Windows.Application;

namespace AcManager.Pages.Dialogs {
    public partial class CarUpdatePreviewsDialog : INotifyPropertyChanged, IProgress<Showroom.ShootingProgress>, IUserPresetable {
        #region Options

        private bool _disableSweetFx;

        public bool DisableSweetFx {
            get { return _disableSweetFx; }
            set {
                if (Equals(value, _disableSweetFx)) return;
                _disableSweetFx = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _disableWatermark;

        public bool DisableWatermark {
            get { return _disableWatermark; }
            set {
                if (Equals(value, _disableWatermark)) return;
                _disableWatermark = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _resizePreviews;

        public bool ResizePreviews {
            get { return _resizePreviews; }
            set {
                if (Equals(value, _resizePreviews)) return;
                _resizePreviews = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _maximizeVideoSettings;

        public bool MaximizeVideoSettings {
            get { return _maximizeVideoSettings; }
            set {
                if (Equals(value, _maximizeVideoSettings)) return;
                _maximizeVideoSettings = value;
                OnPropertyChanged();
            }
        }

        private bool _enableFxaa;

        public bool EnableFxaa {
            get { return _enableFxaa; }
            set {
                if (Equals(value, _enableFxaa)) return;
                _enableFxaa = value;
                OnPropertyChanged();
            }
        }
        
        private bool _useSpecialResolution;

        public bool UseSpecialResolution {
            get { return _useSpecialResolution; }
            set {
                if (Equals(value, _useSpecialResolution)) return;
                _useSpecialResolution = value;
                OnPropertyChanged();
            }
        }

        private string _cameraPosition;

        public string CameraPosition {
            get { return _cameraPosition; }
            set {
                if (Equals(value, _cameraPosition)) return;
                _cameraPosition = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private string _cameraLookAt;

        public string CameraLookAt {
            get { return _cameraLookAt; }
            set {
                if (Equals(value, _cameraLookAt)) return;
                _cameraLookAt = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double _cameraFov;

        public double CameraFov {
            get { return _cameraFov; }
            set {
                if (Equals(value, _cameraFov)) return;
                _cameraFov = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double? _cameraExposure;

        public double? CameraExposure {
            get { return _cameraExposure; }
            set {
                if (Equals(value, _cameraExposure)) return;
                _cameraExposure = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private ShowroomObject _selectedShowroom;

        public ShowroomObject SelectedShowroom {
            get { return _selectedShowroom; }
            set {
                if (Equals(value, _selectedShowroom)) return;
                var update = _selectedShowroom == null || value == null;
                _selectedShowroom = value;
                OnPropertyChanged();
                SaveLater();

                if (!update) return;
                foreach (var command in Buttons.Select(x => x.Command).OfType<RelayCommand>().Where(x => x != null)) {
                    command.OnCanExecuteChanged();
                }
            }
        }

        public AcLoadedOnlyCollection<ShowroomObject> Showrooms => ShowroomsManager.Instance.LoadedOnlyCollection;

        private IWithId _selectedFilter;

        public IWithId SelectedFilter {
            get { return _selectedFilter; }
            set {
                if (Equals(value, _selectedFilter)) return;
                var update = _selectedFilter == null || value == null;
                _selectedFilter = value;
                OnPropertyChanged();
                SaveLater();

                if (!update) return;
                foreach (var command in Buttons.Select(x => x.Command).OfType<RelayCommand>().Where(x => x != null)) {
                    command.OnCanExecuteChanged();
                }
            }
        }

        public BuiltInPpFilter DefaultPpFilter { get; } = new BuiltInPpFilter {
            Name = "AT-Previews Special",
            Filename = "AT-Previews Special.ini",
            Content = Properties.BinaryResources.PpFilterAtPreviewsSpecial
        };

        public class BuiltInPpFilter : NotifyPropertyChanged, IWithId {
            public string Filename;
            public byte[] Content;

            private string _id, _name;

            public string Id => _id ?? (_id = Filename.ToLower());

            public string Name {
                get {
                    return _name ?? (_name = Path.GetFileNameWithoutExtension(Filename));
                }
                set {
                    if (Equals(value, _name)) return;
                    _name = value;
                    OnPropertyChanged();
                }
            } 

            public override string ToString() {
                return Name;
            }

            public void EnsureInstalled() {
                var destination = Path.Combine(FileUtils.GetPpFiltersDirectory(AcRootDirectory.Instance.Value), Filename);
                if (File.Exists(destination) && new FileInfo(destination).Length == Content.Length) return;

                // don’t ignore changes because why? list will be updated, but it’s not a bad thing
                File.WriteAllBytes(destination, Content);
            }
        }

        private IReadOnlyList<IWithId> _filters;

        public IReadOnlyList<IWithId> Filters {
            get {
                PpFiltersManager.Instance.EnsureLoaded();
                return _filters ?? (_filters = new ObservableCollection<IWithId>(
                        PpFiltersManager.Instance.LoadedOnly.Where(x => DefaultPpFilter.Id != x.Id)
                                        .Cast<IWithId>().Prepend(DefaultPpFilter)));
            }
        }
        #endregion

        #region Shooting
        public CarObject SelectedCar { get; set; }

        private string _status;

        public string Status {
            get { return _status; }
            set {
                if (Equals(value, _status)) return;
                _status = value;
                OnPropertyChanged();
            }
        }

        private string _errorMessage;

        public string ErrorMessage {
            get { return _errorMessage; }
            set {
                if (Equals(value, _errorMessage)) return;
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<ResultPreviewComparison> _resultPreviewComparisons;

        public ObservableCollection<ResultPreviewComparison> ResultPreviewComparisons {
            get { return _resultPreviewComparisons; }
            set {
                if (Equals(value, _resultPreviewComparisons)) return;
                _resultPreviewComparisons = value;
                OnPropertyChanged();

                ResultPreviewComparisonsView = new CollectionView(value);
            }
        }

        private CollectionView _resultPreviewComparisonsView;

        public CollectionView ResultPreviewComparisonsView {
            get { return _resultPreviewComparisonsView; }
            set {
                if (Equals(value, _resultPreviewComparisonsView)) return;
                _resultPreviewComparisonsView = value;
                OnPropertyChanged();
            }
        }
        #endregion

        private class SaveableData {
            public string ShowroomId, FilterId;
            public string CameraPosition, CameraLookAt;
            public double CameraFov;
            public double? CameraExposure;

            [DefaultValue(true)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public bool DisableSweetFx;

            [DefaultValue(true)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public bool DisableWatermark;

            [DefaultValue(true)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public bool ResizePreviews;

            [DefaultValue(true)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public bool MaximizeVideoSettings;

            [DefaultValue(true)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public bool EnableFxaa;

            [DefaultValue(true)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public bool UseSpecialResolution;
        }

        private void SaveLater() {
            _saveable.SaveLater();
            Changed?.Invoke(this, new EventArgs());
        }

        private readonly ISaveHelper _saveable;

        private void ShowroomMessage(string showroomName, string showroomId, string informationUrl) {
            if (ShowMessage($@"[url={informationUrl.Length}]{showroomName}[/url] isn’t installed. Install it?",
                    @"Showroom is missing", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            InstallShowroom(showroomName, showroomId).Forget();
        }

        private async Task InstallShowroom(string showroomName, string showroomId) {
            _mode = DialogMode.Options;
            await Task.Delay(100);

            using (var dialog = new WaitingDialog(showroomName)) {
                dialog.Report("Downloading…");

                var destination = FileUtils.GetShowroomsDirectory(AcRootDirectory.Instance.Value);
                var data = await CmApiProvider.GetDataAsync($"static/get/{showroomId}");

                if (data == null) {
                    dialog.Close();
                    NonfatalError.Notify($@"Can’t download showroom “{showroomName}”", "Make sure internet connection is working properly.");
                    return;
                }

                dialog.Content = "Installing…";
                try {
                    await Task.Run(() => {
                        using (var stream = new MemoryStream(data, false))
                        using (var archive = new ZipArchive(stream)) {
                            archive.ExtractToDirectory(destination);
                        }
                    });
                } catch (Exception e) {
                    dialog.Close();
                    NonfatalError.Notify($@"Can’t install showroom “{showroomName}”", e);
                    return;
                }

                await Task.Delay(1000);
                SelectedShowroom = ShowroomsManager.Instance.GetById(showroomId) ?? SelectedShowroom;
            }
        }

        public enum DialogMode {
            Options, Start, StartManual
        }

        private readonly string _loadPreset;
        private readonly string[] _skinIds;

        public CarUpdatePreviewsDialog(CarObject carObject, string[] skinIds, DialogMode mode, string loadPreset = null) {
            SelectedCar = carObject;
            _skinIds = skinIds;
            _mode = mode;
            _loadPreset = loadPreset;

            _saveable = new SaveHelper<SaveableData>("__AutoUpdatePreviews", () => IsVisible ? new SaveableData {
                ShowroomId = SelectedShowroom?.Id,
                FilterId = SelectedFilter?.Id,
                CameraPosition = CameraPosition,
                CameraLookAt = CameraLookAt,
                CameraFov = CameraFov,
                CameraExposure = CameraExposure,
                DisableSweetFx = DisableSweetFx,
                DisableWatermark = DisableWatermark,
                ResizePreviews = ResizePreviews,
                MaximizeVideoSettings = MaximizeVideoSettings,
                EnableFxaa = EnableFxaa,
                UseSpecialResolution = UseSpecialResolution
            } : null, o => {
                if (o.ShowroomId != null) {
                    var showroom = ShowroomsManager.Instance.GetById(o.ShowroomId);
                    SelectedShowroom = showroom ?? SelectedShowroom;
                    if (showroom == null) {
                        switch (o.ShowroomId) {
                            case "at_studio_black":
                                ShowroomMessage("Studio Black Showroom (AT Previews Special)", "at_studio_black",
                                        "http://www.racedepartment.com/downloads/studio-black-showroom.4353/");
                                break;

                            case "at_previews":
                                ShowroomMessage("Kunos Previews Showroom (AT Previews Special)", "at_previews",
                                        "http://www.assettocorsa.net/assetto-corsa-v1-5-dev-diary-part-33/");
                                break;
                        }
                    }
                }

                if (o.FilterId != null) {
                    SelectedFilter = Filters.GetByIdOrDefault(o.FilterId) ?? SelectedFilter;
                } else {
                    SelectedFilter = DefaultPpFilter;
                }

                CameraPosition = o.CameraPosition;
                CameraLookAt = o.CameraLookAt;
                CameraFov = o.CameraFov;
                CameraExposure = o.CameraExposure == 0 ? null : o.CameraExposure;
                DisableWatermark = o.DisableWatermark;
                DisableSweetFx = o.DisableSweetFx;
                ResizePreviews = o.ResizePreviews;
                MaximizeVideoSettings = o.MaximizeVideoSettings;
                EnableFxaa = o.EnableFxaa;
                UseSpecialResolution = o.UseSpecialResolution;
            }, () => {
                SelectedShowroom = null;
                SelectedFilter = (IWithId)PpFiltersManager.Instance.GetDefault() ?? DefaultPpFilter;
                CameraPosition = "-3.867643, 1.423590, 4.70381";
                CameraLookAt = "0.0, 0.7, 0.5";
                CameraFov = 30;
                CameraExposure = 94.5;
                DisableWatermark = true;
                DisableSweetFx = true;
                ResizePreviews = true;
                MaximizeVideoSettings = true;
                EnableFxaa = true;
                UseSpecialResolution = true;
            });

            InitializeComponent();
            DataContext = this;
        }

        public CarUpdatePreviewsDialog(CarObject carObject, DialogMode mode, string loadPreset = null)
            : this(carObject, null, mode, loadPreset) { }

        public static void Run(CarObject carObject, string[] skinIds, string presetFilename) {
            new CarUpdatePreviewsDialog(carObject, skinIds, DialogMode.Start).ShowDialog();
        }

        public static void RunPreset(CarObject carObject, string[] skinIds, string presetFilename) {
            new CarUpdatePreviewsDialog(carObject, skinIds, DialogMode.Start, presetFilename).ShowDialog();
        }

        private void CarUpdatePreviewsDialog_OnLoaded(object sender, RoutedEventArgs e) {
            if (_loadPreset == null) {
                if (_saveable.HasSavedData || UserPresetsControl.CurrentUserPreset != null) {
                    _saveable.Initialize();
                } else {
                    _saveable.Reset();
                    UserPresetsControl.CurrentUserPreset =
                            UserPresetsControl.SavedPresets.FirstOrDefault(x => x.ToString() == "Kunos");
                }
            } else {
                _saveable.Reset();
                UserPresetsControl.CurrentUserPreset =
                        UserPresetsControl.SavedPresets.FirstOrDefault(x => x.Filename == _loadPreset);
            }

            if (_mode == DialogMode.Options) {
                SelectPhase(Phase.Options);
            } else {
                RunShootingProcess(_mode == DialogMode.StartManual);
            }
        }

        private DialogMode _mode;
        private bool _cancelled;

        public new bool ShowDialog() {
            if (_cancelled) return false;
            base.ShowDialog();
            return CurrentPhase == Phase.Result;
        }

        public bool CanBeSaved => SelectedShowroom != null && SelectedFilter != null;

        public const string UserPresetableKeyValue = "Previews";

        public string UserPresetableKey => UserPresetableKeyValue;

        public string ExportToUserPresetData() {
            return _saveable.ToSerializedString();
        }

        public event EventHandler Changed;
        public void ImportFromUserPresetData(string data) {
            _saveable.FromSerializedString(data);
        }

        private void CarUpdatePreviewsDialog_OnClosing(object sender, CancelEventArgs args) {
            if (CurrentPhase == Phase.Waiting) {
                _cancellationTokenSource.Cancel(false);
            } else if (CurrentPhase == Phase.Result && IsResultOk) {
                Apply();
            }
        }

        private async void Apply() {
            using (var waiting = new WaitingDialog {
                Owner = Application.Current.MainWindow
            }) {
                try {
                    await ImageUtils.ApplyPreviewsAsync(AcRootDirectory.Instance.Value, SelectedCar.Id, _resultDirectory, ResizePreviews, waiting, waiting.CancellationToken);
                } catch (OutOfMemoryException e) {
                    NonfatalError.Notify("Can’t save previews", AppAddonsManager.Instance.IsAddonEnabled("Magick")
                            ? "Please, report this bug to developers." : "I recommend to enable Magick.NET addon.", e);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t save previews", e);
                }
            }
        }

        public enum Phase {
            Options, Waiting, Result, Error
        }

        private Phase _currentPhase;

        public Phase CurrentPhase {
            get { return _currentPhase; }
            set {
                if (Equals(value, _currentPhase)) return;
                _currentPhase = value;
                OnPropertyChanged();
            }
        }
        
        private const string KeySize = "_CarUpdatePreviewsDialog.Size";

        private void Resize(double? width, double? height, bool resizeable) {
            var oldWidth = Width;
            var oldHeight = Height;
            var area = Screen.PrimaryScreen.WorkingArea;

            if (width.HasValue && height.HasValue) {
                double w, h;

                if (resizeable) {
                    var p = ValuesStorage.GetPoint(KeySize, new Point(width.Value, height.Value));
                    w = Math.Min(p.X, area.Width);
                    h = Math.Min(p.Y, area.Height);
                } else {
                    w = Math.Min(width.Value, area.Width);
                    h = Math.Min(height.Value, area.Height);
                }

                Width = w;
                Height = h;
                MinWidth = Math.Min(width.Value, area.Width);
                MinHeight = Math.Min(height.Value, area.Height);
                SizeToContent = SizeToContent.Manual;
                ResizeMode = resizeable ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;
            } else {
                MinWidth = 240d;
                MinHeight = 120d;
                SizeToContent = SizeToContent.WidthAndHeight;
                ResizeMode = ResizeMode.NoResize;
            }

            Left = Math.Max(area.Left, Left + (oldWidth - Width) / 2d);
            Top = Math.Max(area.Top, Top + (oldHeight - Height) / 2d);
        }

        private void CarUpdatePreviewsDialog_OnSizeChanged(object sender, SizeChangedEventArgs e) {
            if (ResizeMode == ResizeMode.CanResizeWithGrip) {
                ValuesStorage.Set(KeySize, new Point(Width, Height));
            }
        }

        private void SelectPhase(Phase phase) {
            CurrentPhase = phase;

            switch (phase) {
                case Phase.Options:
                    OptionsPhase.Visibility = Visibility.Visible;
                    WaitingPhase.Visibility = Visibility.Collapsed;
                    ResultPhase.Visibility = Visibility.Collapsed;
                    ErrorPhase.Visibility = Visibility.Collapsed;
                    Resize(540d, 400d, false);
                    var manual = CreateExtraDialogButton("Manual", o => RunShootingProcess(true), o => CanBeSaved);
                    manual.ToolTip = "Set camera position manually and then press F8 to start shooting";
                    Buttons = new[] {
                        CreateExtraStyledDialogButton("Go.Button", "Go", o => RunShootingProcess(), o => CanBeSaved),
                        manual,
                        CloseButton
                    };
                    break;

                case Phase.Waiting:
                    OptionsPhase.Visibility = Visibility.Collapsed;
                    WaitingPhase.Visibility = Visibility.Visible;
                    ResultPhase.Visibility = Visibility.Collapsed;
                    ErrorPhase.Visibility = Visibility.Collapsed;
                    Resize(540d, 400d, false);
                    Buttons = new[] { CancelButton };
                    break;

                case Phase.Result:
                    OptionsPhase.Visibility = Visibility.Collapsed;
                    WaitingPhase.Visibility = Visibility.Collapsed;
                    ResultPhase.Visibility = Visibility.Visible;
                    ErrorPhase.Visibility = Visibility.Collapsed;
                    Resize(540d, 400d, true);
                    Buttons = new[] { OkButton, CancelButton };
                    break;

                case Phase.Error:
                    OptionsPhase.Visibility = Visibility.Collapsed;
                    WaitingPhase.Visibility = Visibility.Collapsed;
                    ResultPhase.Visibility = Visibility.Collapsed;
                    ErrorPhase.Visibility = Visibility.Visible;
                    Resize(null, null, false);
                    Buttons = new[] { OkButton };
                    break;
            }
        }

        public class ResultPreviewComparison : NotifyPropertyChanged {
            public string Name { get; set; }

            public string LiveryImage { get; set; }

            public string OriginalImage { get; set; }

            public string UpdatedImage { get; set; }
        }

        private CancellationTokenSource _cancellationTokenSource;

        private string _resultDirectory;

        private async void RunShootingProcess(bool manualMode = false) {
            if (SelectedShowroom == null) {
                if (ShowMessage(@"Showroom is missing. Open options?", @"One more thing", MessageBoxButton.YesNo) ==
                    MessageBoxResult.Yes) {
                    SelectPhase(Phase.Options);
                } else {
                    _cancelled = true;
                    Top = -9999;
                    await Task.Delay(1);
                    Close();
                }
                return;
            }

            if (SelectedFilter == null) {
                if (ShowMessage(@"Filter is missing. Open options?", @"One more thing", MessageBoxButton.YesNo) ==
                    MessageBoxResult.Yes) {
                    SelectPhase(Phase.Options);
                } else {
                    _cancelled = true;
                    Top = -9999;
                    await Task.Delay(1);
                    Close();
                }
                return;
            }

            if (SelectedCar.Enabled == false || _skinIds?.Any(x => SelectedCar.SkinsManager.GetWrapperById(x)?.Value.Enabled == false) == true) {
                ErrorMessage = "Can’t update previews of disabled car or skin.";
                SelectPhase(Phase.Error);
                return;
            }

            Status = "Please wait…";
            SelectPhase(Phase.Waiting);

            _cancellationTokenSource = new CancellationTokenSource();

            try {
                string filterId;

                var builtInPpFilter = SelectedFilter as BuiltInPpFilter;
                if (builtInPpFilter != null) {
                    builtInPpFilter.EnsureInstalled();
                    filterId = builtInPpFilter.Name;
                } else {
                    var filterObject = SelectedFilter as PpFilterObject;
                    filterId = filterObject?.Name;
                }

                var begin = DateTime.Now;
                _resultDirectory = await Showroom.ShotAsync(new Showroom.ShotProperties {
                    AcRoot = AcRootDirectory.Instance.Value,
                    CarId = SelectedCar.Id,
                    ShowroomId = SelectedShowroom.Id,
                    SkinIds = _skinIds,
                    Filter = filterId,
                    Fxaa = EnableFxaa,
                    SpecialResolution = UseSpecialResolution,
                    MaximizeVideoSettings = MaximizeVideoSettings,
                    Mode = manualMode ? Showroom.ShotMode.ClassicManual : Showroom.ShotMode.Fixed,
                    UseBmp = true,
                    DisableWatermark = DisableWatermark,
                    DisableSweetFx = DisableSweetFx,
                    ClassicCameraDx = 0.0,
                    ClassicCameraDy = 0.0,
                    ClassicCameraDistance = 5.5,
                    FixedCameraPosition = CameraPosition,
                    FixedCameraLookAt = CameraLookAt,
                    FixedCameraFov = CameraFov,
                    FixedCameraExposure = CameraExposure ?? 0d,
                }, this, _cancellationTokenSource.Token);
                TakenTime = DateTime.Now - begin;

                if (_resultDirectory == null) {
                    ErrorMessage = "Something went wrong.";
                    SelectPhase(Phase.Error);
                    Logging.Warning("cannot update previews, result is null");
                    return;
                }

                ResultPreviewComparisons = new ObservableCollection<ResultPreviewComparison>(
                    Directory.GetFiles(_resultDirectory, "*.*").Select(x => {
                        var id = (Path.GetFileNameWithoutExtension(x) ?? "").ToLower();
                        return new ResultPreviewComparison {
                            Name = SkinDisplayName(id),

                            /* because theoretically skin could be non-existed at this point */
                            LiveryImage = Path.Combine(SelectedCar.Location, "skins", id, "livery.png"),
                            OriginalImage = Path.Combine(SelectedCar.Location, "skins", id, "preview.jpg"),
                            UpdatedImage = x
                        };
                    })
                );

                SelectPhase(Phase.Result);
            } catch (ShotingCancelledException e) {
                ErrorMessage = e.UserCancelled ? "Cancelled" : e.Message + ".";
                SelectPhase(Phase.Error);

                if (!e.UserCancelled) {
                    Logging.Warning("cannot update previews: " + e);
                }
            } catch (Exception e) {
                ErrorMessage = e.Message + ".";
                SelectPhase(Phase.Error);
                Logging.Warning("cannot update previews: " + e);
            }
        }

        private TimeSpan _takenTime;

        public TimeSpan TakenTime {
            get { return _takenTime; }
            set {
                if (Equals(value, _takenTime)) return;
                _takenTime = value;
                OnPropertyChanged();
            }
        }

        private string SkinDisplayName(string skinId) {
            var skin = SelectedCar.GetSkinById(skinId);
            return skin == null ? skinId : skin.DisplayName;
        }

        public void Report(Showroom.ShootingProgress value) {
            Status = $@"Now updating: {SkinDisplayName(value.SkinId)} ({value.SkinNumber + 1}/{value.TotalSkins})";
        }

        private void ImageViewer(bool showUpdated) {
            var current = (ResultPreviewComparison)ResultPreviewComparisonsView.CurrentItem;
            new ImageViewer(new[] { current.OriginalImage, current.UpdatedImage }, showUpdated ? 1 : 0, 1022, 575).ShowDialog();
        }

        private void OriginalPreview_OnMouseDown(object sender, MouseButtonEventArgs e) {
            ImageViewer(false);
        }

        private void UpdatedPreview_OnMouseDown(object sender, MouseButtonEventArgs e) {
            ImageViewer(true);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
