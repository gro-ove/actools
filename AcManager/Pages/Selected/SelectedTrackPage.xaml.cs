using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedTrackPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedTrackPageViewModel : SelectedAcObjectViewModel<TrackObject> {
            public SelectedTrackPageViewModel([NotNull] TrackObject acObject) : base(acObject) {
                SelectedTrackConfiguration = acObject;
            }

            private TrackBaseObject _selectedTrackConfiguration;

            public TrackBaseObject SelectedTrackConfiguration {
                get { return _selectedTrackConfiguration; }
                private set {
                    if (Equals(value, _selectedTrackConfiguration)) return;
                    _selectedTrackConfiguration = value;
                    OnPropertyChanged();
                }
            }
        }

        private void SpecsInfoBlock_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                // new TrackSpecsEditor(SelectedTrack).ShowDialog();
            }
        }

        private void GeoTags_KeyDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new TrackGeoTagsDialog(_model.SelectedObject).ShowDialog();
            }
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private TrackObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await TracksManager.Instance.GetByIdAsync(_id);
        }

        void ILoadableContent.Load() {
            _object = TracksManager.Instance.GetById(_id);
        }

        public void Initialize() {
            if (_object == null) throw new ArgumentException("Can't find object with provided ID");

            InitializeAcObjectPage(_model = new SelectedTrackPageViewModel(_object));
            InitializeComponent();
        }

        private SelectedTrackPageViewModel _model;
    }
}
