using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class HistoricalTextBox : BetterComboBox {
        private readonly BetterObservableCollection<string> _filtersHistory = new BetterObservableCollection<string>();
        private DateTime _lastChanged;
        private bool _changed, _initialized;

        public HistoricalTextBox() {
            DefaultStyleKey = typeof(HistoricalTextBox);
            
            AddHandler(ContentElement.LostFocusEvent, new RoutedEventHandler(OnLostFocus));
            AddHandler(TextBoxBase.TextChangedEvent, new RoutedEventHandler(OnTextChanged));
            ItemsSource = _filtersHistory;
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            Text = e.AddedItems.Count == 0 ? "" : e.AddedItems[0]?.ToString() ?? "";
        }

        private ICommand _deleteCommand;

        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new ProperCommand(o => {
            if (_filtersHistory.Remove(o as string)) {
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
            get { return (string)GetValue(SaveKeyProperty); }
            set { SetValue(SaveKeyProperty, value); }
        }

        private static void OnSaveKeyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((HistoricalTextBox)o).OnSaveKeyChanged((string)e.NewValue);
        }

        
        private void OnSaveKeyChanged(string newValue) {
            _filtersHistory.ReplaceEverythingBy(newValue == null ? new string[0] : ValuesStorage.GetStringList(newValue));
        }

        public static readonly DependencyProperty MaxSizeProperty = DependencyProperty.Register(nameof(MaxSize), typeof(int),
                typeof(HistoricalTextBox), new PropertyMetadata(10, OnMaxSizeChanged));

        public int MaxSize {
            get { return (int)GetValue(MaxSizeProperty); }
            set { SetValue(MaxSizeProperty, value); }
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