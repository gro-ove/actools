using FirstFloor.ModernUI.Commands;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcJsonObjectNew {
        private DelegateCommand _tagsCleanUpCommand;

        public DelegateCommand TagsCleanUpCommand => _tagsCleanUpCommand ?? (_tagsCleanUpCommand = new DelegateCommand(() => {
            Tags = Tags.CleanUp();
        }));

        private DelegateCommand _tagsSortCommand;

        public DelegateCommand TagsSortCommand => _tagsSortCommand ?? (_tagsSortCommand = new DelegateCommand(() => {
            Tags = Tags.Sort();
        }));

        private DelegateCommand _tagsCleanUpAndSortCommand;

        public DelegateCommand TagsCleanUpAndSortCommand => _tagsCleanUpAndSortCommand ?? (_tagsCleanUpAndSortCommand = new DelegateCommand(() => {
            Tags = Tags.CleanUp().Sort();
        }));
    }
}
