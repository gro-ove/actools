using System;
using System.ComponentModel;
using System.Windows;
using AcManager.Controls.Helpers;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Dialogs {
    public partial class SelectTrackDialog {

        private static WeakReference<SelectTrackDialog> _instance;

        public static SelectTrackDialog Instance {
            get {
                if (_instance == null) {
                    return null;
                }

                SelectTrackDialog result;
                return _instance.TryGetTarget(out result) ? result : null;
            }
        }

        public static Uri FavouritesUri() {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=track&Filter=fav+&Title={0}",
                    "Favourites");
        }

        public static Uri RatingUri(double rating) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=track&Filter={0}&Title={1}",
                    $"rating≥{Filter.Encode(rating.FloorToInt().ToInvariantString())} & rating<{Filter.Encode((rating.FloorToInt() + 1).ToInvariantString())}",
                    PluralizingConverter.PluralizeExt(rating.FloorToInt(), "{0} Star"));
        }

        public static Uri TagUri(string tag) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=track&Filter={0}&Title={1}",
                    $"enabled+&tag:{Filter.Encode(tag)}", tag);
        }

        public static Uri CountryUri(string country) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=track&Filter={0}&Title={1}",
                    $"enabled+&country:{Filter.Encode(country)}", country);
        }

        public SelectTrackDialog([NotNull] TrackObjectBase selectedTrackConfiguration) {
            _instance = new WeakReference<SelectTrackDialog>(this);

            DataContext = new ViewModel(selectedTrackConfiguration);
            InitializeComponent();
            
            Model.PropertyChanged += Model_PropertyChanged;
            BackgroundImage0.Source = UriToCachedImageConverter.Convert(Model.CurrentPreviewImage);

            Buttons = new[] { OkButton, CancelButton };
        }

        /// <summary>
        /// Returns null only when there is no tracks in the list at all and argument is null
        /// as well.
        /// </summary>
        [CanBeNull]
        public static TrackObjectBase Show([CanBeNull] TrackObjectBase track) {
            if (track == null) {
                track = TracksManager.Instance.GetDefault();
                if (track == null) return null;
            }

            var dialog = new SelectTrackDialog(track);
            dialog.ShowDialog();
            return !dialog.IsResultOk || dialog.Model.SelectedTrackConfiguration == null ? track : dialog.Model.SelectedTrackConfiguration;
        }
        
        [ContractAnnotation(@"=> track:null, false; => track:notnull, true")]
        public static bool Show([CanBeNull] ref TrackObjectBase track) {
            if (track == null) {
                track = TracksManager.Instance.GetDefault();
                if (track == null) return false;
            }

            var dialog = new SelectTrackDialog(track);
            dialog.ShowDialog();

            if (!dialog.IsResultOk || dialog.Model.SelectedTrackConfiguration == null) return false;
            track = dialog.Model.SelectedTrackConfiguration;
            return true;
        }

        private int _state;

        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(Model.CurrentPreviewImage)) return;
            (_state == 0 ? BackgroundImage1 : BackgroundImage0).Source = UriToCachedImageConverter.Convert(Model.CurrentPreviewImage);
            VisualStateManager.GoToElementState(BackgroundImage1, @"State" + _state, true);
            _state = 1 - _state;
        }

        private ISelectedItemPage<AcObjectNew> _list;
        private IChoosingItemControl<AcObjectNew> _choosing;

        private void Tabs_OnNavigated(object sender, NavigationEventArgs e) {
            if (_list != null) {
                _list.PropertyChanged -= List_PropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen -= Choosing_ItemChosen;
            }

            var content = ((ModernTab)sender).Frame.Content;
            _list = content as ISelectedItemPage<AcObjectNew>;
            _choosing = content as IChoosingItemControl<AcObjectNew>;

            if (_list != null) {
                _list.SelectedItem = Model.SelectedTrackConfiguration?.MainTrackObject;
                _list.PropertyChanged += List_PropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen += Choosing_ItemChosen;
            }
        }

        private void Choosing_ItemChosen(object sender, ItemChosenEventArgs<AcObjectNew> e) {
            var c = e.ChosenItem as TrackObjectBase;
            if (c != null) {
                Model.SelectedTrackConfiguration = c.MainTrackObject.SelectedLayout;
                CloseWithResult(MessageBoxResult.OK);
            }
        }

        private void List_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(_list.SelectedItem)) {
                Model.SelectedTrackConfiguration = (_list.SelectedItem as TrackObject)?.SelectedLayout ?? _list.SelectedItem as TrackObjectBase;
            }
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private readonly DelayedPropertyWrapper<TrackObjectBase> _selectedTrackConfiguration;

            [CanBeNull]
            public TrackObjectBase SelectedTrackConfiguration {
                get { return _selectedTrackConfiguration.Value; }
                set {
                    if (value != null) {
                        _selectedTrackConfiguration.Value = value;
                    }
                }
            }

            public TrackObject SelectedTrack => SelectedTrackConfiguration?.MainTrackObject;

            private string _currentPreviewImage;

            public string CurrentPreviewImage {
                get { return _currentPreviewImage; }
                set {
                    if (Equals(value, _currentPreviewImage)) return;
                    _currentPreviewImage = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel([NotNull] TrackObjectBase selectedTrackConfiguration) {
                _selectedTrackConfiguration = new DelayedPropertyWrapper<TrackObjectBase>(v => {
                    if (v == null) return;

                    v.MainTrackObject.SelectedLayout = v;
                    CurrentPreviewImage = v.PreviewImage;

                    OnPropertyChanged(nameof(SelectedTrackConfiguration));
                    OnPropertyChanged(nameof(SelectedTrack));
                });

                SelectedTrackConfiguration = selectedTrackConfiguration;
            }
        }
    }
}
