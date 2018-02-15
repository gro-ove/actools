using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public abstract class NotifyPropertyChanged : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RaiseApplyEvent(string propertyName, string[] linked) {
            OnPropertyChanged(propertyName);
            if (linked != null) {
                for (var i = 0; i < linked.Length; i++) {
                    OnPropertyChanged(linked[i]);
                }
            }
        }

        protected bool Apply<T>(T value, ref T backendValue, [CallerMemberName] string propertyName = null,
                params string[] linked) {
            if (Equals(value, backendValue)) return false;
            backendValue = value;
            RaiseApplyEvent(propertyName, linked);
            return true;
        }

        protected bool Apply<T>(T value, ref T backendValue, Action onChangeCallback, [CallerMemberName] string propertyName = null,
                params string[] linked) {
            if (Equals(value, backendValue)) return false;
            backendValue = value;
            RaiseApplyEvent(propertyName, linked);
            onChangeCallback();
            return true;
        }

        protected bool Apply<T>(T value, StoredValue<T> backendValue, [CallerMemberName] string propertyName = null,
                params string[] linked) {
            if (Equals(value, backendValue)) return false;
            backendValue.Value = value;
            RaiseApplyEvent(propertyName, linked);
            return true;
        }

        protected bool Apply<T>(T value, StoredValue<T> backendValue, Action onChangeCallback, [CallerMemberName] string propertyName = null,
                params string[] linked) {
            if (Equals(value, backendValue)) return false;
            backendValue.Value = value;
            RaiseApplyEvent(propertyName, linked);
            onChangeCallback();
            return true;
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
