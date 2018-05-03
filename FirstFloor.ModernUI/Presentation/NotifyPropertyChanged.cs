using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public interface IInvokingNotifyPropertyChanged : INotifyPropertyChanged {
        void OnPropertyChanged(string propertyName);
    }

    public abstract class NotifyPropertyChanged : IInvokingNotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void IInvokingNotifyPropertyChanged.OnPropertyChanged(string propertyName) {
            OnPropertyChanged(propertyName);
        }

        protected bool Apply<T>(T value, ref T backendValue, Action onChangeCallback = null, [CallerMemberName] string propertyName = null) {
            return NotifyPropertyChangedExtension.Apply(this, value, ref backendValue, onChangeCallback, propertyName);
        }

        protected bool Apply<T>(T value, StoredValue<T> backendValue, Action onChangeCallback = null, [CallerMemberName] string propertyName = null) {
            return NotifyPropertyChangedExtension.Apply(this, value, backendValue, onChangeCallback, propertyName);
        }
    }

    /// <summary>
    /// For situations where it’s impossible to use NotifyPropertyChanged as a parent class.
    /// </summary>
    public static class NotifyPropertyChangedExtension {
        public static bool Apply<T>(this IInvokingNotifyPropertyChanged target, T value, ref T backendValue, Action onChangeCallback = null,
                [CallerMemberName] string propertyName = null) {
            if (Equals(value, backendValue)) return false;
            backendValue = value;
            onChangeCallback?.Invoke();
            target.OnPropertyChanged(propertyName);
            return true;
        }

        public static bool Apply<T>(this IInvokingNotifyPropertyChanged target, T value, StoredValue<T> backendValue, Action onChangeCallback = null,
                [CallerMemberName] string propertyName = null) {
            if (Equals(value, backendValue)) return false;
            backendValue.Value = value;
            onChangeCallback?.Invoke();
            target.OnPropertyChanged(propertyName);
            return true;
        }
    }
}
