using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.ServerPreset {
    public partial class SelectedPage {
        public partial class ViewModel {
            private ShareMode[] _shareModes = EnumExtension.GetValues<ShareMode>();

            public ShareMode[] ShareModes {
                get => _shareModes;
                set => Apply(value, ref _shareModes);
            }

            public ServerPresetDetailsMode[] DetailsModes { get; } = EnumExtension.GetValues<ServerPresetDetailsMode>();

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
                WrapperContentCars.LoadFrom(SelectedObject.DetailsContentJObject?[@"cars"], "skins");
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
                WrapperContentTracks.FirstOrDefault()?.LoadFrom(SelectedObject.DetailsContentJObject?[@"track"]);
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
                WrapperContentWeather.LoadFrom(SelectedObject.DetailsContentJObject?[@"weather"]);
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
                WrapperContentCars.SaveTo(jObj, @"cars", @"skins");

                var track = new JObject();
                WrapperContentTracks.FirstOrDefault()?.SaveTo(track);
                if (track.Count > 0) {
                    jObj[@"track"] = track;
                } else {
                    jObj.Remove("track");
                }

                WrapperContentWeather.SaveTo(jObj, "weather");

                // Updating SelectedObject value:
                _wrapperContentCarsBusy.Do(() => _wrapperContentTracksBusy.Do(() => _wrapperContentWeatherBusy.Do(() => {
                    SelectedObject.DetailsContentJObject = jObj;
                })));
            }
        }
    }
}