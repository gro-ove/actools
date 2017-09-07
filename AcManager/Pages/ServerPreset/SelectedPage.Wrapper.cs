using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.ServerPreset {
    public enum ShareMode {
        [Description("None")]
        None,

        [Description("Download URL")]
        Url,

        [Description("Share From Server")]
        Directly
    }

    public static class WrapperContentObjectExtension {
        public static void LoadFrom([CanBeNull] this IEnumerable<WrapperContentObject> enumerable, [CanBeNull] JToken obj, string childrenKey = null) {
            if (enumerable == null) return;
            foreach (var o in enumerable) {
                o.LoadFrom(obj?[o.AcObject.Id], childrenKey);
            }
        }

        public static void SaveTo([CanBeNull] this IList<WrapperContentObject> list, [NotNull] JObject obj, string key, string childrenKey = null) {
            if (list == null) return;

            var o = new JObject();
            foreach (var i in list) {
                var j = new JObject();
                i.SaveTo(j, childrenKey);
                if (j.Count > 0) {
                    o[i.AcObject.Id] = j;
                    obj[key] = o;
                }
            }
        }
    }

    public class WrapperContentObject : Displayable {
        private readonly List<string> _toRemove = new List<string>();
        private readonly string _contentDirectory;
        public bool CanBePacked { get; }

        public WrapperContentObject(AcCommonObject acObject, string contentDirectory) {
            _contentDirectory = contentDirectory;
            CanBePacked = acObject.CanBePacked();

            AcObject = acObject;
            Version = ContentVersion = (acObject as IAcObjectVersionInformation)?.Version;

            AcObject.SubscribeWeak((sender, args) => {
                switch (args.PropertyName) {
                    case nameof(AcCommonObject.DisplayName):
                        UpdateDisplayName();
                        break;
                    case nameof(IAcObjectVersionInformation.Version):
                        var updated = (acObject as IAcObjectVersionInformation)?.Version;
                        if (Version == ContentVersion) {
                            Version = updated;
                        }

                        ContentVersion = updated;
                        break;
                }
            });

            UpdateDisplayName();
            _sizeLazy = Lazier.Create(GetSize);
            _fileIsMissingLazy = Lazier.Create(() => ShareMode == ShareMode.Directly && Filename != null && !File.Exists(Filename));
        }

        private long? GetSize() {
            try {
                return ShareMode != ShareMode.Directly ? null :
                        Filename != null && File.Exists(Filename) ? new FileInfo(Filename).Length : (long?)null;
            } catch (Exception) {
                return null;
            }
        }

        private void UpdateDisplayName() {
            switch (AcObject.GetType().Name) {
                case nameof(CarObject):
                    DisplayName = "Car: " + AcObject.DisplayName;
                    break;
                case nameof(CarSkinObject):
                    DisplayName = "Skin: " + AcObject.DisplayName;
                    break;
                case nameof(TrackObject):
                case nameof(TrackExtraLayoutObject):
                case nameof(TrackObjectBase):
                    DisplayName = "Track: " + AcObject.Name;
                    break;
                case nameof(WeatherObject):
                    DisplayName = "Weather: " + AcObject.DisplayName;
                    break;
                default:
                    DisplayName = AcObject.DisplayName;
                    break;
            }
        }

        public AcCommonObject AcObject { get; }

        private readonly Lazier<long?> _sizeLazy;
        public long? Size => _sizeLazy.Value;

        private readonly Lazier<bool> _fileIsMissingLazy;
        public bool FileIsMissing => _fileIsMissingLazy.Value;

        public void LoadFrom([CanBeNull] JToken e, string childrenKey = null) {
            if (e == null || !CanBePacked) {
                ShareMode = ShareMode.None;
            } else {
                if ((string)e["url"] != null) {
                    DownloadUrl = (string)e["url"];
                    Filename = null;
                    ShareMode = ShareMode.Url;
                } else if ((string)e["file"] != null) {
                    var fileName = (string)e["file"];
                    DownloadUrl = null;
                    Filename = Path.IsPathRooted(fileName) ? fileName : Path.Combine(_contentDirectory, fileName);
                    ShareMode = ShareMode.Directly;
                } else {
                    DownloadUrl = null;
                    Filename = null;
                    ShareMode = ShareMode.None;
                }

                Version = (string)e["version"];
            }

            if (childrenKey != null) {
                Children.LoadFrom(e?[childrenKey]);
            }
        }

        public void SaveTo([NotNull] JObject j, string childrenKey = null) {
            j.RemoveAll();

            if (ShareMode == ShareMode.Url) {
                j["url"] = string.IsNullOrWhiteSpace(DownloadUrl) ? null : DownloadUrl;
            } else if (ShareMode == ShareMode.Directly) {
                j["file"] = string.IsNullOrWhiteSpace(Filename) ? null : Filename;
            }

            if (childrenKey != null) {
                Children.SaveTo(j, childrenKey);
            }

            if (j.Count > 0 && !string.IsNullOrWhiteSpace(Version)) {
                j["version"] = Version;
            }
        }

        private ShareMode _shareMode = ShareMode.Url;

        public ShareMode ShareMode {
            get => _shareMode;
            set {
                if (Equals(value, _shareMode)) return;
                _shareMode = value;
                _sizeLazy.Reset();
                _fileIsMissingLazy.Reset();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Size));
                OnPropertyChanged(nameof(FileIsMissing));
            }
        }

        private string _filename;

        [CanBeNull]
        public string Filename {
            get => _filename;
            set {
                if (Equals(value, _filename)) return;
                _filename = value;
                _sizeLazy.Reset();
                _fileIsMissingLazy.Reset();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Size));
                OnPropertyChanged(nameof(FileIsMissing));
                DisplayFilename = value == null ? null : FileUtils.GetRelativePath(value, _contentDirectory);
            }
        }

        private string _displayFilename;

        [CanBeNull]
        public string DisplayFilename {
            get => _displayFilename;
            set {
                if (Equals(value, _displayFilename)) return;
                _displayFilename = value;
                OnPropertyChanged();
            }
        }

        private string _downloadUrl;

        public string DownloadUrl {
            get => _downloadUrl;
            set {
                if (string.IsNullOrEmpty(value)) value = null;
                if (Equals(value, _downloadUrl)) return;
                _downloadUrl = value;
                OnPropertyChanged();
            }
        }

        private string _version;

        public string Version {
            get => _version;
            set {
                if (string.IsNullOrEmpty(value)) value = null;
                if (Equals(value, _version)) return;
                _version = value;
                OnPropertyChanged();
                VersionsDiffer = _version != null && _version != _contentVersion;
                _resetToContentVersionCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _contentVersion;

        public string ContentVersion {
            get => _contentVersion;
            set {
                if (Equals(value, _contentVersion)) return;
                _contentVersion = value;
                OnPropertyChanged();
                VersionsDiffer = _version != null && _version != _contentVersion;
            }
        }

        private bool _versionsDiffer;

        public bool VersionsDiffer {
            get => _versionsDiffer;
            set {
                if (Equals(value, _versionsDiffer)) return;
                _versionsDiffer = value;
                OnPropertyChanged();
                _resetToContentVersionCommand?.RaiseCanExecuteChanged();
            }
        }

        private DelegateCommand _resetToContentVersionCommand;

        public DelegateCommand ResetToContentVersionCommand => _resetToContentVersionCommand ??
                (_resetToContentVersionCommand = new DelegateCommand(() => { Version = ContentVersion; }, () => VersionsDiffer || _version == null));

        private string _childrenName;

        public string ChildrenName {
            get => _childrenName;
            set {
                if (Equals(value, _childrenName)) return;
                _childrenName = value;
                OnPropertyChanged();
            }
        }

        private List<WrapperContentObject> _children;

        public List<WrapperContentObject> Children {
            get => _children;
            set {
                if (Equals(value, _children)) return;

                if (_children != null) {
                    foreach (var v in _children) {
                        v.PropertyChanged -= OnChildPropertyChanged;
                    }
                }

                _children = value;
                OnPropertyChanged();

                if (value != null) {
                    foreach (var v in value) {
                        v.PropertyChanged += OnChildPropertyChanged;
                    }
                }
            }
        }

        public object ChildrenValues => _children;

        private void OnChildPropertyChanged(object sender1, PropertyChangedEventArgs propertyChangedEventArgs) {
            OnPropertyChanged(nameof(ChildrenValues));
        }

        private DelegateCommand _selectFileCommand;

        public DelegateCommand SelectFileCommand => _selectFileCommand ?? (_selectFileCommand = new DelegateCommand(() => {
            var dialog = new OpenFileDialog {
                Title = "Select Packed Archive",
                CheckFileExists = true,
                Filter = FileDialogFilters.ArchivesFilter,
                InitialDirectory = _contentDirectory,
                Multiselect = false,
                RestoreDirectory = false,
                CustomPlaces = new List<FileDialogCustomPlace>(new[] {
                    new FileDialogCustomPlace(_contentDirectory)
                }.Where(x => x != null))
            };

            if (dialog.ShowDialog() == true) {
                RemoveCurrentIfNeeded();
                Filename = dialog.FileName;
            }
        }));

        public class PackCarDataHolder : NotifyPropertyChanged {
            private bool _value = ValuesStorage.GetBool("serverWrapper:packCarData", true);

            public bool Value {
                get => _value;
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                    ValuesStorage.Set("serverWrapper:packCarData", value);
                }
            }
        }

        public static PackCarDataHolder PackCarData = new PackCarDataHolder();
        public bool IsCarObject => AcObject is CarObject;

        private string GetTypePrefix() {
            switch (AcObject) {
                case CarObject car:
                    return $"car-{car.Id}-{car.Version}.zip";
                case CarSkinObject skin:
                    return $"skin-{skin.CarId}-{skin.Id}.zip";
                case TrackObject track:
                    return $"track-{track.Id}-{track.Version}.zip";
                case WeatherObject weather:
                    return $"weather-{weather.Id}.zip";
                default:
                    return $"something-{AcObject.Id}.zip";
            }
        }

        public async Task Repack(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            var filename = Filename;

            FileUtils.EnsureDirectoryExists(_contentDirectory);
            var newFilename = FileUtils.EnsureUnique(Path.Combine(_contentDirectory, GetTypePrefix()), "-{0}", true, 0);

            AcCommonObject.AcCommonObjectPackerParams packParams;
            switch (AcObject) {
                case CarObject _:
                    packParams = new CarObject.CarPackerParams {
                        IncludeTemplates = false,
                        PackData = PackCarData.Value
                    };
                    break;

                case CarSkinObject _:
                    packParams = new CarSkinObject.CarSkinPackerParams {
                        CmForFlag = false,
                        CmPaintShopValues = false
                    };
                    break;

                default:
                    packParams = new AcCommonObject.AcCommonObjectPackerParams();
                    break;
            }

            packParams.Destination = newFilename;
            packParams.ShowInExplorer = false;
            packParams.Progress = progress;
            packParams.Cancellation = cancellation;

            if (!await AcObject.TryToPack(packParams) || filename != Filename) return;

            RemoveCurrentIfNeeded();
            Filename = newFilename;
        }

        private AsyncCommand _repackCommand;
        public AsyncCommand RepackCommand => _repackCommand ?? (_repackCommand = new AsyncCommand(() => Repack()));

        private void RemoveCurrentIfNeeded() {
            var filename = Filename;
            if (filename != null && FileUtils.IsAffected(_contentDirectory, filename) && Path.GetFileName(filename).StartsWith(GetTypePrefix())) {
                _toRemove.Add(filename);
            }
        }

        public IEnumerable<string> GetFilesToRemove() {
            var result = _toRemove.ToList();
            _toRemove.Clear();
            return result;
        }
    }

    public partial class SelectedPage {
        public static readonly ShareMode[] ShareModes = EnumExtension.GetValues<ShareMode>();

        public partial class ViewModel {
            [NotNull]
            public ChangeableObservableCollection<WrapperContentObject> WrapperContentCars { get; } = new ChangeableObservableCollection<WrapperContentObject>();

            [NotNull]
            public ChangeableObservableCollection<WrapperContentObject> WrapperContentTracks { get; } = new ChangeableObservableCollection<WrapperContentObject>();

            [NotNull]
            public ChangeableObservableCollection<WrapperContentObject> WrapperContentWeather { get; } = new ChangeableObservableCollection<WrapperContentObject>();

            private void InitializeWrapperContent() {
                WrapperContentCars.ItemPropertyChanged += OnWrapperContentPropertyChanged;
                WrapperContentTracks.ItemPropertyChanged += OnWrapperContentPropertyChanged;
                WrapperContentWeather.ItemPropertyChanged += OnWrapperContentPropertyChanged;
            }

            private void OnWrapperContentPropertyChanged(object sender1, PropertyChangedEventArgs propertyChangedEventArgs) {
                SetWrapperContentState();
            }

            private IEnumerable<WrapperContentObject> GetContentCars() {
                return from c in SelectedObject.DriverEntries.GroupBy(x => x.CarId)
                       let car = CarsManager.Instance.GetById(c.Key)
                       where car != null
                       let skins = c.Select(x => x.CarSkinId).NonNull().Distinct().Select(car.GetSkinById).NonNull()
                                    .Where(x => x.CanBePacked()).Select(x => new WrapperContentObject(x, SelectedObject.WrapperContentDirectory) {
                                        ShareMode = ShareMode.None
                                    }).ToList()
                       where car?.CanBePacked() == true || skins.Count > 0
                       select new WrapperContentObject(car, SelectedObject.WrapperContentDirectory) {
                           ChildrenName = "Skins",
                           Children = skins
                       };
            }

            private void SetWrapperContentCars() {
                WrapperContentCars.ReplaceEverythingBy(new ChangeableObservableCollection<WrapperContentObject>(
                        GetContentCars().OrderBy(x => x.DisplayName)));
            }

            private void LoadWrapperContentCars() {
                WrapperContentCars.LoadFrom(SelectedObject.WrapperContentJObject?["cars"], "skins");
            }

            private readonly Busy _wrapperContentCarsBusy = new Busy();

            private void UpdateWrapperContentCars() {
                _wrapperContentCarsBusy.Do(() => {
                    SetWrapperContentCars();
                    LoadWrapperContentCars();
                });
            }

            private IEnumerable<WrapperContentObject> GetContentTracks() {
                if (SelectedObject.TrackId == null) return new WrapperContentObject[0];
                return from track in new[] { TracksManager.Instance.GetLayoutById(SelectedObject.TrackId, SelectedObject.TrackLayoutId) }
                       where track?.CanBePacked() == true
                       select new WrapperContentObject(track, SelectedObject.WrapperContentDirectory);
            }

            private void SetWrapperContentTracks() {
                WrapperContentTracks.ReplaceEverythingBy(new ChangeableObservableCollection<WrapperContentObject>(GetContentTracks()));
            }

            private void LoadWrapperContentTracks() {
                WrapperContentTracks.FirstOrDefault()?.LoadFrom(SelectedObject.WrapperContentJObject?["track"]);
            }

            private readonly Busy _wrapperContentTracksBusy = new Busy();

            private void UpdateWrapperContentTracks() {
                _wrapperContentTracksBusy.Do(() => {
                    SetWrapperContentTracks();
                    LoadWrapperContentTracks();
                });
            }

            private IEnumerable<WrapperContentObject> GetContentWeather() {
                return from c in SelectedObject.Weather.GroupBy(x => x.WeatherId)
                       let weather = WeatherManager.Instance.GetById(c.Key)
                       where weather?.CanBePacked() == true
                       select new WrapperContentObject(weather, SelectedObject.WrapperContentDirectory);
            }

            private void SetWrapperContentWeather() {
                WrapperContentWeather.ReplaceEverythingBy(new ChangeableObservableCollection<WrapperContentObject>(
                        GetContentWeather().OrderBy(x => x.DisplayName)));
            }

            private void LoadWrapperContentWeather() {
                WrapperContentWeather.LoadFrom(SelectedObject.WrapperContentJObject?["weather"]);
            }

            private readonly Busy _wrapperContentWeatherBusy = new Busy();

            private void UpdateWrapperContentWeather() {
                _wrapperContentWeatherBusy.Do(() => {
                    SetWrapperContentWeather();
                    LoadWrapperContentWeather();
                });
            }

            private void SetWrapperContentState() {
                // Building new JObject:
                var jObj = new JObject();
                WrapperContentCars.SaveTo(jObj, "cars", "skins");

                var track = new JObject();
                WrapperContentTracks.FirstOrDefault()?.SaveTo(track);
                if (track.Count > 0) {
                    jObj["track"] = track;
                } else {
                    jObj.Remove("track");
                }

                WrapperContentWeather.SaveTo(jObj, "weather");

                // Updating SelectedObject value:
                _wrapperContentCarsBusy.Do(() => _wrapperContentTracksBusy.Do(() => _wrapperContentWeatherBusy.Do(() => {
                    SelectedObject.WrapperContentJObject = jObj;
                })));
            }
        }
    }
}