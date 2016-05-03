using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Lists {
    public class AutocompleteValuesList : ObservableCollection<string> {
        private readonly List<string> _valuesLc = new List<string>();

        public AutocompleteValuesList() { }

        public AutocompleteValuesList(IEnumerable<string> list) {
            foreach (var e in list) {
                AddUnique(e);
            }
        }

        private void Add(string s, string l) {
            base.Add(s);
            _valuesLc.Add(l);
        }

        public new void Add(string s) {
            Add(s, s.ToLower());
        }

        public new void Clear() {
            _valuesLc.Clear();
            base.Clear();
        }

        public new bool Contains(string s) {
            return _valuesLc.Contains(s.ToLower());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public new void RemoveAt(int pos) {
            Remove(this[pos]);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public new void Remove(string s) {
            var lc = s.ToLower();
            _valuesLc.Remove(lc);
            var index = this.FindIndex(x => x.ToLower() == lc);
            if (index >= 0) {
                base.RemoveAt(index);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddUnique(string value) {
            if (string.IsNullOrWhiteSpace(value)) return;

            try {
                value = value.Trim();
                var valueLc = value.ToLower();
                if (_valuesLc.IndexOf(valueLc) == -1) {
                    Add(value, valueLc);
                }
            } catch (Exception e) {
                if (Application.Current.Dispatcher.CheckAccess()) {
                    Logging.Error("AutocompleteValuesList.AddUnique failed: " + e);
                } else {
                    Logging.Warning("[ UI THREAD ONLY! ]");
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
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
