using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Navigation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class TrackGeoTagsDialog {
        private TrackGeoTagsDialogViewModel Model => (TrackGeoTagsDialogViewModel)DataContext;

        public TrackGeoTagsDialog(TrackBaseObject track) {
            DataContext = new TrackGeoTagsDialogViewModel(track);
            InitializeComponent();

            Buttons = new[] { OkButton, CancelButton };
            MapWebBrowser.Navigate(CmHelpersProvider.GetAddress("map"));
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        [ComVisible(true)]
        public class TrackGeoTagsDialogViewModel : NotifyPropertyChanged {

            private string _latitude;

            public string Latitude {
                get { return _latitude; }
                set {
                    if (value == _latitude) return;
                    _latitude = value;
                    OnPropertyChanged();
                }
            }
            private string _longitude;

            public string Longitude {
                get { return _longitude; }
                set {
                    if (value == _longitude) return;
                    _longitude = value;
                    OnPropertyChanged();
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

            public void Test(string value) {
                Logging.Write("TEST: " + value);
            }
        }
        
        private static string GetMapsAddress(TrackBaseObject track) {
            return track.GeoTags?.IsEmptyOrInvalid == false ?
                    $"https://www.google.ru/maps/@{track.GeoTags.LatitudeValue},{track.GeoTags.LongitudeValue},13z" :
                    $"https://www.google.ru/maps/?q={track.City}+{track.Country}";
        }

        private void MapWebBrowser_OnNavigating(object sender, NavigatingCancelEventArgs e) {
            // UpdateValues(e.Uri.AbsolutePath);
        }

        private void MapWebBrowser_OnNavigated(object sender, NavigationEventArgs e) {
            WebBrowserHelper.SetSilent(MapWebBrowser, true);

            /*try {
                ModernDialog.ShowMessage(MapWebBrowser.InvokeScript("eval", "5+11+'called from script code'")?.ToString() ?? "NULL");
                ModernDialog.ShowMessage(MapWebBrowser.InvokeScript("eval", "window.external.Test('called from script code')")?.ToString() ?? "NULL");
            } catch (Exception ex) {
                Logging.Write("HERE: " + ex);
            }*/
        }
    }
}
