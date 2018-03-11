using System;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.SelectionLists {
    public abstract class SelectCategoryBase : Displayable {
        private readonly string _name;

        protected SelectCategoryBase([NotNull] string name) {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public sealed override string DisplayName {
            get => _name;
            set { }
        }

        private int _itemsCount;

        public int ItemsCount {
            get => _itemsCount;
            set => Apply(value, ref _itemsCount);
        }

        private bool _isNew;

        public bool IsNew {
            get => _isNew;
            set => Apply(value, ref _isNew);
        }

        public virtual bool IsSameAs(SelectCategoryBase category) {
            return string.Equals(_name, category._name, StringComparison.OrdinalIgnoreCase);
        }

        public sealed override string ToString() => _name;

        [CanBeNull]
        internal abstract string Serialize();
    }
}