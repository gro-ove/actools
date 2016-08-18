using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Selected;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

namespace AcManager.Pages.ServerPreset {
    public partial class SelectedPage : ILoadableContent, IParametrizedUriContent {
        public static ServerPresetAssistState[] AssistStates { get; } = Enum.GetValues(typeof(ServerPresetAssistState)).OfType<ServerPresetAssistState>().ToArray();

        public static ServerPresetJumpStart[] JumpStarts { get; } = Enum.GetValues(typeof(ServerPresetJumpStart)).OfType<ServerPresetJumpStart>().ToArray();

        private class ProgressCapacityConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return (value.AsDouble() - 2d) / 10d;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter ProgressCapacityConverter { get; } = new ProgressCapacityConverterInner();

        private class ClientsToBandwidthConverterInner : IMultiValueConverter {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
                if (values.Length != 2) return null;
                var hz = values[0].AsDouble();
                var mc = values[1].AsDouble();
                return 384d * hz * mc * (mc - 1) / 1e6;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IMultiValueConverter ClientsToBandwidthConverter { get; } = new ClientsToBandwidthConverterInner();

        public class ViewModel : SelectedAcObjectViewModel<ServerPresetObject> {
            private TrackObjectBase _track;

            public TrackObjectBase Track {
                get { return _track; }
                set {
                    if (Equals(value, _track)) return;
                    _track = value;
                    OnPropertyChanged();
                    
                    SelectedObject.TrackId = _track.MainTrackObject.Id;
                    SelectedObject.TrackLayoutId = _track.LayoutId;

                    MaximumCapacity = FlexibleParser.ParseInt(_track.SpecsPitboxes, 2);
                }
            }

            private int _maximumCapacity;

            public int MaximumCapacity {
                get { return _maximumCapacity; }
                set {
                    if (Equals(value, _maximumCapacity)) return;
                    _maximumCapacity = value;
                    OnPropertyChanged();
                }
            }

            public BetterObservableCollection<CarObject> Cars { get; }

            private ICommand _changeTrackCommand;

            public ICommand ChangeTrackCommand => _changeTrackCommand ?? (_changeTrackCommand = new RelayCommand(o => {
                Track = SelectTrackDialog.Show(Track);
            }));

            public ViewModel([NotNull] ServerPresetObject acObject, TrackObjectBase track, CarObject[] cars) : base(acObject) {
                SelectedObject.PropertyChanged += AcObject_PropertyChanged;

                Track = track;
                Cars = new BetterObservableCollection<CarObject>(cars);
                Cars.CollectionChanged += Cars_CollectionChanged;
            }

            private void Cars_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
                SelectedObject.CarIds = Cars.Select(x => x.Id).ToArray();
            }

            public override void Unload() {
                base.Unload();
                SelectedObject.PropertyChanged -= AcObject_PropertyChanged;
            }

            private void AcObject_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(SelectedObject.TrackId):
                    case nameof(SelectedObject.TrackLayoutId):
                        Track = TracksManager.Instance.GetLayoutById(SelectedObject.TrackId, SelectedObject.TrackLayoutId);
                        break;

                    case nameof(SelectedObject.CarIds):
                        Cars.ReplaceEverythingBy(SelectedObject.CarIds.Select(x => CarsManager.Instance.GetById(x)));
                        break;
                }
            }
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private ServerPresetObject _object;
        private TrackObjectBase _track;
        private CarObject[] _cars;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await ServerPresetsManager.Instance.GetByIdAsync(_id);
            if (_object == null) return;
            
            _track = _object.TrackId == null ? null : await TracksManager.Instance.GetLayoutByIdAsync(_object.TrackId, _object.TrackLayoutId);
            _cars = (await _object.CarIds.Select(x => CarsManager.Instance.GetByIdAsync(x)).WhenAll(4)).ToArray();
        }

        void ILoadableContent.Load() {
            _object = ServerPresetsManager.Instance.GetById(_id);
            if (_object == null) return;

            _track = _object.TrackId == null ? null : TracksManager.Instance.GetLayoutById(_object.TrackId, _object.TrackLayoutId);
            _cars = _object.CarIds.Select(x => CarsManager.Instance.GetById(x)).ToArray();
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(_model = new ViewModel(_object, _track, _cars));
            /*InputBindings.AddRange(new[] {
                new InputBinding(_model.TestCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
            });*/
            InitializeComponent();
        }

        private void SelectedObject_PropertyChanged(object sender, PropertyChangedEventArgs e) {
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            _object.PropertyChanged -= SelectedObject_PropertyChanged;
        }

        private void OnFrameNavigated(object sender, NavigationEventArgs e) {
        }
    }
}
