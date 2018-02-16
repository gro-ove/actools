using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FirstFloor.ModernUI.Presentation {
    public abstract class NotifyPropertyErrorsChanged : NotifyPropertyChanged, INotifyDataErrorInfo {
        public abstract IEnumerable GetErrors(string propertyName);

        public abstract bool HasErrors { get; }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        protected virtual void OnErrorsChanged([CallerMemberName] string propertyName = null) {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }
}