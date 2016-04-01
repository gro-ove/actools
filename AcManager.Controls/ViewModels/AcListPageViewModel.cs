using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public abstract class AcListPageViewModel<T> : AcObjectListCollectionViewWrapper<T> where T : AcObjectNew {
        protected AcListPageViewModel([NotNull] IAcManagerNew list, IFilter<T> listFilter)
            : base(list, listFilter, "__aclistpageviewmodel_selected") {
        }

        protected override void FilteredNumberChanged(int oldValue, int newValue) {
            base.FilteredNumberChanged(oldValue, newValue);
            OnPropertyChanged(nameof(Status));
        }

        protected abstract string GetStatus();

        public string Status => GetStatus();
    }
}
