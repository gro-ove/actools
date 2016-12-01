using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public abstract class AcListPageViewModel<T> : AcObjectListCollectionViewWrapper<T> where T : AcObjectNew {
        private const string KeyBase = "Content";

        protected AcListPageViewModel([NotNull] IAcManagerNew list, IFilter<T> listFilter) : base(list, listFilter, KeyBase, false) {}

        protected override void FilteredNumberChanged(int oldValue, int newValue) {
            base.FilteredNumberChanged(oldValue, newValue);
            OnPropertyChanged(nameof(Status));
        }

        protected abstract string GetStatus();

        public string Status => GetStatus();

        public static void OnLinkChanged(LinkChangedEventArgs e) {
            LimitedStorage.Move(LimitedSpace.SelectedEntry, GetKey(KeyBase, e.OldValue), GetKey(KeyBase, e.NewValue));
        }
    }
}
