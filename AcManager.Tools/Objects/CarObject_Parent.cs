using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Managers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public partial class CarObject {
        private void UpdateParentValues() {
            if (Parent == null) return;
            Parent?.OnPropertyChanged(nameof(Children));
            Parent?.OnPropertyChanged(nameof(HasChildren));
        }

        private string _parentId;

        [CanBeNull]
        public string ParentId {
            get { return _parentId; }
            set {
                if (value == _parentId) return;
                var oldParentId = _parentId;
                _parentId = value;

                _parent = null;
                if (_parentId != null) {
                    _parentGetted = false;
                    var parentExists = CarsManager.Instance.CheckIfIdExists(_parentId);
                    if (parentExists) {
                        RemoveError(AcErrorType.Car_ParentIsMissing);
                    } else { 
                        AddError(AcErrorType.Car_ParentIsMissing);

                        if (!LoadingInProcess) {
                            UpdateParentValues();
                        }
                    }
                } else {
                    _parentGetted = true;
                    RemoveError(AcErrorType.Car_ParentIsMissing);
                }

                OnPropertyChanged(nameof(ParentId));
                OnPropertyChanged(nameof(Parent));
                OnPropertyChanged(nameof(ParentDisplayName));

                SortAffectingValueChanged();

                if (oldParentId == null || value == null) {
                    OnPropertyChanged(nameof(IsChild));
                    OnPropertyChanged(nameof(NeedsMargin));
                }

                Changed = true;
            }
        }

        private bool _parentGetted;

        private CarObject _parent;

        [CanBeNull]
        public CarObject Parent {
            get {
                if (ParentId == null) return null;
                if (_parentGetted) return _parent;
                _parentGetted = true;
                _parent = CarsManager.Instance.GetById(ParentId);
                return _parent;
            }
        }

        [CanBeNull]
        public string ParentDisplayName => Parent?.Name ?? ParentId;

        /* TODO: Mark as loaded only? */
        public IEnumerable<CarObject> Children => CarsManager.Instance.LoadedOnly.Where(x => x.ParentId == Id);

        public bool HasChildren => Children.Any();

        public bool IsChild => ParentId != null;

        /* TODO: find another way, this one is way too shitty */
        public override bool NeedsMargin => Parent != null && (!Parent.Enabled || Enabled);
    }
}
