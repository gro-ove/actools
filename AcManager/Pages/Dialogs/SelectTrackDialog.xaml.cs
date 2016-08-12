using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using AcManager.Controls.Helpers;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

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

        public SelectTrackDialog(TrackBaseObject selectedTrackConfiguration) {
            _instance = new WeakReference<SelectTrackDialog>(this);

            DataContext = new ViewModel(selectedTrackConfiguration);
            InitializeComponent();
            
            Model.PropertyChanged += Model_PropertyChanged;
            BackgroundImage0.Source = UriToCachedImageConverter.Convert(Model.CurrentPreviewImage);

            Buttons = new[] { OkButton, CancelButton };
        }

        public static TrackBaseObject Show(TrackBaseObject track) {
            var dialog = new SelectTrackDialog(track);
            dialog.ShowDialog();
            return !dialog.IsResultOk || dialog.Model.SelectedTrackConfiguration == null ? track : dialog.Model.SelectedTrackConfiguration;
        }

        private int _state;

        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(Model.CurrentPreviewImage)) return;
            (_state == 0 ? BackgroundImage1 : BackgroundImage0).Source = UriToCachedImageConverter.Convert(Model.CurrentPreviewImage);
            VisualStateManager.GoToElementState(BackgroundImage1, @"State" + _state, true);
            _state = 1 - _state;
        }

        private AcObjectSelectList.AcObjectSelectListViewModel _list;

        private void Tabs_OnNavigated(object sender, NavigationEventArgs e) {
            /* process AcObjectSelectList: unsubscribe from old, check if there is one */
            if (_list != null) {
                _list.PropertyChanged -= List_PropertyChanged;
            }

            _list = (((ModernTab)sender).Frame.Content as AcObjectSelectList)?.Model;
            if (_list == null) return;

            _list.SelectedItem = Model.SelectedTrackConfiguration?.MainTrackObject;
            _list.PropertyChanged += List_PropertyChanged;
        }

        private void List_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(_list.SelectedItem)) {
                Model.SelectedTrackConfiguration = (_list.SelectedItem as TrackObject)?.SelectedLayout ?? _list.SelectedItem as TrackBaseObject;
            }
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private readonly DelayedPropertyWrapper<TrackBaseObject> _selectedTrackConfiguration;

            [CanBeNull]
            public TrackBaseObject SelectedTrackConfiguration {
                get { return _selectedTrackConfiguration.Value; }
                set {
                    if (value == null) {
                        /* BUG: TODO: Figure out how it happens */
                        Debug.WriteLine(@"NULL HERE");
                    } else {
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

            public ViewModel(TrackBaseObject selectedTrackConfiguration) {
                _selectedTrackConfiguration = new DelayedPropertyWrapper<TrackBaseObject>(v => {
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
