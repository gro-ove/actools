using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using JetBrains.Annotations;

namespace AcManager.Pages.SelectionLists {
    public abstract class SelectionList<TObject, TItem> : ListBox, ISelectedItemPage<AcObjectNew>, IEqualityComparer<TItem>
            where TObject : AcObjectNew
            where TItem : SelectCategoryBase {
        private readonly IAcWrapperObservableCollection _baseCollection;
        private readonly BetterObservableCollection<TItem> _items;

        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            base.OnSelectionChanged(e);
            ((UIElement)ItemContainerGenerator.ContainerFromItem(SelectedItem))?.Focus();
        }

        private void SetSelectedItem(TItem item) {
            SelectedItem = item;
            ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
        }

        private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e) {
            if (ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) {
                ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() => {
                    ((UIElement)ItemContainerGenerator.ContainerFromItem(SelectedItem)).Focus();
                }));
            }
        }

        protected SelectionList(BaseAcManager<TObject> baseCollection) {
            _baseCollection = baseCollection.WrappersList;
            _items = new BetterObservableCollection<TItem>(Rebuild());

            var itemsView = (ListCollectionView)CollectionViewSource.GetDefaultView(_items);
            itemsView.SortDescriptions.Add(new SortDescription(nameof(SelectCountry.DisplayName), ListSortDirection.Ascending));
            ItemsSource = itemsView;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            PreviewKeyDown += OnPreviewKeyDown;

            if (!TracksManager.Instance.IsLoaded) {
                EnsureLoadedAsync().Forget();
            }
        }

        private async Task EnsureLoadedAsync() {
            await TracksManager.Instance.EnsureLoadedAsync();
            SyncSelected();
        }

        private AcObjectNew _selectedAcItem;

        AcObjectNew ISelectedItemPage<AcObjectNew>.SelectedItem {
            get { return _selectedAcItem; }
            set {
                if (Equals(value, _selectedAcItem)) return;
                _selectedAcItem = value;

                SyncSelected(true);
                OnPropertyChanged();
            }
        }

        private bool _outOfSync;

        private void SyncSelected(bool force = false) {
            if (_outOfSync || force) {
                if (!_loaded) {
                    _outOfSync = true;
                    return;
                }

                var item = GetSelectedItem(_items, _selectedAcItem as TObject);
                if (item == null) {
                    _outOfSync = true;
                } else {
                    _outOfSync = false;
                    SetSelectedItem(item);
                    ScrollIntoView(item);
                }
            }
        }

        [CanBeNull]
        protected abstract void AddNewIfMissing([NotNull] IList<TItem> list, [NotNull] TObject obj);

        [CanBeNull]
        protected abstract TItem GetSelectedItem([NotNull] IList<TItem> list, [CanBeNull] TObject selected);

        protected void IncreaseCounter(TObject obj, TItem item) {
            item.ItemsCount++;
            if ((obj as AcCommonObject)?.IsNew == true) {
                item.IsNew = true;
            }
        }

        protected void AddNewIfMissing([NotNull] IList<TItem> list, [NotNull] TObject obj, [NotNull] TItem item) {
            for (var i = list.Count - 1; i >= 0; i--) {
                var existing = list[i];
                if (existing.IsSameAs(item)) {
                    IncreaseCounter(obj, existing);
                    return;
                }
            }

            list.Add(item);
            IncreaseCounter(obj, item);
            SyncSelected();
        }

        private IList<TItem> Rebuild(int defaultSize = 100) {
            var result = new List<TItem>(defaultSize);
            foreach (var obj in _baseCollection) {
                if (!obj.IsLoaded) continue;

                var value = obj.Value;
                if (value.Enabled) {
                    AddNewIfMissing(result, (TObject)value);
                }
            }

            result.Capacity = result.Count;
            return result;
        }

        private void UpdateIfNeeded() {
            var newList = Rebuild(_items.Count + 1);

            if (!_items.ReplaceIfDifferBy(newList, this)) {
                var c = newList.Count;
                for (var i = 0; i < c; i++) {
                    var n = newList[i];
                    var o = _items[i];
                    o.ItemsCount = n.ItemsCount;
                    o.IsNew = n.IsNew;
                }
            } else {
                SyncSelected(true);
            }
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            Focus();
            UpdateIfNeeded();
            _baseCollection.WrappedValueChanged += WrappersList_WrappedValueChanged;
            _baseCollection.CollectionChanged += WrappersList_CollectionChanged;
            _baseCollection.ItemPropertyChanged += WrappersList_ItemPropertyChanged;

            SyncSelected(true);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            _baseCollection.WrappedValueChanged -= WrappersList_WrappedValueChanged;
            _baseCollection.CollectionChanged -= WrappersList_CollectionChanged;
            _baseCollection.ItemPropertyChanged -= WrappersList_ItemPropertyChanged;
        }

        private void WrappersList_ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(TrackObjectBase.Country) && ((AcPlaceholderNew)sender).Enabled) {
                UpdateIfNeeded();
            }
        }

        private void WrappersList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateIfNeeded();
        }

        private void WrappersList_WrappedValueChanged(object sender, WrappedValueChangedEventArgs args) {
            var newValue = args.NewValue as TObject;
            var oldValue = args.OldValue as TObject;

            if (newValue != null) {
                if (oldValue != null) {
                    UpdateIfNeeded();
                } else if (newValue.Enabled) {
                    AddNewIfMissing(_items, newValue);
                }
            } else if (oldValue != null) {
                UpdateIfNeeded();
            }
        }

        protected abstract Uri GetPageAddress(TItem category);

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = SelectedItem as TItem;
                if (selected != null) {
                    NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
                }
            }
        }

        protected void OnItemMouseDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            var selected = ((FrameworkElement)sender).DataContext as TItem;
            if (selected != null) {
                NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
            }
        }

        bool IEqualityComparer<TItem>.Equals(TItem x, TItem y) {
            return x.IsSameAs(y);
        }

        int IEqualityComparer<TItem>.GetHashCode(TItem obj) {
            return obj.GetHashCode();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}