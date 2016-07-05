using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Navigation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class TrackGeoTagsDialog {
        private TrackGeoTagsDialogViewModel Model => (TrackGeoTagsDialogViewModel)DataContext;

        public TrackGeoTagsDialog(TrackBaseObject track) {
            DataContext = new TrackGeoTagsDialogViewModel(track);
            InitializeComponent();

            Buttons = new[] {
                CreateExtraDialogButton("Find It", new RelayCommand(o => {
                    MapWebBrowser.InvokeScript("moveTo", GetQuery(Model.Track));
                })),
                CreateExtraDialogButton(FirstFloor.ModernUI.Resources.Ok, new CombinedCommand(Model.SaveCommand, CloseCommand)),
                CancelButton
            };

            MapWebBrowser.ObjectForScripting = new ScriptProvider(Model);
            MapWebBrowser.Navigate(GetMapAddress(track));

            Model.PropertyChanged += Model_PropertyChanged;
        }

        private static bool _skipNext;

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (_skipNext) return;
            switch (e.PropertyName) {
                case nameof(Model.Latitude):
                case nameof(Model.Longitude):
                    var pair = new GeoTagsEntry(Model.Latitude, Model.Longitude);
                    if (!pair.IsEmptyOrInvalid) {
                        MapWebBrowser.InvokeScript("moveTo", pair.LatitudeValue + ";" + pair.LongitudeValue);
                    }
                    break;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        [ComVisible(true)]
        public class ScriptProvider {
            private readonly TrackGeoTagsDialogViewModel _model;

            public ScriptProvider(TrackGeoTagsDialogViewModel model) {
                _model = model;
            }

            public void Log(string message) {
                Logging.Write("[SCRIPTPROVIDER] " + message);
            }

            public void Alert(string message) {
                ShowMessage(message);
            }

            public string Prompt(string message, string defaultValue) {
                return Controls.Dialogs.Prompt.Show(message, "Webpage says", defaultValue);
            }

            public void Update(double lat, double lng) {
                _skipNext = true;
                _model.Latitude = GeoTagsEntry.ToLat(lat);
                _model.Longitude = GeoTagsEntry.ToLng(lng);
                _skipNext = false;
            }

            public object CmTest() {
                return true;
            }
        }

        public class TrackGeoTagsDialogViewModel : NotifyPropertyChanged {
            private string _latitude;

            public string Latitude {
                get { return _latitude; }
                set {
                    if (value == _latitude) return;
                    _latitude = value;
                    OnPropertyChanged();
                    SaveCommand.OnCanExecuteChanged();
                }
            }
            private string _longitude;

            public string Longitude {
                get { return _longitude; }
                set {
                    if (value == _longitude) return;
                    _longitude = value;
                    OnPropertyChanged();
                    SaveCommand.OnCanExecuteChanged();
                }
            }

            public TrackGeoTagsDialogViewModel(TrackBaseObject track) {
                Track = track;

                if (track.GeoTags != null) {
                    Latitude = track.GeoTags.Latitude;
                    Longitude = track.GeoTags.Longitude;
                } else {
                    Latitude = null;
                    Longitude = null;
                }
            }

            public TrackBaseObject Track { get; }

            private RelayCommand _saveCommand;

            public RelayCommand SaveCommand => _saveCommand ?? (_saveCommand = new RelayCommand(o => {
                Track.GeoTags = new GeoTagsEntry(Latitude, Longitude);
            }, o => Latitude != null && Longitude != null));
        }

        private static string GetQuery(TrackBaseObject track) {
            return string.IsNullOrEmpty(track.City) && string.IsNullOrEmpty(track.Country) ? track.Name :
                    new[] { track.City, track.Country }.Where(x => x != null).JoinToString(", ");
        }

        private static string GetMapAddress(TrackBaseObject track) {
            var tags = track.GeoTags;
            return CmHelpersProvider.GetAddress("map") + "?t#" +
                    (tags?.IsEmptyOrInvalid == false ? $"{tags.LatitudeValue};{tags.LongitudeValue}" : GetQuery(track));
        }

        private void MapWebBrowser_OnNavigated(object sender, NavigationEventArgs e) {
            WebBrowserHelper.SetSilent(MapWebBrowser, true);
        }
    }
}
