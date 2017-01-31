using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Pages.Miscellaneous {
    public interface ISelectedItemPage<T> : INotifyPropertyChanged {
        T SelectedItem { get; set; }
    }

    public interface ISelectedItemsPage<T> : ISelectedItemPage<T> {
        IEnumerable<T> GetSelectedItems();
    }

    public class ItemChosenEventArgs<T> : EventArgs {
        public ItemChosenEventArgs([NotNull] T chosenItem) {
            if (chosenItem == null) throw new ArgumentNullException(nameof(chosenItem));
            ChosenItem = chosenItem;
        }

        [NotNull]
        public T ChosenItem { get; }
    }

    public interface IChoosingItemControl<T> {
        event EventHandler<ItemChosenEventArgs<T>> ItemChosen;
    }

    public sealed partial class AcObjectSelectList : ISelectedItemsPage<AcObjectNew>, IChoosingItemControl<AcObjectNew>, ITitleable, IParametrizedUriContent {
        public string Title { get; set; }

        public string Filter { get; private set; }

        public IAcWrapperObservableCollection MainList { get; private set; }

        public void OnUri(Uri uri) {
            Title = uri.GetQueryParam("Title");

            var type = uri.GetQueryParam("Type");
            MainList = type == @"track" ? TracksManager.Instance.WrappersList
                    : type == @"car" ? CarsManager.Instance.WrappersList
                            : type == @"showroom" ? ShowroomsManager.Instance.WrappersList : null;
            Filter = uri.GetQueryParam("Filter");

            DataContext = this;
            InitializeComponent();

            List.UserFiltersKey = $@"AcObjectListBox:FiltersHistory:{type}";
            SaveScroll.SetKey(List, $@".AcObjectSelectList.scroll:{type}:{Filter}");
        }

        private AcObjectNew _selectedItem;

        public AcObjectNew SelectedItem {
            get { return _selectedItem; }
            set {
                if (Equals(value, _selectedItem)) return;
                _selectedItem = value;

                if (value == null) {
                    Logging.Unexpected("Null?");
                }

                OnPropertyChanged();
            }
        }

        public IEnumerable<AcObjectNew> GetSelectedItems() {
            return List.GetSelectedItems();
        }

        public event EventHandler<ItemChosenEventArgs<AcObjectNew>> ItemChosen;

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var s = (DependencyObject)e.OriginalSource;
            var l = (s.GetParent<ListBoxItem>()?.DataContext as AcItemWrapper)?.Loaded();
            if (l != null) {
                ItemChosen?.Invoke(this, new ItemChosenEventArgs<AcObjectNew>(l));
            }
        }
    }
}
