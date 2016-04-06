using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public class AcObjectListCollectionViewWrapper<T> : BaseAcObjectListCollectionViewWrapper<T> where T : AcObjectNew {
        public readonly string Key;

        public AcObjectListCollectionViewWrapper([NotNull] IAcManagerNew list, IFilter<T> listFilter, string key, bool allowNonSelected)
                : base(list, listFilter, allowNonSelected) {
            Key = key + "_" + typeof(T) + "__" + listFilter;
        }

        protected override string LoadCurrentId() {
            return ValuesStorage.GetString(Key);
        }

        protected override void SaveCurrentKey(string id) {
            ValuesStorage.Set(Key, id);
        }
    }
}