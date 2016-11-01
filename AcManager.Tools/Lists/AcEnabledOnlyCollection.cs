using System;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;

namespace AcManager.Tools.Lists {
    // potentially a bad place, requires reworking
    public class AcEnabledOnlyCollection<T> : BetterObservableCollection<T>, IWeakEventListener where T : AcObjectNew {
        private readonly IAcWrapperObservableCollection _collection;

        internal AcEnabledOnlyCollection(IAcWrapperObservableCollection collection) : base(collection.Select(x => x.Value).Where(x => x.Enabled).OfType<T>()) {
            _collection = collection;
            collection.CollectionChanged += Collection_CollectionChanged;
            collection.WrappedValueChanged += Collection_WrappedValueChanged;
        }

        private void Rebuild() {
            ReplaceEverythingBy(_collection.Select(x => x.Value).Where(x => x.Enabled).OfType<T>());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Collection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add: 
                    AddRange(e.NewItems.Cast<AcItemWrapper>().Select(x => x.Value).Where(x => x.Enabled).OfType<T>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems.Cast<AcItemWrapper>().Select(x => x.Value).OfType<T>()) {
                        Remove(item);
                    }
                    break;

                case NotifyCollectionChangedAction.Replace: {
                    foreach (var item in e.OldItems.Cast<AcItemWrapper>().Select(x => x.Value).OfType<T>()) {
                        Remove(item);
                    }

                    AddRange(e.NewItems.Cast<AcItemWrapper>().Select(x => x.Value).OfType<T>().Where(x => x.Enabled));
                    break;
                }

                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Reset:
                    Rebuild();
                    break;
            }
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Collection_WrappedValueChanged(object sender, WrappedValueChangedEventArgs e) {
            var o = e.OldValue as T;
            if (o != null) {
                Remove(o);
            }

            var n = e.NewValue as T;
            if (n?.Enabled != true) return;

            var i = _collection.FindIndex(x => x.Value == n);
            if (i == -1 || i == _collection.Count - 1) {
                Add(n);
                return;
            }

            var after = _collection.Take(i).LastOrDefault(x => x.IsLoaded && x.Value.Enabled);
            if (after == null) {
                Insert(0, n);
                return;
            }

            var afterLocal = Items.IndexOf(after.Value);
            if (afterLocal == -1 || afterLocal == Items.Count - 1) {
                Add(n);
                return;
            }

            Insert(afterLocal + 1, n);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e) {
            var notify = e as NotifyCollectionChangedEventArgs;
            if (notify != null) {
                Collection_CollectionChanged(sender, notify);
                return true;
            }

            var wrapped = e as WrappedValueChangedEventArgs;
            if (wrapped != null) {
                Collection_WrappedValueChanged(sender, wrapped);
                return true;
            }

            return false;
        }
    }
}
