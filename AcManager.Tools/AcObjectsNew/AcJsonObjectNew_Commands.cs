using System.Windows.Input;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcJsonObjectNew {
        private ICommand _tagsCleanUpCommand;

        public ICommand TagsCleanUpCommand => _tagsCleanUpCommand ?? (_tagsCleanUpCommand = new ProperCommand(o => {
            Tags = Tags.CleanUp();
        }));

        private ICommand _tagsSortCommand;

        public ICommand TagsSortCommand => _tagsSortCommand ?? (_tagsSortCommand = new ProperCommand(o => {
            Tags = Tags.Sort();
        }));

        private ICommand _tagsCleanUpAndSortCommand;

        public ICommand TagsCleanUpAndSortCommand => _tagsCleanUpAndSortCommand ?? (_tagsCleanUpAndSortCommand = new ProperCommand(o => {
            Tags = Tags.CleanUp().Sort();
        }));
    }
}
