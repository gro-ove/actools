using System.Collections.ObjectModel;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        private readonly BetterObservableCollection<IAcError> _errors = new BetterObservableCollection<IAcError>();

        public ObservableCollection<IAcError> Errors => _errors;

        public bool HasErrors => Errors.Count > 0;

        public void AddError(AcErrorType type, params object[] args) {
            AddError(new AcError(type, args));
        }

        public void ErrorIf(bool condition, AcErrorType type, params object[] args) {
            if (condition) {
                AddError(new AcError(type, args));
            } else {
                RemoveError(type);
            }
        }

        public void AddError(IAcError error) {
            if (HasError(error.Type)) return;
            _errors.Add(error);
            if (Errors.Count == 1) {
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        public bool HasError(AcErrorType type) {
            return _errors.Any(error => error.Type == type);
        }

        public void RemoveError(AcErrorType type) {
            if (!HasError(type)) return;
            _errors.Remove(_errors.FirstOrDefault(x => x.Type == type));
            if (!_errors.Any()) {
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        public void RemoveError(IAcError error) {
            if (!_errors.Contains(error)) return;
            _errors.Remove(error);
            if (!_errors.Any()) {
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        public void ClearErrors() {
            if (!_errors.Any()) return;
            _errors.Clear();
            OnPropertyChanged(nameof(HasErrors));
        }

        public void ClearErrors(AcErrorCategory category) {
            if (!_errors.Any()) return;
            for (int i; (i = _errors.FindIndex(x => x.Category == category)) != -1;) {
                _errors.RemoveAt(i);
            }
            if (!_errors.Any()) {
                OnPropertyChanged(nameof(HasErrors));
            }
        }
    }
}
