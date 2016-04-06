using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.Lists {
    public class AcWrapperCollectionView : BetterListCollectionView {
        public AcWrapperCollectionView(IAcObjectList list)
                : base(list) { }

        public void MoveCurrentToOrFirst(IAcObjectNew obj) {
            if (obj == null) {
                base.MoveCurrentTo(null);
                return;
            }

            var current = InternalList.Cast<AcItemWrapper>().FirstOrDefault(x => x.Value == obj);
            MoveCurrentTo(current ?? (Count > 0 ? GetItemAt(0) : null));
        }

        public void MoveCurrentToOrNull(IAcObjectNew obj) {
            if (obj == null) {
                base.MoveCurrentTo(null);
                return;
            }

            var current = InternalList.Cast<AcItemWrapper>().FirstOrDefault(x => x.Value == obj);
            MoveCurrentTo(current);
        }

        public void MoveCurrentTo(IAcObjectNew obj) {
            if (obj == null) {
                base.MoveCurrentTo(null);
                return;
            }

            var current = InternalList.Cast<AcItemWrapper>().FirstOrDefault(x => x.Value == obj);
            if (current != null) {
                MoveCurrentTo(current);
            }
        }

        public void MoveCurrentToIdOrFirst(string id) {
            if (id == null) {
                MoveCurrentToFirst();
                return;
            }

            var current = InternalList.Cast<AcItemWrapper>().FirstOrDefault(x => x.Value.Id == id);
            MoveCurrentTo(current ?? (Count > 0 ? GetItemAt(0) : null));
        }

        public void MoveCurrentToId(string id) {
            if (id == null) {
                base.MoveCurrentTo(null);
                return;
            }

            var current = InternalList.Cast<AcItemWrapper>().FirstOrDefault(x => x.Value.Id == id);
            if (current != null) {
                MoveCurrentTo(current);
            }
        }

        public AcObjectNew LoadedCurrent => (CurrentItem as AcItemWrapper)?.Loaded();
    }
}
