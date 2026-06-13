using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcTools.Utils;
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
        void SetCurrentItem(string id);
        AcWrapperCollectionView GetAcWrapperCollectionView();
        IAcManagerNew Manager { get; }
        string SortingBy { get; }
        ContextMenu BuildListContextMenu(Action selectMultiple);
    }

    public abstract class AcListPageViewModel<T> : AcObjectListCollectionViewWrapper<T>, IAcListPageViewModel where T : AcObjectNew {
        private const string KeyBase = "Content";

        public IAcManagerNew Manager { get; }

        protected AcListPageViewModel([NotNull] IAcManagerNew list, IFilter<T> listFilter) : base(list, listFilter, KeyBase, false) {
            Manager = list;
            _sortingBy = ValuesStorage.Get<string>($"{Key}.s");
            if (_sortingBy != null) {
                Sorting = GetSortingImpl(_sortingBy);
            }
        }

        protected virtual IEnumerable<KeyValuePair<string, Type>> GetSortingTypes() {
            yield return new KeyValuePair<string, Type>("name", null);
            yield return new KeyValuePair<string, Type>("age", typeof(SortingAge));
            yield return new KeyValuePair<string, Type>("rating", typeof(SortingRating));
        }

        private class SortingAge : AcObjectSorter<T> {
            public override int Compare(T x, T y) {
                return (y.CreationDateTime - x.CreationDateTime).TotalDays.Sign();
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(AcObjectNew.Age);
            }
        }

        private class SortingRating : AcObjectSorter<T> {
            public override int Compare(T x, T y) {
                return ((y.Rating ?? -1d) - (x.Rating ?? -1d)).Sign();
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(AcObjectNew.Rating);
            }
        }

        protected AcObjectSorter<T> GetSortingImpl(string key) {
            var found = GetSortingTypes().FirstOrDefault(x => key == x.Value?.Name).Value;
            return found != null ? (AcObjectSorter<T>)Activator.CreateInstance(found) : null;
        }

        public ContextMenu BuildListContextMenu(Action selectMultiple) {
            var copyIDs = new DelegateCommand(() => ClipboardHelper.SetText(MainList.OfType<AcItemWrapper>().Select(x => x.Id).JoinToString('\n')));
            var copyTags = new DelegateCommand(() => ClipboardHelper.SetText(MainList.OfType<AcItemWrapper>().Select(x => x.Value)
                    .OfType<AcJsonObjectNew>().SelectMany(x => x.Tags).OrderBy(x => x).Distinct().JoinToString('\n')));
            var setSorting = new DelegateCommand<string>(mode => {
                SortingBy = mode;
                Sorting = GetSortingImpl(mode);
                ValuesStorage.Set($"{Key}.s", mode);
            });

            MenuItem BuildSortingItem(string displaySort, string sortArg) {
                var ret = new MenuItem { Header = $"Sort by {displaySort}", Command = setSorting, CommandParameter = sortArg, };
                ret.SetBinding(MenuItem.IsCheckedProperty, new Binding(nameof(SortingBy)) {
                    Source = this,
                    Converter = EnumToBooleanConverter.Instance,
                    ConverterParameter = sortArg
                });
                return ret;
            }

            var menu = new ContextMenu();
            foreach (var kv in GetSortingTypes()) {
                menu.Items.Add(BuildSortingItem(kv.Key, kv.Value?.Name));
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Copy IDs", Command = copyIDs });
            menu.Items.Add(new MenuItem { Header = "Copy tags", Command = copyTags });
            if (selectMultiple != null) {
                menu.Items.Add(new Separator());
                menu.Items.Add(new MenuItem {
                    Header = "Select multiple…",
                    Command = new DelegateCommand(selectMultiple),
                    ToolTip = "Alternatively, you can hold Ctrl or Shift and click a second item in the list"
                });
            }

            return menu;
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

        private string _sortingBy;

        public string SortingBy {
            get => _sortingBy;
            set => Apply(value, ref _sortingBy);
        }

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