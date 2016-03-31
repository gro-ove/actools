using System;
using System.Collections;
using System.ComponentModel;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public abstract class BaseAcObjectListCollectionViewWrapper<T> : NotifyPropertyChanged, IComparer where T : AcObjectNew {
        protected readonly IFilter<T> ListFilter;

        [NotNull]
        private readonly IAcObjectList _list;

        [NotNull]
        private readonly AcWrapperCollectionView _mainList;

        [NotNull]
        public AcWrapperCollectionView MainList {
            get {
                if (!_loaded) {
                    Load();
                }

                return _mainList;
            }
        }

        protected AcItemWrapper CurrentItem => MainList.CurrentItem as AcItemWrapper;

        protected BaseAcObjectListCollectionViewWrapper([NotNull] IAcObjectList list, IFilter<T> listFilter) {
            _list = list;
            _mainList = new AcWrapperCollectionView(_list);

            ListFilter = listFilter;
        }

        private void List_CollectionReady(object sender, EventArgs e) {
            MainList.Refresh();
        }

        private bool _loaded, _second;

        /// <summary>
        /// Don't forget to use me!
        /// </summary>
        public virtual void Load() {
            if (_loaded) return;
            _loaded = true;

            if (ListFilter != null) {
                _list.ItemPropertyChanged += List_ItemPropertyChanged;
                _list.WrappedValueChanged += List_WrapperValueChanged;
            }
            _list.CollectionChanged += List_CollectionChanged;
            _list.CollectionReady += List_CollectionReady;

            if (_second) return;
            _second = true;

            using (MainList.DeferRefresh()) {
                if (ListFilter == null) {
                    MainList.Filter = null;
                } else {
                    MainList.Filter = FilterTest;
                }
                MainList.CustomSort = this;
            }

            LoadCurrent(true);
            _oldNumber = MainList.Count;
            MainList.CurrentChanged += OnCurrentChanged;
        }

        /// <summary>
        /// Don't forget to use me!
        /// </summary>
        public virtual void Unload() {
            if (!_loaded) return;
            _loaded = false;
            
            if (ListFilter != null) {
                _list.ItemPropertyChanged -= List_ItemPropertyChanged;
                _list.WrappedValueChanged -= List_WrapperValueChanged;
            }
            _list.CollectionChanged -= List_CollectionChanged;
            _list.CollectionReady -= List_CollectionReady;
        }

        public const string InvalidId = "";

        protected abstract string LoadCurrentId();
        protected abstract void SaveCurrentKey(string id);

        private bool _userChange = true;
        private bool _wrongItemSelected;
        private void LoadCurrent(bool orFirst) {
            if (MainList.IsEmpty) return;

            var selectedId = LoadCurrentId();
            if (selectedId == InvalidId) return;

            _userChange = false;
            if (orFirst) {
                MainList.MoveCurrentToIdOrFirst(selectedId);
            } else {
                MainList.MoveCurrentToId(selectedId);
            }
            _wrongItemSelected = CurrentItem?.Value.Id != selectedId;
            _userChange = true;
        }

        protected virtual void OnCurrentChanged(object sender, EventArgs e) {
            var obj = CurrentItem;
            if (obj == null) return;
            if (_userChange) {
                SaveCurrentKey(obj.Value.Id);
            }
        }

        protected bool FilterTest(object o) {
            var t = o as AcItemWrapper;
            return t != null && t.IsLoaded && ListFilter.Test((T)t.Value);
        }

        private int _oldNumber;

        private void MainListUpdated() {
            var newNumber = MainList.Count;

            if (MainList.CurrentItem == null) {
                LoadCurrent(true);
            } else if (_wrongItemSelected) {
                LoadCurrent(false);
            }

            if (newNumber == _oldNumber) return;
            FilteredNumberChanged(_oldNumber, newNumber);
            _oldNumber = newNumber;
        }

        private void List_ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (!ListFilter.IsAffectedBy(e.PropertyName)) return;
            _list.RefreshFilter((AcPlaceholderNew)sender);
            MainListUpdated();
        }

        private void List_WrapperValueChanged(object sender, WrappedValueChangedEventArgs e) {
            _list.RefreshFilter((AcItemWrapper)sender);
            MainListUpdated();
        }

        private void List_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            MainListUpdated();
        }

        protected virtual void FilteredNumberChanged(int oldValue, int newValue) {
            if (oldValue == 0 || newValue == 0) {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        int IComparer.Compare(object x, object y) {
            return AcItemWrapper.CompareHelper(x, y);
        }

        // TODO: remove
        public bool IsEmpty => MainList.IsEmpty;
    }
}