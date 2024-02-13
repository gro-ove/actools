using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.UserControls.Web;
using AcManager.Tools;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class TrackGeoTagsDialog {
        private ViewModel Model => (ViewModel)DataContext;
        private bool _closed;

        public TrackGeoTagsDialog(TrackObjectBase track) {
            DataContext = new ViewModel(track);
            InitializeComponent();

            Buttons = new[] {
                CreateExtraDialogButton(ToolsStrings.TrackGeoTags_FindIt, new DelegateCommand(() => {
                    // MapWebBrowser.MainTab?.Execute(@"moveTo", GetQuery(Model.Track));
                    MapWebBrowser.MainTab?.Navigate(GetMapAddress(Model.Track, true));
                })),
                CreateExtraDialogButton(FirstFloor.ModernUI.UiStrings.Ok, new CombinedCommand(Model.SaveCommand, CloseCommand)),
                CancelButton
            };

            MapWebBrowser.SetJsBridge<JsBridge>(x => x.Model = Model);
            MapWebBrowser.StartPage = GetMapAddress(track);

            Model.PropertyChanged += Model_PropertyChanged;
            UrlMonitoring();
        }

        private async void UrlMonitoring() {
            while (!_closed) {
                await Task.Delay(TimeSpan.FromMilliseconds(100d));

                var url = MapWebBrowser.MainTab?.ActiveUrl;
                if (url != null) {
                    var pos = url.Split('@');
                    if (pos.Length >= 2) {
                        var pos2 = pos[1].Split('z')[0].Split(',');
                        if (pos2.Length == 3) {
                            _skipNext = true;
                            if (Model != null) {
                                Model.Latitude = GeoTagsEntry.ToLat(pos2[0].As(Model.Latitude.As(0d)));
                                Model.Longitude = GeoTagsEntry.ToLng(pos2[1].As(Model.Longitude.As(0d)));
                                Model.ZoomLevel = pos2[2].Or(Model.ZoomLevel);
                            }
                            _skipNext = false;
                        }
                    }
                }
                /*_skipNext = true;
                if (_bridge.Model != null) {
                    _bridge.Model.Latitude = GeoTagsEntry.ToLat(lat);
                    _bridge.Model.Longitude = GeoTagsEntry.ToLng(lng);
                }
                _skipNext = false;*/
            }
        }

        protected override void OnClosingOverride(CancelEventArgs e) {
            base.OnClosingOverride(e);
        }

        private static bool _skipNext;

        private void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (_skipNext) return;
            switch (e.PropertyName) {
                case nameof(Model.Latitude):
                case nameof(Model.Longitude):
                    var pair = new GeoTagsEntry(Model.Latitude, Model.Longitude);
                    if (!pair.IsEmptyOrInvalid) {
                        // MapWebBrowser.MainTab?.Execute(@"moveTo", $@"{pair.LatitudeValue};{pair.LongitudeValue}");
                        MapWebBrowser.MainTab?.Navigate($@"https://www.google.com/maps/@{pair.LatitudeValue},{pair.LongitudeValue},{Model.ZoomLevel}z");
                    }
                    break;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class JsProxy : JsProxyCSharp {
            private JsBridge _bridge;

            public JsProxy(JsBridge bridge) : base(bridge) {
                _bridge = bridge;
            }

            [UsedImplicitly]
            public void Update(double lat, double lng) {
                Logging.Debug($"Update: lat={lat}, lng={lng}");
                Sync(() => {
                    _skipNext = true;
                    if (_bridge.Model != null) {
                        _bridge.Model.Latitude = GeoTagsEntry.ToLat(lat);
                        _bridge.Model.Longitude = GeoTagsEntry.ToLng(lng);
                    }
                    _skipNext = false;
                });
            }
        }

        public class JsBridge : JsBridgeBase {
            [CanBeNull]
            internal ViewModel Model;

            protected override JsProxyBase MakeProxy() {
                return new JsProxy(this);
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            private string _latitude;

            public string Latitude {
                get => _latitude;
                set {
                    if (value == _latitude) return;
                    _latitude = value;
                    OnPropertyChanged();
                    _saveCommand?.RaiseCanExecuteChanged();
                }
            }
            private string _longitude;

            public string Longitude {
                get => _longitude;
                set {
                    if (value == _longitude) return;
                    _longitude = value;
                    OnPropertyChanged();
                    _saveCommand?.RaiseCanExecuteChanged();
                }
            }

            private string _zoomLevel = @"14";

            public string ZoomLevel {
                get => _zoomLevel;
                set => Apply(value, ref _zoomLevel);
            }

            public ViewModel(TrackObjectBase track) {
                Track = track;

                if (track.GeoTags != null) {
                    Latitude = track.GeoTags.DisplayLatitude.Replace(@"lat", "");
                    Longitude = track.GeoTags.DisplayLongitude.Replace(@"lon", "");
                } else {
                    Latitude = null;
                    Longitude = null;
                }
            }

            public TrackObjectBase Track { get; }

            private CommandBase _saveCommand;

            public ICommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand(() => {
                Track.GeoTags = new GeoTagsEntry(Latitude, Longitude);
            }, () => Latitude != null && Longitude != null));
        }

        private static string GetQuery(TrackObjectBase track) {
            return string.IsNullOrEmpty(track.City) && string.IsNullOrEmpty(track.Country) ? track.Name :
                    new[] { track.City, track.Country }.Where(x => x != null).JoinToString(@", ");
        }

        private static string GetMapAddress(TrackObjectBase track, bool forceQuery = false) {
            var tags = track.GeoTags;
            if (forceQuery || tags?.IsEmptyOrInvalid != false) return $@"https://www.google.com/maps/search/{GetQuery(track)}";
            return $@"https://www.google.com/maps/@{tags.LatitudeValue},{tags.LongitudeValue},14z";
            return CmHelpersProvider.GetAddress("map") + @"?ms&t=new#" +
                    (tags?.IsEmptyOrInvalid == false ? $"{tags.LatitudeValue};{tags.LongitudeValue}" : GetQuery(track));
        }

        private void OnShowDevToolsClick(object sender, RoutedEventArgs e) {
            MapWebBrowser.CurrentTab?.ShowDevTools();
        }
    }
}
