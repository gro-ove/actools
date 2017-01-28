using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AcManager.Tools.AcErrors;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using JetBrains.Annotations;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        private readonly BetterObservableCollection<IAcError> _errors = new BetterObservableCollection<IAcError>();

        public ObservableCollection<IAcError> InnerErrors => _errors;

        [NotNull]
        public virtual ObservableCollection<IAcError> Errors => _errors;

        public bool HasErrors => Errors.Count > 0;

        public void AddError(AcErrorType type, params object[] args) {
            AddError(new AcError(this, type, args));
        }

        private static bool IsSeveralAllowed(AcErrorType errorType) {
            var type = typeof(AcErrorType);
            var memInfo = type.GetMember(errorType.ToString());
            return memInfo[0].GetCustomAttributes(typeof(SeveralAllowedAttribute), false).Length > 0;
        }

        /// <summary>
        /// Add error if condition is true, remove existing if exists otherwise.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="type"></param>
        /// <param name="args"></param>
        public void ErrorIf(bool condition, AcErrorType type, params object[] args) {
            if (condition) {
                AddError(new AcError(this, type, args));
            } else {
                RemoveError(type);
            }
        }

        public void AddError(IAcError error) {
            if (HasError(error.Type) && !IsSeveralAllowed(error.Type)) return;
            _errors.Add(error);
            if (Errors.Count == 1) {
                OnPropertyChanged(nameof(HasErrors));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasError(AcErrorType type) {
            return _errors.Any(error => error.Type == type);
        }

        public void RemoveError(AcErrorType type) {
            if (!HasError(type)) return;
            _errors.Remove(_errors.FirstOrDefault(x => x.Type == type));
            if (Errors.Count == 0) {
                OnPropertyChanged(nameof(HasErrors));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void RemoveError(IAcError error) {
            if (!_errors.Contains(error)) return;
            _errors.Remove(error);
            if (Errors.Count == 0) {
                OnPropertyChanged(nameof(HasErrors));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void ClearErrors() {
            if (_errors.Count == 0) return;
            _errors.Clear();

            if (Errors.Count == 0) {
                OnPropertyChanged(nameof(HasErrors));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void ClearErrors(AcErrorCategory category) {
            if (_errors.Count == 0) return;

            for (int i; (i = _errors.FindIndex(x => x.Category == category)) != -1;) {
                _errors.RemoveAt(i);
            }

            if (Errors.Count == 0) {
                OnPropertyChanged(nameof(HasErrors));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
