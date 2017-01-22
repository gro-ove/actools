using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public abstract class NotifyPropertyChanged : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public abstract class NotifyPropertyErrorsChanged : NotifyPropertyChanged, INotifyDataErrorInfo {
        public abstract IEnumerable GetErrors(string propertyName);

        public abstract bool HasErrors { get; }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        protected virtual void OnErrorsChanged([CallerMemberName] string propertyName = null) {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }
}
