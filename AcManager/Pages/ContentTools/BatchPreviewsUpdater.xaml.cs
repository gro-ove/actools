using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using StringBasedFilter;

namespace AcManager.Pages.ContentTools {
    public partial class BatchPreviewsUpdater {
        protected override async Task<bool> LoadOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            await CarsManager.Instance.EnsureLoadedAsync();

            Entries = new ChangeableObservableCollection<CarObjectEntry>(
                    CarsManager.Instance.EnabledOnly.OrderBy(x => x.DisplayName).Select(x => new CarObjectEntry(x)));

            for (int i = 0; i < Entries.Count; i++) {
                var entry = Entries[i];
                progress.Report(new AsyncProgressEntry($"Loading skins ({entry.Car.DisplayName})…", i, Entries.Count));
                await entry.Car.SkinsManager.EnsureLoadedAsync();
            }

            Entries.ItemPropertyChanged += OnEntryPropertyChanged;
            SelectedEntry = Entries.FirstOrDefault();
            return Entries.Any();
        }

        private void OnEntryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(CarObjectEntry.SelectedSkins):
                    if (ReferenceEquals(sender, SelectedEntry)) {
                        UpdateSkinsListSelection();
                    }
                    break;
            }
        }

        protected override void InitializeOverride(Uri uri) {
            InitializeComponent();
        }

        private BetterListCollectionView _entriesView;

        public BetterListCollectionView EntriesView {
            get { return _entriesView; }
            set {
                if (Equals(value, _entriesView)) return;
                _entriesView = value;
                OnPropertyChanged();
            }
        }

        private ChangeableObservableCollection<CarObjectEntry> _entries;

        public ChangeableObservableCollection<CarObjectEntry> Entries {
            get { return _entries; }
            set {
                if (Equals(value, _entries)) return;
                _entries = value;
                OnPropertyChanged();

                var view = new BetterListCollectionView(value);
                using (view.DeferRefresh()) {
                    view.Filter = o => _filter == null || _filter.Test(((CarObjectEntry)o).Car);
                }

                EntriesView = view;

            }
        }

        private string _filterValue;
        private IFilter<CarObject> _filter;

        public string FilterValue {
            get { return _filterValue; }
            set {
                if (Equals(value, _filterValue)) return;
                _filterValue = value;
                _filter = string.IsNullOrWhiteSpace(value) ? null : Filter.Create(CarObjectTester.Instance, value);
                EntriesView?.Refresh();
                OnPropertyChanged();
            }
        }

        private CarObjectEntry _selectedEntry;

        public CarObjectEntry SelectedEntry {
            get { return _selectedEntry; }
            set {
                if (Equals(value, _selectedEntry)) return;

                if (_selectedEntry != null) {
                    _selectedEntry.IsCurrent = false;
                }

                _selectedEntry = value;

                if (_selectedEntry != null) {
                    _selectedEntry.IsCurrent = true;
                    UpdateSkinsListSelection();
                }

                OnPropertyChanged();
            }
        }

        public class CarObjectEntry : NotifyPropertyChanged {
            public CarObject Car { get; }

            public CarObjectEntry(CarObject car) {
                Car = car;
            }

            private ICollection<CarSkinObject> _selectedSkins = new List<CarSkinObject>(0);

            public ICollection<CarSkinObject> SelectedSkins {
                get { return _selectedSkins; }
                set {
                    if (Equals(value, _selectedSkins)) return;
                    _selectedSkins = value;
                    OnPropertyChanged();

                    if (value.Count == Car.EnabledOnlySkins.Count) {
                        if (_isSelected != true) {
                            _isSelected = true;
                            OnPropertyChanged(nameof(IsSelected));
                        }
                    } else if (value.Count == 0) {
                        if (_isSelected != false) {
                            _isSelected = false;
                            OnPropertyChanged(nameof(IsSelected));
                        }
                    } else {
                        if (_isSelected != null) {
                            _isSelected = null;
                            OnPropertyChanged(nameof(IsSelected));
                        }
                    }
                }
            }

            private bool? _isSelected = false;

            public bool? IsSelected {
                get { return _isSelected; }
                set {
                    if (value == _isSelected) return;
                    _isSelected = value;
                    OnPropertyChanged();

                    if (IsSelected == true) {
                        SelectedSkins = Car.EnabledOnlySkins;
                    } else if (IsSelected == false) {
                        SelectedSkins = new List<CarSkinObject>(0);
                    }
                }
            }

            private bool _isCurrent;

            public bool IsCurrent {
                get { return _isCurrent; }
                set {
                    if (Equals(value, _isCurrent)) return;
                    _isCurrent = value;
                    OnPropertyChanged();
                }
            }
        }

        private void OnItemSelectButtonClick(object sender, RoutedEventArgs e) {
            var item = ((FrameworkElement)sender).DataContext as CarObjectEntry;
            if (item == null) return;

            e.Handled = true;
            SelectedEntry = item;
        }

        private bool _ignore;

        private void UpdateSkinsListSelection() {
            if (_ignore) return;
            _ignore = true;

            try {
                SkinsList.SelectedItems.Clear();

                var value = SelectedEntry;
                if (value == null) return;
                foreach (var skin in value.SelectedSkins) {
                    SkinsList.SelectedItems.Add(skin);
                }
            } finally {
                _ignore = false;
            }
        }

        private void OnSkinsListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_ignore) return;
            _ignore = true;

            try {
                var item = ((FrameworkElement)sender).DataContext as CarObjectEntry;
                if (item == null) return;

                item.SelectedSkins = SkinsList.SelectedItems.OfType<CarSkinObject>().ToList();
            } finally {
                _ignore = false;
            }
        }

        private void OnListBoxPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            var listBox = (ListBox)sender;
            listBox.SelectionMode = Keyboard.Modifiers == ModifierKeys.None ? SelectionMode.Multiple : SelectionMode.Extended;
        }

        private async void OnListBoxMouseUp(object sender, EventArgs e) {
            var listBox = (ListBox)sender;
            await Task.Delay(1);
            listBox.SelectionMode = SelectionMode.Extended;
        }

        private void OnItemGotFocus(object sender, RoutedEventArgs e) {
                var item = ((FrameworkElement)sender).DataContext as CarObjectEntry;
            if (item == null) return;
            
            SelectedEntry = item;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            foreach (var entry in e.AddedItems.OfType<CarObjectEntry>()) {
                entry.IsSelected = true;
            }

            foreach (var entry in e.RemovedItems.OfType<CarObjectEntry>()) {
                entry.IsSelected = false;
            }
        }
    }
}
