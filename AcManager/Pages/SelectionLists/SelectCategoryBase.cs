using System;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

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

        public virtual bool IsSameAs(SelectCategoryBase category) {
            return string.Equals(_name, category._name, StringComparison.OrdinalIgnoreCase);
        }

        public sealed override string ToString() => _name;

        [CanBeNull]
        internal abstract string Serialize();
    }
}