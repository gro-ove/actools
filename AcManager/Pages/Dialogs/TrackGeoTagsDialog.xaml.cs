using System;
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
                CreateExtraDialogButton(FirstFloor.ModernUI.Resources.Ok, new CombinedCommand(Model.SaveCommand, CloseCommand)),
                CancelButton
            };

            MapWebBrowser.ObjectForScripting = new ScriptProvider(Model);
            MapWebBrowser.Navigate(GetMapAddress(track));
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
                return Dialogs.Prompt.Show("Webpage says", message, defaultValue);
            }

            public void Update(double lat, double lng) {
                _model.Latitude = lat;
                _model.Longitude = lng;
            }

            public object CmTest() {
                return true;
            }
        }

        public class TrackGeoTagsDialogViewModel : NotifyPropertyChanged {

            private double? _latitude;

            public double? Latitude {
                get { return _latitude; }
                set {
                    value = value.HasValue ? Math.Round(MathUtils.Clamp(value.Value, -90d, 90d), 5) : (double?)null;
                    if (value == _latitude) return;
                    _latitude = value;
                    OnPropertyChanged();
                    SaveCommand.OnCanExecuteChanged();
                }
            }
            private double? _longitude;

            public double? Longitude {
                get { return _longitude; }
                set {
                    value = value.HasValue ? Math.Round(MathUtils.Clamp(value.Value, -180d, 180d), 5) : (double?)null;
                    if (value == _longitude) return;
                    _longitude = value;
                    OnPropertyChanged();
                    SaveCommand.OnCanExecuteChanged();
                }
            }

            public TrackGeoTagsDialogViewModel(TrackBaseObject track) {
                Track = track;

                if (track.GeoTags != null) {
                    Latitude = track.GeoTags.LatitudeValue;
                    Longitude = track.GeoTags.LongitudeValue;
                } else {
                    Latitude = null;
                    Longitude = null;
                }
            }

            public TrackBaseObject Track { get; }

            private RelayCommand _saveCommand;

            public RelayCommand SaveCommand => _saveCommand ?? (_saveCommand = new RelayCommand(o => {
                Track.GeoTags = new GeoTagsEntry(Latitude ?? 0d, Longitude ?? 0d);
            }, o => Latitude != null && Longitude != null));
        }
        
        private static string GetMapAddress(TrackBaseObject track) {
            return CmHelpersProvider.GetAddress("map") + "?t#" + (
                    track.GeoTags?.IsEmptyOrInvalid == false ?
                            $"{track.GeoTags.LatitudeValue};{track.GeoTags.LongitudeValue}" :
                            string.IsNullOrEmpty(track.City) && string.IsNullOrEmpty(track.Country) ? track.Name :
                                    new[] { track.City, track.Country }.Where(x => x != null).JoinToString(", "));
        }

        private void MapWebBrowser_OnNavigated(object sender, NavigationEventArgs e) {
            WebBrowserHelper.SetSilent(MapWebBrowser, true);
        }
    }
}
