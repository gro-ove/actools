using System;
using System.ComponentModel;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public class AcObjectListCollectionViewWrapper<T> : AcObjectListCollectionViewWrapperBase<T> where T : AcObjectNew {
        public readonly string Key;

        protected static string GetKey(string basePart, string filter) {
            return basePart + @"_" + typeof(T).Name + @"_" + filter;
        }

        public AcObjectListCollectionViewWrapper([NotNull] IAcManagerNew list, IFilter<T> listFilter, [Localizable(false)] string key, bool allowNonSelected)
                : base(list, listFilter, allowNonSelected) {
            Key = GetKey(key, listFilter?.Source);
        }

        protected override string LoadCurrentId() {
            return LimitedStorage.Get(LimitedSpace.SelectedEntry, Key);
        }

        protected override void SaveCurrentKey(string id) {
            LimitedStorage.Set(LimitedSpace.SelectedEntry, Key, id);
        }
    }
}