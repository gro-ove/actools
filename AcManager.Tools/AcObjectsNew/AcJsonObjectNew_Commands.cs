using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcJsonObjectNew {
        private ICommand _tagsCleanUpCommand;

        public ICommand TagsCleanUpCommand => _tagsCleanUpCommand ?? (_tagsCleanUpCommand = new DelegateCommand(() => {
            Tags = Tags.CleanUp();
        }));

        private ICommand _tagsSortCommand;

        public ICommand TagsSortCommand => _tagsSortCommand ?? (_tagsSortCommand = new DelegateCommand(() => {
            Tags = Tags.Sort();
        }));

        private ICommand _tagsCleanUpAndSortCommand;

        public ICommand TagsCleanUpAndSortCommand => _tagsCleanUpAndSortCommand ?? (_tagsCleanUpAndSortCommand = new DelegateCommand(() => {
            Tags = Tags.CleanUp().Sort();
        }));
    }
}
