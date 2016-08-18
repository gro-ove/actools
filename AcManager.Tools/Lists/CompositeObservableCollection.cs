using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using FirstFloor.ModernUI;
using JetBrains.Annotations;

namespace AcManager.Tools.Lists {
    public class CompositeObservableCollection<T> : BetterObservableCollection<T> {
        private readonly List<ObservableCollection<T>> _childCollections = new List<ObservableCollection<T>>();

        public CompositeObservableCollection() { }

        public CompositeObservableCollection(List<T> list) : base(list) { }

        public CompositeObservableCollection([NotNull] IEnumerable<T> collection) : base(collection) { }

        public void Add(ObservableCollection<T> collection) {
            _childCollections.Add(collection);
            AddRange(collection);
            collection.CollectionChanged += Collection_CollectionChanged;
        }

        public void Remove(ObservableCollection<T> collection) {
            _childCollections.Remove(collection);
            RemoveRange(collection);
            collection.CollectionChanged -= Collection_CollectionChanged;
        }

        private void Rebuild() {
            ReplaceEverythingBy(_childCollections.SelectMany(x => x));
        }

        private void Collection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    AddRange(e.NewItems.Cast<T>());
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveRange(e.OldItems.Cast<T>());
                    break;

                case NotifyCollectionChangedAction.Reset:
                    Rebuild();
                    break;

                case NotifyCollectionChangedAction.Replace:
                    RemoveRange(e.OldItems.Cast<T>());
                    AddRange(e.NewItems.Cast<T>());
                    break;

                case NotifyCollectionChangedAction.Move:
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}