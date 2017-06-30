using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    public static class HierarchicalHelper {
        [Pure, NotNull]
        public static IEnumerable<object> Flatten([NotNull] this HierarchicalGroup source) {
            foreach (var i in source) {
                var g = i as HierarchicalGroup;
                if (g != null) {
                    foreach (var r in g.Flatten()) {
                        yield return r;
                    }
                } else {
                    yield return i;
                }
            }
        }

        [Pure, NotNull]
        public static T GetById<T>([NotNull] this HierarchicalGroup source, string id) where T : IWithId {
            return source.OfType<T>().GetById(id);
        }

        [Pure, CanBeNull]
        public static T GetByIdOrDefault<T>([NotNull] this HierarchicalGroup source, string id) where T : IWithId {
            return source.OfType<T>().GetByIdOrDefault(id);
        }

        [Pure, NotNull]
        public static IEnumerable<T> HierarchicalWhere<T>([NotNull] this HierarchicalGroup source, Func<T, bool> filter) {
            return source.OfType<T>().Where(filter);
        }

        [Pure, NotNull]
        public static IEnumerable<T> OfType<T>([NotNull] this HierarchicalGroup source){
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Flatten().OfType<T>();
        }

        [Pure, CanBeNull]
        public static T FirstOrDefault<T>([NotNull] this HierarchicalGroup source) {
            return source.OfType<T>().FirstOrDefault();
        }

        [Pure, CanBeNull]
        public static T FirstOrDefault<T>([NotNull] this HierarchicalGroup source, Func<T, bool> filter) {
            return source.HierarchicalWhere(filter).FirstOrDefault();
        }
    }
}