using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
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
                if (!Loaded) {
                    Load();
                }

                return _mainList;
            }
        }

        [CanBeNull]
        protected AcItemWrapper CurrentItem => Loaded ? _mainList.CurrentItem as AcItemWrapper : null;

        private readonly bool _allowNonSelected;

        protected BaseAcObjectListCollectionViewWrapper([NotNull] IAcManagerNew manager, IFilter<T> listFilter, bool allowNonSelected) {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            _manager = manager;
            _list = _manager.WrappersAsIList;
            _mainList = new AcWrapperCollectionView(_list);
            ListFilter = listFilter;
            _allowNonSelected = allowNonSelected;
        }

        private void List_CollectionReady(object sender, EventArgs e) {
            if (!Loaded) return;
            _mainList.Refresh();
        }

        protected bool Loaded { get; private set; }

        private bool _second;

        /// <summary>
        /// Don’t forget to use me!
        /// </summary>
        public virtual void Load() {
            if (Loaded) return;
            Loaded = true;

            if (ListFilter != null) {
                _list.ItemPropertyChanged += List_ItemPropertyChanged;
                _list.WrappedValueChanged += List_WrapperValueChanged;
            }
            _list.CollectionChanged += List_CollectionChanged;
            _list.CollectionReady += List_CollectionReady;

            if (_second) return;
            _second = true;

            using (_mainList.DeferRefresh()) {
                if (ListFilter == null) {
                    _mainList.Filter = null;
                } else {
                    _mainList.Filter = FilterTest;
                }

                _mainList.CustomSort = SortingComparer;
            }

            LoadCurrent();
            _oldNumber = _mainList.Count;
            _mainList.CurrentChanged += OnCurrentChanged;
        }

        [NotNull]
        public IComparer SortingComparer => (IComparer)_sorting ?? this;

        private AcObjectSorter<T> _sorting;

        [CanBeNull]
        public AcObjectSorter<T> Sorting {
            set {
                if (Equals(value, _sorting)) return;
                _sorting = value;
                OnPropertyChanged();

                if (Loaded) {
                    _mainList.CustomSort = SortingComparer;
                }
            }
        }

        /// <summary>
        /// Don’t forget to use me!
        /// </summary>
        public virtual void Unload() {
            if (!Loaded) return;
            Loaded = false;
            
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
            if (!Loaded || _mainList.IsEmpty) return;

            var selectedId = LoadCurrentId();
            if (selectedId == InvalidId) return;

            var selected = selectedId == null ? null : _manager.GetObjectById(selectedId);

            _userChange = false;
            if (_allowNonSelected) {
                _mainList.MoveCurrentToOrNull(selected);
            } else if (selected == null) {
                _mainList.MoveCurrentToFirst();
            } else {
                _mainList.MoveCurrentToOrFirst(selected);
            }
            _userChange = true;
        }

        protected virtual void OnCurrentChanged(object sender, EventArgs e) {
            var obj = CurrentItem;
            if (obj == null) return;
            if (_userChange) {
                SaveCurrentKey(obj.Value.Id);
            }

            if (_testMeLater != null) {
                RefreshFilter(_testMeLater);
            }
        }

        protected bool FilterTest(AcPlaceholderNew o) {
            var t = o as T;
            return t != null && ListFilter.Test(t);
        }

        protected bool FilterTest(object o) {
            var t = o as AcItemWrapper;
            return t != null && t.IsLoaded && ListFilter.Test((T)t.Value);
        }

        private int _oldNumber;

        private void MainListUpdated() {
            if (!Loaded) return;

            var newNumber = _mainList.Count;

            if (_mainList.CurrentItem == null) {
                LoadCurrent();
            }

            if (newNumber == _oldNumber) return;
            FilteredNumberChanged(_oldNumber, newNumber);
            _oldNumber = newNumber;
        }

        private AcItemWrapper _testMeLater;

        private void RefreshFilter(AcPlaceholderNew obj) {
            if (!Loaded) return;

            if (CurrentItem?.Value == obj) {
                _testMeLater = CurrentItem;
                return;
            }

            _testMeLater = null;

            var contains = _mainList.OfType<AcItemWrapper>().Any(x => x.Value == obj);
            var newValue = FilterTest(obj);

            if (contains != newValue) {
                _list.RefreshFilter(obj);
            }
        }

        private void RefreshFilter(AcItemWrapper obj) {
            if (!Loaded) return;

            if (CurrentItem == obj) {
                _testMeLater = CurrentItem;
                return;
            }

            _testMeLater = null;

            var contains = _mainList.OfType<AcItemWrapper>().Contains(obj);
            var newValue = FilterTest(obj);

            if (contains != newValue) {
                _list.RefreshFilter(obj);
            }
        }

        private void List_ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (_sorting?.IsAffectedBy(e.PropertyName) == true) {
                _list.RefreshFilter((AcPlaceholderNew)sender);
                return;
            }

            if (!ListFilter.IsAffectedBy(e.PropertyName)) return;
            RefreshFilter((AcPlaceholderNew)sender);
            MainListUpdated();
        }

        private void List_WrapperValueChanged(object sender, WrappedValueChangedEventArgs e) {
            RefreshFilter((AcItemWrapper)sender);
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
        public bool IsEmpty => !Loaded || _mainList.IsEmpty;
    }
}