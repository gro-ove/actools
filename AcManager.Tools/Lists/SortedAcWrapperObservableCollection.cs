using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Lists {
    public class SortedAcWrapperObservableCollection : AcWrapperObservableCollection, IComparer<AcItemWrapper> {
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            if (IsSorted()) {
                CollectionChangedInnerInvoke(e);
            } else {
                Sort();
            }
        }

        private bool IsSorted() {
            return Items.IsOrdered(this);
        }

        private void Sort() {
            var items = Items.Sort(this).ToList();
            Items.Clear();
            foreach (var item in items) {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public override void AddRange(IEnumerable<AcItemWrapper> range) {
            throw new NotSupportedException();
        }

        public override void AddRange_Direct(IEnumerable<AcItemWrapper> range) {
            throw new NotSupportedException();
        }

        public override bool ReplaceIfDifferBy(IEnumerable<AcItemWrapper> range, IEqualityComparer<AcItemWrapper> comparer) {
            return base.ReplaceIfDifferBy(range.Sort(this), comparer);
        }

        public override void ReplaceEverythingBy_Direct(IEnumerable<AcItemWrapper> range) {
            base.ReplaceEverythingBy(range.Sort(this));
        }

        public override void ReplaceEverythingBy(IEnumerable<AcItemWrapper> range) {
            base.ReplaceEverythingBy(range.Sort(this));
        }

        protected virtual int Compare(string x, string y) {
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        public virtual int Compare(AcItemWrapper x, AcItemWrapper y) {
            return Compare(x?.Value.Id, y?.Value.Id);
        }
    }
}