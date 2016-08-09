using System.ComponentModel;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public class AcObjectListCollectionViewWrapper<T> : BaseAcObjectListCollectionViewWrapper<T> where T : AcObjectNew {
        public readonly string Key;

        public AcObjectListCollectionViewWrapper([NotNull] IAcManagerNew list, IFilter<T> listFilter, [Localizable(false)] string key, bool allowNonSelected,
                bool delayedLoad = false) : base(list, listFilter, allowNonSelected, delayedLoad) {
            Key = key + @"_" + typeof(T).Name + @"_" + listFilter?.Source;
        }

        protected override string LoadCurrentId() {
            return LimitedStorage.Get(LimitedSpace.SelectedEntry, Key);
        }

        protected override void SaveCurrentKey(string id) {
            LimitedStorage.Set(LimitedSpace.SelectedEntry, Key, id);
        }
    }
}