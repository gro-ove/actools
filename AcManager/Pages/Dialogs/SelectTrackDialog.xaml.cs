using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using AcManager.Controls.Helpers;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
using OxyPlot.Series;

namespace AcManager.Pages.Dialogs {
    public partial class SelectTrackDialog {
        public const string UriKey = "SelectTrackDialog.UriKey";

        public SelectTrackDialog(TrackBaseObject selectedTrackConfiguration) {
            DataContext = new SelectTrackDialogViewModel(selectedTrackConfiguration);
            InitializeComponent();

            Tabs.SelectedSource = ValuesStorage.GetUri(UriKey) ?? Tabs.Links.FirstOrDefault()?.Source;
            Model.PropertyChanged += Model_PropertyChanged;
            BackgroundImage0.Source = UriToCachedImageConverter.Convert(Model.CurrentPreviewImage);

            Buttons = new[] { OkButton, CancelButton };
        }

        private int _state;

        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != "CurrentPreviewImage") return;
            (_state == 0 ? BackgroundImage1 : BackgroundImage0).Source = UriToCachedImageConverter.Convert(Model.CurrentPreviewImage);
            VisualStateManager.GoToElementState(BackgroundImage1, "State" + _state, true);
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

            _list.SelectedItem = Model.SelectedTrackConfiguration.MainTrackObject;
            _list.PropertyChanged += List_PropertyChanged;

            /* save uri */
            ValuesStorage.Set(UriKey, e.Source);
        }

        private void List_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "SelectedItem") {
                Model.SelectedTrackConfiguration = _list.SelectedItem as TrackBaseObject;
            }
        }

        public SelectTrackDialogViewModel Model => (SelectTrackDialogViewModel)DataContext;

        public class SelectTrackDialogViewModel : NotifyPropertyChanged {
            private readonly DelayedPropertyWrapper<TrackBaseObject> _selectedTrackConfiguration;

            public TrackBaseObject SelectedTrackConfiguration {
                get { return _selectedTrackConfiguration.Value; }
                set {
                    if (value == null) {
                        /* BUG: TODO: Figure out how it happens */
                        Debug.WriteLine("NULL HERE");
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

            public SelectTrackDialogViewModel(TrackBaseObject selectedTrackConfiguration) {
                _selectedTrackConfiguration = new DelayedPropertyWrapper<TrackBaseObject>(v => {
                    CurrentPreviewImage = v.PreviewImage;

                    OnPropertyChanged(nameof(SelectedTrackConfiguration));
                    OnPropertyChanged(nameof(SelectedTrack));
                });

                SelectedTrackConfiguration = selectedTrackConfiguration;
            }
        }
    }
}
