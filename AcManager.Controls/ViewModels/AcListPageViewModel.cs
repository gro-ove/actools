using System.Linq;
using System.Windows.Input;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Controls.ViewModels {
    public interface IAcListPageViewModel {
        string GetNumberString(int count);
        string Status { get; }
        ICommand CopyIdsCommand { get; }
        ICommand CopyTagsCommand { get; }
        void SetCurrentItem(string id);
        AcWrapperCollectionView GetAcWrapperCollectionView();
        IAcManagerNew Manager { get; }
    }

    public abstract class AcListPageViewModel<T> : AcObjectListCollectionViewWrapper<T>, IAcListPageViewModel where T : AcObjectNew {
        private const string KeyBase = "Content";

        public IAcManagerNew Manager { get; }

        protected AcListPageViewModel([NotNull] IAcManagerNew list, IFilter<T> listFilter) : base(list, listFilter, KeyBase, false) {
            Manager = list;
            CopyIdsCommand = new DelegateCommand(() => ClipboardHelper.SetText(MainList.OfType<AcItemWrapper>().Select(x => x.Id).JoinToString('\n')));
            CopyTagsCommand = new DelegateCommand(() => ClipboardHelper.SetText(MainList.OfType<AcItemWrapper>().Select(x => x.Value)
                    .OfType<AcJsonObjectNew>().SelectMany(x => x.Tags).OrderBy(x => x).Distinct().JoinToString('\n')));
        }

        protected override void FilteredNumberChanged(int oldValue, int newValue) {
            base.FilteredNumberChanged(oldValue, newValue);
            OnPropertyChanged(nameof(Status));
        }

        protected abstract string GetSubject();

        public string GetNumberString(int count) {
            return PluralizingConverter.PluralizeExt(count, GetSubject());
        }

        public string Status => GetNumberString(MainList.Count);

        public ICommand CopyIdsCommand { get; }

        public ICommand CopyTagsCommand { get; }

        public void SetCurrentItem(string id) {
            var found = MainList.OfType<AcItemWrapper>().GetByIdOrDefault(id) ?? MainList.OfType<AcItemWrapper>().FirstOrDefault();
            MainList.MoveCurrentTo(found);
        }

        public AcWrapperCollectionView GetAcWrapperCollectionView() {
            return MainList;
        }

        public static void OnLinkChanged(LinkChangedEventArgs e) {
            LimitedStorage.Move(LimitedSpace.SelectedEntry, GetKey(KeyBase, e.OldValue), GetKey(KeyBase, e.NewValue));
        }
    }
}