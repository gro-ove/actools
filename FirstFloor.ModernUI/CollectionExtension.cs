using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FirstFloor.ModernUI {
    public static class CollectionExtension {
        public static void RemoveRange<T>(this Collection<T> collection, IEnumerable<T> items) {
            foreach (var item in items) {
                collection.Remove(item);
            }
        }

        public static void Replace<T>(this Collection<T> collection, T item, T newItem) {
            var index = collection.IndexOf(item);
            if (index < 0) return;
            collection[index] = newItem;
        }
    }
}