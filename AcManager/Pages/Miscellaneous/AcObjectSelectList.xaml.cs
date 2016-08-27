using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Pages.Miscellaneous {
    public interface ISelectedItemPage<T> : INotifyPropertyChanged {
        T SelectedItem { get; set; }
    }

    public interface ISelectedItemsPage<T> : ISelectedItemPage<T> {
        IEnumerable<T> GetSelectedItems();
    }

    public class ItemChosenEventArgs<T> : EventArgs {
        public ItemChosenEventArgs(T chosenItem) {
            ChosenItem = chosenItem;
        }

        public T ChosenItem { get; }
    }

    public interface IChoosingItemPage<T> {
        event EventHandler<ItemChosenEventArgs<T>> Selected;
    }

    public partial class AcObjectSelectList : ISelectedItemsPage<AcObjectNew>, IChoosingItemPage<AcObjectNew>, ITitleable, IParametrizedUriContent {
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
        }

        private AcObjectNew _selectedItem;

        public AcObjectNew SelectedItem {
            get { return _selectedItem; }
            set {
                if (Equals(value, _selectedItem)) return;
                _selectedItem = value;
                OnPropertyChanged();
                Logging.Debug("Here: " + value);
            }
        }

        public IEnumerable<AcObjectNew> GetSelectedItems() {
            return List.GetSelectedItems();
        }

        public event EventHandler<ItemChosenEventArgs<AcObjectNew>> Selected;

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
