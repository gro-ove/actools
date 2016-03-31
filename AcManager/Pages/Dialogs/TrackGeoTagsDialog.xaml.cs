using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows.Navigation;
using AcManager.Annotations;
using AcManager.Controls.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;

namespace AcManager.Pages.Dialogs {
    public partial class TrackGeoTagsDialog : INotifyPropertyChanged {
        public TrackObject SelectedTrack { get; private set; }

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

        private bool _setAutomatically;

        public bool SetAutomatically {
            get { return _setAutomatically; }
            set {
                if (value == _setAutomatically) return;
                _setAutomatically = value;
                OnPropertyChanged();
            }
        }

        private Timer _timer;

        public TrackGeoTagsDialog(TrackObject track) {
            InitializeComponent();
            DataContext = this;

            SelectedTrack = track;

            Buttons = new [] { OkButton, CancelButton };

            if (track.GeoTags != null) {
                Latitude = track.GeoTags.Latitude;
                Longitude = track.GeoTags.Longitude;
            } else {
                Latitude = null;
                Longitude = null;
            }

            WebBrowserHelper.SetSilent(MapWebBrowser, true);
            MapWebBrowser.Navigate(GetMapsAddress(
                FlexibleParser.TryParseDouble(Latitude) * (Latitude != null && Regex.IsMatch(Latitude, @"\bs") ? -1.0 : 1.0), 
                FlexibleParser.TryParseDouble(Longitude) * (Longitude != null && Regex.IsMatch(Longitude, @"\bw") ? -1.0 : 1.0)));

            /*_timer = new Timer(9000);
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;

            Closing += TrackGeoTagsDialog_Closing;*/
        }

        void Timer_Elapsed(object sender, ElapsedEventArgs e) {
            UpdateValues(MapWebBrowser.Source.AbsolutePath);
        }

        private string _previousPath;

        void UpdateValues(string path) {
            if (path == _previousPath) return;
            _previousPath = path;

            var match = Regex.Match(path, @"/maps/@(-?\d+(?:\.\d+)),(-?\d+(?:\.\d+))");
            if (!match.Success) return;
            Latitude = match.Groups[1].Value;
            Longitude = match.Groups[2].Value;
        }

        void TrackGeoTagsDialog_Closing(object sender, CancelEventArgs e) {
            if (_timer != null) {
                _timer.Enabled = false;
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
        }

        private string GetMapsAddress(double? lat, double? lon) {
            return string.Format("https://www.google.ru/maps/@{0},{1},13z", lat, lon);
        }

        private void MapWebBrowser_OnNavigating(object sender, NavigatingCancelEventArgs e) {
            UpdateValues(e.Uri.AbsolutePath);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
