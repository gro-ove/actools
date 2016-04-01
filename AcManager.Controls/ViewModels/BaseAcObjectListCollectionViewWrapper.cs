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
        [NotNull]
        private readonly IAcManagerNew _manager;

        [NotNull]
        private readonly IAcObjectList _list;

        protected readonly IFilter<T> ListFilter;

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

        protected BaseAcObjectListCollectionViewWrapper([NotNull] IAcManagerNew manager, IFilter<T> listFilter) {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            _manager = manager;
            _list = _manager.WrappersAsIList;
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

            LoadCurrent();
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

        private void LoadCurrent() {
            if (MainList.IsEmpty) return;

            var selectedId = LoadCurrentId();
            if (selectedId == InvalidId) return;

            var selected = selectedId == null ? null : _manager.GetObjectById(selectedId);

            _userChange = false;
            if (selected == null) {
                MainList.MoveCurrentToFirst();
            } else {
                MainList.MoveCurrentToOrFirst(selected);
            }
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
                LoadCurrent();
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