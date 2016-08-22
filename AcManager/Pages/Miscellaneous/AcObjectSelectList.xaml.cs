using System;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Miscellaneous {
    public partial class AcObjectSelectList : ITitleable, IParametrizedUriContent {
        public ViewModel Model => (ViewModel) DataContext;

        public string Title { get; set; }

        public void OnUri(Uri uri) {
            Title = uri.GetQueryParam("Title");

            var type = uri.GetQueryParam("Type");
            var mainList = type == @"track" ? TracksManager.Instance.WrappersList
                : type == @"car" ? CarsManager.Instance.WrappersList
                : type == @"showroom" ? ShowroomsManager.Instance.WrappersList : null;
            var filter = uri.GetQueryParam("Filter");

            DataContext = new ViewModel(mainList, filter);
            InitializeComponent();
        }

        public class ViewModel : NotifyPropertyChanged {
            private AcObjectNew _selectedItem;

            public string Filter { get; }

            public IAcWrapperObservableCollection MainList { get; }

            public AcObjectNew SelectedItem {
                get { return _selectedItem; }
                set {
                    if (Equals(value, _selectedItem)) return;
                    _selectedItem = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel(IAcWrapperObservableCollection list, string filter) {
                MainList = list;
                Filter = filter;
            }
        }
    }
}
