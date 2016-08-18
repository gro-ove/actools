using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Lists {
    public class AutocompleteValuesList : BetterObservableCollection<string> {
        private readonly List<string> _valuesLc = new List<string>();

        public AutocompleteValuesList() { }

        public AutocompleteValuesList(IEnumerable<string> list) {
            foreach (var e in list) {
                AddUnique(e);
            }
        }

        public new void Add(string s) {
            AddUnique(s);
        }

        public new void Clear() {
            lock (_valuesLc) {
                _valuesLc.Clear();
                base.Clear();
            }
        }

        public new bool Contains(string s) {
            lock (_valuesLc) {
                return _valuesLc.Contains(s.ToLower());
            }
        }
        
        public new void RemoveAt(int pos) {
            Remove(this[pos]);
        }
        
        public new void Remove(string s) {
            lock (_valuesLc) {
                var lc = s.ToLower();
                _valuesLc.Remove(lc);
                var index = this.FindIndex(x => x.ToLower() == lc);
                if (index >= 0) {
                    base.RemoveAt(index);
                }
            }
        }
        
        public new void ReplaceEverythingBy(IEnumerable<string> values) {
            lock (_valuesLc) {
                try {
                    _valuesLc.Clear();

                    Items.Clear();
                    foreach (var item in values.NonNull()) {
                        var valueLc = item.ToLower();
                        if (_valuesLc.IndexOf(valueLc) == -1) {
                            Items.Add(item);
                            _valuesLc.Add(valueLc);
                        }
                    }

                    OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                    OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                } catch (Exception e) {
                    if (Application.Current.Dispatcher.CheckAccess()) {
                        Logging.Error("AutocompleteValuesList.AddUnique failed: " + e);
                    } else {
                        Logging.Warning("[ UI THREAD ONLY! ]");
                    }
                }
            }
        }
        
        public void AddUnique(string value) {
            if (string.IsNullOrWhiteSpace(value)) return;

            lock (_valuesLc) {
                try {
                    value = value.Trim();
                    var valueLc = value.ToLower();
                    if (_valuesLc.IndexOf(valueLc) == -1) {
                        base.Add(value);
                        _valuesLc.Add(valueLc);
                    }
                } catch (Exception e) {
                    if (Application.Current.Dispatcher.CheckAccess()) {
                        Logging.Error("AutocompleteValuesList.AddUnique failed: " + e);
                    } else {
                        Logging.Warning("[ UI THREAD ONLY! ]");
                    }
                }
            }
        }
        
        public void AddUniques(IEnumerable<string> values) {
            foreach (var value in values) {
                AddUnique(value);
            }
        }

        private ListCollectionView _view;

        public ListCollectionView View => _view ?? (_view = CreateListView());

        private ListCollectionView CreateListView() {
            var result = (ListCollectionView)CollectionViewSource.GetDefaultView(this);
            result.SortDescriptions.Add(new SortDescription());
            return result;
        }
    }
}
