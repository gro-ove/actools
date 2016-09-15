using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Input;
using System.Windows.Navigation;
using AcManager.Controls;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class TrackGeoTagsDialog {
        private ViewModel Model => (ViewModel)DataContext;

        public TrackGeoTagsDialog(TrackObjectBase track) {
            DataContext = new ViewModel(track);
            InitializeComponent();

            Buttons = new[] {
                CreateExtraDialogButton(ToolsStrings.TrackGeoTags_FindIt, new ProperCommand(o => {
                    MapWebBrowser.InvokeScript(@"moveTo", GetQuery(Model.Track));
                })),
                CreateExtraDialogButton(FirstFloor.ModernUI.UiStrings.Ok, new CombinedCommand(Model.SaveCommand, CloseCommand)),
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
                        MapWebBrowser.InvokeScript(@"moveTo", pair.LatitudeValue + @";" + pair.LongitudeValue);
                    }
                    break;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        [ComVisible(true)]
        public class ScriptProvider {
            private readonly ViewModel _model;

            public ScriptProvider(ViewModel model) {
                _model = model;
            }

            public void Log(string message) {
                Logging.Write("" + message);
            }

            public void Alert(string message) {
                ShowMessage(message);
            }

            public string Prompt(string message, string defaultValue) {
                return Controls.Dialogs.Prompt.Show(message, ControlsStrings.WebBrowser_Prompt, defaultValue);
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

        public class ViewModel : NotifyPropertyChanged {
            private string _latitude;

            public string Latitude {
                get { return _latitude; }
                set {
                    if (value == _latitude) return;
                    _latitude = value;
                    OnPropertyChanged();
                    _saveCommand?.OnCanExecuteChanged();
                }
            }
            private string _longitude;

            public string Longitude {
                get { return _longitude; }
                set {
                    if (value == _longitude) return;
                    _longitude = value;
                    OnPropertyChanged();
                    _saveCommand?.OnCanExecuteChanged();
                }
            }

            public ViewModel(TrackObjectBase track) {
                Track = track;

                if (track.GeoTags != null) {
                    Latitude = track.GeoTags.Latitude;
                    Longitude = track.GeoTags.Longitude;
                } else {
                    Latitude = null;
                    Longitude = null;
                }
            }

            public TrackObjectBase Track { get; }

            private ProperCommand _saveCommand;

            public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new ProperCommand(o => {
                Track.GeoTags = new GeoTagsEntry(Latitude, Longitude);
            }, o => Latitude != null && Longitude != null));
        }

        private static string GetQuery(TrackObjectBase track) {
            return string.IsNullOrEmpty(track.City) && string.IsNullOrEmpty(track.Country) ? track.Name :
                    new[] { track.City, track.Country }.Where(x => x != null).JoinToString(@", ");
        }

        private static string GetMapAddress(TrackObjectBase track) {
            var tags = track.GeoTags;
            return CmHelpersProvider.GetAddress("map") + @"?t#" +
                    (tags?.IsEmptyOrInvalid == false ? $"{tags.LatitudeValue};{tags.LongitudeValue}" : GetQuery(track));
        }

        private void MapWebBrowser_OnNavigated(object sender, NavigationEventArgs e) {
            WebBrowserHelper.SetSilent(MapWebBrowser, true);
        }
    }
}
