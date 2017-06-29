using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class HistoricalTextBox : BetterComboBox {
        private readonly BetterObservableCollection<string> _filtersHistory = new BetterObservableCollection<string>();
        private DateTime _lastChanged;
        private bool _changed, _initialized;

        public HistoricalTextBox() {
            DefaultStyleKey = typeof(HistoricalTextBox);

            AddHandler(FrameworkContentElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
            AddHandler(ContentElement.LostFocusEvent, new RoutedEventHandler(OnLostFocus));
            AddHandler(TextBoxBase.TextChangedEvent, new RoutedEventHandler(OnTextChanged));
            ItemsSource = _filtersHistory;

            SetCurrentValue(DefaultItemsProperty, new ObservableCollection<string>());
        }

        private bool _dirty;

        private void OnLoaded(object o, EventArgs e) {
            Update();
        }

        private void UpdateDirty() {
            _dirty = true;
            Update();
        }

        public static readonly DependencyProperty DefaultItemsProperty = DependencyProperty.Register(nameof(DefaultItems), typeof(ObservableCollection<string>),
                typeof(HistoricalTextBox), new PropertyMetadata(OnDefaultItemsChanged));

        public ObservableCollection<string> DefaultItems {
            get => (ObservableCollection<string>)GetValue(DefaultItemsProperty);
            set => SetValue(DefaultItemsProperty, value);
        }

        private static void OnDefaultItemsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HistoricalTextBox)o).OnDefaultItemsChanged((ObservableCollection<string>)e.OldValue, (ObservableCollection<string>)e.NewValue);
        }

        private void OnDefaultItemsChanged(ObservableCollection<string> oldValue, ObservableCollection<string> newValue) {
            if (oldValue != null) {
                WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.RemoveHandler(oldValue, nameof(oldValue.CollectionChanged),
                        OnCollectionChanged);
            }
            UpdateDirty();
            if (newValue != null) {
                WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(newValue, nameof(newValue.CollectionChanged),
                        OnCollectionChanged);
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateDirty();
        }

        protected override void OnDropDownOpened(EventArgs e) {
            SelectedItem = null;
            base.OnDropDownOpened(e);
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            if (e.AddedItems.Count > 0) {
                Text = e.AddedItems[0]?.ToString() ?? "";
            }
        }

        private ICommand _deleteCommand;

        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand<string>(o => {
            if (_filtersHistory.Remove(o)) {
                var saveKey = SaveKey;
                if (saveKey != null) {
                    ValuesStorage.Set(saveKey, _filtersHistory);
                }
            }
        }));

        private void OnTextChanged(object sender, RoutedEventArgs e) {
            if (!_initialized) {
                AddText();
                _initialized = true;
            }

            _changed = true;
        }

        private void OnLostFocus(object sender, RoutedEventArgs e) {
            if (_changed) {
                _changed = false;
                AddText();
            }
        }

        private void AddText() {
            var value = Text;
            if (string.IsNullOrWhiteSpace(value)) return;

            var previous = _filtersHistory.TakeWhile(x => !string.Equals(x, value, StringComparison.OrdinalIgnoreCase)).Count();
            if (previous < _filtersHistory.Count) {
                _filtersHistory.Move(previous, 0);
            } else if (DateTime.Now - _lastChanged > TimeSpan.FromSeconds(4) || _filtersHistory.Count == 0) {
                if (_filtersHistory.Count > MaxSize) {
                    _filtersHistory.RemoveAt(_filtersHistory.Count - 1);
                }

                _filtersHistory.Insert(0, value);
            } else {
                _filtersHistory[0] = value;
            }

            _lastChanged = DateTime.Now;

            var saveKey = SaveKey;
            if (saveKey != null) {
                ValuesStorage.Set(saveKey, _filtersHistory);
            }
        }

        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(HistoricalTextBox), new PropertyMetadata(null, OnSaveKeyChanged));

        public string SaveKey {
            get => (string)GetValue(SaveKeyProperty);
            set => SetValue(SaveKeyProperty, value);
        }

        private static void OnSaveKeyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HistoricalTextBox)o).UpdateDirty();
        }

        private void Update() {
            if (!IsLoaded || !_dirty) return;

            var saveKey = SaveKey;
            var items = saveKey == null ? new string[0] : ValuesStorage.GetStringList(saveKey);
            var defaultItems = DefaultItems;
            if (defaultItems.Count > 0) {
                items = items.Union(defaultItems);
            }

            _filtersHistory.ReplaceEverythingBy_Direct(items);
            _dirty = false;
        }

        public static readonly DependencyProperty MaxSizeProperty = DependencyProperty.Register(nameof(MaxSize), typeof(int),
                typeof(HistoricalTextBox), new PropertyMetadata(10, OnMaxSizeChanged));

        public int MaxSize {
            get => (int)GetValue(MaxSizeProperty);
            set => SetValue(MaxSizeProperty, value);
        }

        private static void OnMaxSizeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HistoricalTextBox)o).OnMaxSizeChanged((int)e.NewValue);
        }

        private void OnMaxSizeChanged(int newValue) {
            if (_filtersHistory.Count > newValue) {
                _filtersHistory.RemoveRange(_filtersHistory.Skip(newValue - _filtersHistory.Count));
            }
        }
    }
}