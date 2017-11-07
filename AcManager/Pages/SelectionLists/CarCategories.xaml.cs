using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.SelectionLists {
    public class SelectCategory : SelectCategoryBase {
        public SelectCategoryDescription Description { get; }

        public string Group { get; }

        public SelectCategory(SelectCategoryDescription description) : base(description.Name) {
            Description = description;
            Group = description.Source == @"List" ? "Default" : description.Source;
        }

        internal override string Serialize() {
            throw new NotSupportedException();
        }
    }

    public abstract class SelectionCategoriesList<TObject> : SelectionList<TObject, SelectCategory> where TObject : AcObjectNew {
        protected SelectionCategoriesList([NotNull] BaseAcManager<TObject> manager) : base(manager, false) { }

        private IList<SelectCategoryInner> _categories;

        protected IList<SelectCategoryInner> Categories => _categories ?? (_categories = ReloadCategories().ToList());

        protected abstract ITester<TObject> GetTester();

        protected abstract string GetCategory();

        private IEnumerable<SelectCategoryInner> ReloadCategories() {
            var tester = GetTester();
            return SelectCategoryDescription.LoadCategories(GetCategory())
                                            .Select(x => new SelectCategoryInner(x, Filter.Create(tester, x.Filter)));
        }

        protected override ICollectionView GetCollectionView(BetterObservableCollection<SelectCategory> items) {
            var result = base.GetCollectionView(items);
            result.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SelectCategory.Group)));
            result.SortDescriptions.Add(new SortDescription(nameof(SelectCategory.Group), ListSortDirection.Ascending));
            result.SortDescriptions.Add(new SortDescription(nameof(SelectCategory.DisplayName), ListSortDirection.Ascending));
            return result;
        }

        [Localizable(false)]
        protected abstract string GetUriType();

        protected sealed override Uri GetPageAddress(SelectCategory category) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type={2}&Filter={0}&Title={1}",
                    $"enabled+&({category.Description.Filter})", category.DisplayName, GetUriType());
        }

        private bool _loaded;

        protected override void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            base.OnLoaded(sender, e);
            FilesStorage.Instance.Watcher(GetCategory()).Update += OnCategoriesUpdate;
        }

        protected override void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            base.OnUnloaded(sender, e);
            FilesStorage.Instance.Watcher(GetCategory()).Update -= OnCategoriesUpdate;
        }

        private void OnCategoriesUpdate(object sender, EventArgs e) {
            _categories = null;
            UpdateIfNeeded();
        }

        protected class SelectCategoryInner {
            public readonly SelectCategoryDescription Description;
            private readonly IFilter<TObject> _filter;

            public SelectCategoryInner(SelectCategoryDescription description, IFilter<TObject> filter) {
                Description = description;
                _filter = filter;
            }

            public bool Test([NotNull] TObject obj) {
                return _filter.Test(obj);
            }
        }

        protected sealed override SelectCategory GetSelectedItem(IList<SelectCategory> list, TObject obj) {
            var description = (from category in Categories where category.Test(obj) select category.Description).FirstOrDefault();
            return list.FirstOrDefault(x => ReferenceEquals(x.Description, description));
        }

        protected override SelectCategory LoadFromCache(string serialized) {
            throw new NotSupportedException();
        }

        protected override bool OnObjectPropertyChanged(TObject obj, PropertyChangedEventArgs e) {
            return false;
        }

        protected override void AddNewIfMissing(IList<SelectCategory> list, TObject obj) {
            var categories = Categories;
            foreach (var category in categories) {
                if (category.Test(obj)) {
                    AddNewIfMissing(list, obj, new SelectCategory(category.Description));
                }
            }
        }
    }

    public partial class CarCategories {
        public CarCategories() : base(CarsManager.Instance) {
            InitializeComponent();
        }

        protected override ITester<CarObject> GetTester() {
            return CarObjectTester.Instance;
        }

        protected override string GetCategory() {
            return ContentCategory.CarCategories;
        }

        protected override string GetUriType() {
            return "car";
        }
    }
}
