using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Lists {
    public interface IBaseAcObjectObservableCollection : INotifyCollectionChanged {
        event EventHandler CollectionReady;

        void RefreshFilter(AcPlaceholderNew valueObject);

        void RefreshFilter(AcItemWrapper wrapperObject);

        event PropertyChangedEventHandler ItemPropertyChanged;

        event WrappedValueChangedEventHandler WrappedValueChanged;
    }

    public interface IAcWrapperObservableCollection : IBaseAcObjectObservableCollection, IList<AcItemWrapper> { }

    /// <summary>
    /// Specially for ListCollectionView.
    /// </summary>
    public interface IAcObjectList : IBaseAcObjectObservableCollection, IList { }

    public class AcWrapperObservableCollection : ChangeableObservableCollection<AcItemWrapper>, IAcWrapperObservableCollection, IAcObjectList {
        /// <summary>
        /// TODO: Change everything? For example, use binary search instead? Or just eliminate internal list and use Dictionary only?
        /// </summary>
        private readonly Dictionary<string, AcItemWrapper> _index = new Dictionary<string, AcItemWrapper>(50);

        protected override void Subscribe(AcItemWrapper item) {
            item.ValueChanged += Item_ValueChanged;
            item.Value.PropertyChanged += Item_PropertyChanged;
            _index[item.Id.ToLowerInvariant()] = item;
        }

        protected override void Unsubscribe(AcItemWrapper item) {
            item.ValueChanged -= Item_ValueChanged;
            item.Value.PropertyChanged -= Item_PropertyChanged;
            _index.Remove(item.Id.ToLowerInvariant());
        }

        public AcItemWrapper GetById(string id) {
            return _index[id.ToLowerInvariant()];
        }

        [CanBeNull]
        public AcItemWrapper GetByIdOrDefault([NotNull] string id) {
            AcItemWrapper result;
            return _index.TryGetValue(id.ToLowerInvariant(), out result) ? result : null;
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            CollectionChangedInner?.Invoke(this, e);
        }

        public event WrappedValueChangedEventHandler WrappedValueChanged;

        private void Item_ValueChanged(object sender, WrappedValueChangedEventArgs e) {
            e.OldValue.PropertyChanged -= Item_PropertyChanged;
            e.NewValue.PropertyChanged += Item_PropertyChanged;
            WrappedValueChanged?.Invoke(sender, e);
        }

        public void RefreshFilter([NotNull] AcPlaceholderNew valueObject) {
            var wrapperObject = Items.FirstOrDefault(x => x.Value == valueObject);
            if (wrapperObject == null) {
                Logging.Warning("Wrapper object is null");
                return;
            }

            RefreshFilter(wrapperObject);
        }

        public void RefreshFilter([NotNull] AcItemWrapper wrapperObject) {
            CollectionChangedInner?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, wrapperObject, wrapperObject));
        }

        public int ListenersCount { get; private set; }

        public bool HasListeners => CollectionChangedInner != null;

        protected void CollectionChangedInnerInvoke(NotifyCollectionChangedEventArgs eventArgs) {
            CollectionChangedInner?.Invoke(this, eventArgs);
        }

        private event NotifyCollectionChangedEventHandler CollectionChangedInner;

        public event ListenersChangedEventHandler ListenersChanged;

        public override event NotifyCollectionChangedEventHandler CollectionChanged {
            add {
                CollectionChangedInner += value;
                ListenersCount++;
                ListenersChanged?.Invoke(this, new ListenersChangedEventHandlerArgs(ListenersCount, ListenersCount - 1));
            }
            remove {
                CollectionChangedInner -= value;
                ListenersCount--;
                ListenersChanged?.Invoke(this, new ListenersChangedEventHandlerArgs(ListenersCount, ListenersCount + 1));
            }
        }

        public new void Clear() {
            foreach (var item in Items) {
                Unsubscribe(item);
            }

            base.Clear();
            IsReady = false;
        }

        public event EventHandler CollectionReady;

        public bool IsReady { get; private set; }

        internal void Ready() {
            IsReady = true;
            Update();
        }

        public void Update() {
            if (!IsReady) return;
            CollectionReady?.Invoke(this, new EventArgs());
        }
    }

    public delegate void ListenersChangedEventHandler(object sender, ListenersChangedEventHandlerArgs args);

    public class ListenersChangedEventHandlerArgs {
        public readonly int NewListenersCount, OldListenersCount;

        public ListenersChangedEventHandlerArgs(int newCount, int oldCount) {
            NewListenersCount = newCount;
            OldListenersCount = oldCount;
        }
    }
}
