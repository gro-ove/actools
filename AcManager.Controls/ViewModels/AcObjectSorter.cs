using System.Collections;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Controls.ViewModels {
    public abstract class AcObjectSorter<T> : IComparer where T : AcObjectNew {
        int IComparer.Compare(object x, object y) {
            var xs = (x as AcItemWrapper)?.Value as T;
            var ys = (y as AcItemWrapper)?.Value as T;
            if (xs == null) return ys == null ? 0 : 1;
            if (ys == null) return -1;

            return Compare(xs, ys);
        }

        public abstract int Compare(T x, T y);

        public abstract bool IsAffectedBy(string propertyName);
    }
}