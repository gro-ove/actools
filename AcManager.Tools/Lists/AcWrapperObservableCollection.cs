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
    public class CollectionReadyEventArgs : EventArgs {
        public bool JustReady { get; set; }
    }

    public interface IBaseAcObjectObservableCollection : INotifyCollectionChanged {
        event EventHandler<CollectionReadyEventArgs> CollectionReady;

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
            item.ValueChanged += OnItemValueChanged;
            item.Value.PropertyChanged += OnItemPropertyChanged;
            _index[item.Id.ToLowerInvariant()] = item;
        }

        protected override void Unsubscribe(AcItemWrapper item) {
            item.ValueChanged -= OnItemValueChanged;
            item.Value.PropertyChanged -= OnItemPropertyChanged;
            _index.Remove(item.Id.ToLowerInvariant());
        }

        public AcItemWrapper GetById(string id) {
            return _index[id.ToLowerInvariant()];
        }

        [CanBeNull]
        public AcItemWrapper GetByIdOrDefault([NotNull] string id) {
            return _index.TryGetValue(id.ToLowerInvariant(), out var result) ? result : null;
        }

        public event WrappedValueChangedEventHandler WrappedValueChanged;

        private void OnItemValueChanged(object sender, WrappedValueChangedEventArgs e) {
            e.OldValue.PropertyChanged -= OnItemPropertyChanged;
            e.NewValue.PropertyChanged += OnItemPropertyChanged;
            WrappedValueChanged?.Invoke(sender, e);
        }

        public void RefreshFilter([NotNull] AcPlaceholderNew valueObject) {
            Logging.Debug(valueObject.DisplayName);

            var wrapperObject = Items.FirstOrDefault(x => x.Value == valueObject);
            if (wrapperObject == null) {
                Logging.Warning("Wrapper object is null");
                return;
            }

            RefreshFilter(wrapperObject);
        }

        public new void Clear() {
            foreach (var item in Items) {
                Unsubscribe(item);
            }

            base.Clear();
            IsReady = false;
        }

        public event EventHandler<CollectionReadyEventArgs> CollectionReady;

        public bool IsReady { get; private set; }

        internal void Ready() {
            IsReady = true;
            CollectionReady?.Invoke(this, new CollectionReadyEventArgs { JustReady = true });
        }

        public void Update(bool force) {
            if (force || IsReady) {
                CollectionReady?.Invoke(this, new CollectionReadyEventArgs());
            }
        }
    }
}
