using System;
using JetBrains.Annotations;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.SelectionLists {
    public abstract class SelectCategoryBase : Displayable {
        private readonly string _name;

        protected SelectCategoryBase([NotNull] string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));
            _name = name;
        }

        public sealed override string DisplayName {
            get { return _name; }
            set { }
        }

        private int _itemsCount;

        public int ItemsCount {
            get { return _itemsCount; }
            set {
                if (Equals(value, _itemsCount)) return;
                _itemsCount = value;
                OnPropertyChanged();
            }
        }

        private bool _isNew;

        public bool IsNew {
            get { return _isNew; }
            set {
                if (Equals(value, _isNew)) return;
                _isNew = value;
                OnPropertyChanged();
            }
        }

        public abstract bool IsSameAs(SelectCategoryBase category);

        public sealed override string ToString() => _name;
    }

    public sealed class SelectCountry : SelectCategoryBase {
        [CanBeNull]
        public string CountryId { get; }

        public SelectCountry([NotNull] string name) : base(name) {
            CountryId = DataProvider.Instance.CountryToIds.GetValueOrDefault(AcStringValues.CountryFromTag(name) ?? "");
        }

        public override bool IsSameAs(SelectCategoryBase category) {
            return (category as SelectCountry)?.CountryId == CountryId;
        }
    }
}