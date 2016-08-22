using System;
using System.Collections.Generic;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public static class HierarchicalHelper {
        [Pure, NotNull]
        public static T HierarchicalGetById<T>([NotNull] this IEnumerable<object> source, string id) where T : class, IWithId {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source) {
                var t = i as T;
                if (t != null) {
                    if (Equals(t.Id, id)) return t;
                } else {
                    var v = (i as HierarchicalGroup)?.HierarchicalGetByIdOrDefault<T>(id);
                    if (v != null) return v;
                }
            }
            throw new Exception("Element with given ID not found");
        }

        [Pure, CanBeNull]
        public static T HierarchicalGetByIdOrDefault<T>([NotNull] this IEnumerable<object> source, string id) where T : class, IWithId {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source) {
                var t = i as T;
                if (t != null) {
                    if (Equals(t.Id, id)) return t;
                } else {
                    var v = (i as HierarchicalGroup)?.HierarchicalGetByIdOrDefault<T>(id);
                    if (v != null) return v;
                }
            }

            return null;
        }
    }
}