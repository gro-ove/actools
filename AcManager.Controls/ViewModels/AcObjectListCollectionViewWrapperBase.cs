using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public interface IAcObjectListCollectionViewWrapper {
        void Load();
        void Unload();
    }

    public abstract class AcObjectListCollectionViewWrapperBase<T> : NotifyPropertyChanged, IAcObjectListCollectionViewWrapper, IComparer where T : AcObjectNew {
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

        protected AcObjectListCollectionViewWrapperBase([NotNull] IAcManagerNew manager, IFilter<T> listFilter, bool allowNonSelected) {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _list = _manager.WrappersAsIList;
            _mainList = new AcWrapperCollectionView(_list);
            ListFilter = listFilter;
            _allowNonSelected = allowNonSelected;
        }

        private DelegateCommand _addNewCommand;

        public DelegateCommand AddNewCommand => _addNewCommand ?? (_addNewCommand = new DelegateCommand(() => {
            try {
                (_manager as ICreatingManager)?.AddNew();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t add a new object", e);
            }
        }, () => _manager is ICreatingManager, true));

        private bool _collectionReady;

        private void OnCollectionReady(object sender, EventArgs e) {
            if (!Loaded) return;
            _collectionReady = true;
            _mainList.Refresh();
        }

        private bool _grouped;
        private string _groupByPropertyName;
        private GroupDescription _groupDescription;
        private GroupByConverter _groupByConverter;

        [CanBeNull]
        public delegate string GroupByConverter([CanBeNull] string input);

        public void GroupBy(string propertyName, GroupByConverter converter) {
            _groupByPropertyName = propertyName;
            _groupByConverter = converter;

            if (Loaded) {
                SetGrouping();
            }
        }

        public void GroupBy(string propertyName, GroupDescription description) {
            _groupByPropertyName = propertyName;
            _groupDescription = description;

            if (Loaded) {
                SetGrouping();
            }
        }

        private void SetGrouping() {
            if (_groupByPropertyName == null || _grouped) return;
            _grouped = true;
            MainList.GroupDescriptions?.Add(_groupDescription ?? new PropertyGroupDescription(
                    $@"Value.{_groupByPropertyName}",
                    _groupByConverter == null ? null : new ToGroupNameConverter(_groupByConverter)));
        }

        private class ToGroupNameConverter : IValueConverter {
            private readonly GroupByConverter _groupByConverter;

            public ToGroupNameConverter(GroupByConverter groupByConverter) {
                _groupByConverter = groupByConverter;
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value == null ? null : _groupByConverter(value.ToString());
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotImplementedException();
            }
        }

        protected bool Loaded { get; private set; }

        private bool _second;

        /// <summary>
        /// Don’t forget to use me!
        /// </summary>
        public virtual void Load() {
            if (Loaded) return;
            Loaded = true;

            SetGrouping();

            if (ListFilter != null || _grouped) {
                _list.ItemPropertyChanged += OnItemPropertyChanged;
                _list.WrappedValueChanged += OnWrapperValueChanged;
            }

            _list.CollectionChanged += OnCollectionChanged;
            _list.CollectionReady += OnCollectionReady;

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

            if (ListFilter != null || _grouped) {
                _list.ItemPropertyChanged -= OnItemPropertyChanged;
                _list.WrappedValueChanged -= OnWrapperValueChanged;
            }

            _list.CollectionChanged -= OnCollectionChanged;
            _list.CollectionReady -= OnCollectionReady;
        }

        public const string InvalidId = "";

        protected abstract string LoadCurrentId();

        protected abstract void SaveCurrentKey(string id);

        private bool _userChange = true;
        private bool _loadCurrentWaiting;

        private void LoadCurrent() {
            if (!Loaded || _mainList.IsEmpty || _loadCurrentWaiting) return;

            var selectedId = LoadCurrentId();
            if (selectedId == InvalidId) return;

            var selected = selectedId == null ? null : _manager.GetWrapperById(selectedId);
            if (selected?.IsLoaded == false) {
                _loadCurrentWaiting = true;
                selected.LoadedAsync().ContinueWith(r => ActionExtension.InvokeInMainThreadAsync(() => {
                    _mainList.MoveCurrentToOrFirst(r.Result);
                    _loadCurrentWaiting = false;
                }));
                return;
            }

            _userChange = false;
            if (_allowNonSelected) {
                _mainList.MoveCurrentToOrNull(selected?.Loaded());
            } else if (selected == null) {
                _mainList.MoveCurrentToFirst();
            } else {
                _mainList.MoveCurrentToOrFirst(selected.Loaded());
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
            return o is T t && ListFilter.Test(t);
        }

        protected bool FilterTest(object o) {
            return o is AcItemWrapper t && t.IsLoaded && ListFilter.Test((T)t.Value);
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

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (_sorting?.IsAffectedBy(e.PropertyName) == true) {
                _list.RefreshFilter((AcPlaceholderNew)sender);
                return;
            }

            if (ListFilter != null) {
                if (!ListFilter.IsAffectedBy(e.PropertyName)) return;
                RefreshFilter((AcPlaceholderNew)sender);
                MainListUpdated();
                return;
            }

            if (_grouped && e.PropertyName == _groupByPropertyName) {
                _list.RefreshFilter((AcPlaceholderNew)sender);
            }
        }

        private void OnWrapperValueChanged(object sender, WrappedValueChangedEventArgs e) {
            if (ListFilter != null) {
                RefreshFilter((AcItemWrapper)sender);
                MainListUpdated();
            } else if (_grouped && _collectionReady) {
                _list.RefreshFilter((AcItemWrapper)sender);
            }
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
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