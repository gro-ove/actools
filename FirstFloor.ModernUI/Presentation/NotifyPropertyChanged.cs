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

        protected bool Apply<T>(T value, ref T backendValue, [CallerMemberName] string propertyName = null,
                params string[] linked) {
            return NotifyPropertyChangedExtension.Apply(this, value, ref backendValue, propertyName, linked);
        }

        protected bool Apply<T>(T value, ref T backendValue, Action onChangeCallback, [CallerMemberName] string propertyName = null,
                params string[] linked) {
            return NotifyPropertyChangedExtension.Apply(this, value, ref backendValue, onChangeCallback, propertyName, linked);
        }

        protected bool Apply<T>(T value, StoredValue<T> backendValue, [CallerMemberName] string propertyName = null,
                params string[] linked) {
            return NotifyPropertyChangedExtension.Apply(this, value, backendValue, propertyName, linked);
        }

        protected bool Apply<T>(T value, StoredValue<T> backendValue, Action onChangeCallback, [CallerMemberName] string propertyName = null,
                params string[] linked) {
            return NotifyPropertyChangedExtension.Apply(this, value, backendValue, onChangeCallback, propertyName, linked);
        }
    }

    /// <summary>
    /// For situations where it’s impossible to use NotifyPropertyChanged as a parent class.
    /// </summary>
    public static class NotifyPropertyChangedExtension {
        private static void RaiseApplyEvent(IInvokingNotifyPropertyChanged target, string propertyName, string[] linked) {
            target.OnPropertyChanged(propertyName);
            if (linked != null) {
                for (var i = 0; i < linked.Length; i++) {
                    target.OnPropertyChanged(linked[i]);
                }
            }
        }

        public static bool Apply<T>(this IInvokingNotifyPropertyChanged target, T value, ref T backendValue,
                [CallerMemberName] string propertyName = null, params string[] linked) {
            if (Equals(value, backendValue)) return false;
            backendValue = value;
            RaiseApplyEvent(target, propertyName, linked);
            return true;
        }

        public static bool Apply<T>(this IInvokingNotifyPropertyChanged target, T value, ref T backendValue, Action onChangeCallback,
                [CallerMemberName] string propertyName = null, params string[] linked) {
            if (Equals(value, backendValue)) return false;
            backendValue = value;
            RaiseApplyEvent(target, propertyName, linked);
            onChangeCallback();
            return true;
        }

        public static bool Apply<T>(this IInvokingNotifyPropertyChanged target, T value, StoredValue<T> backendValue,
                [CallerMemberName] string propertyName = null, params string[] linked) {
            if (Equals(value, backendValue)) return false;
            backendValue.Value = value;
            RaiseApplyEvent(target, propertyName, linked);
            return true;
        }

        public static bool Apply<T>(this IInvokingNotifyPropertyChanged target, T value, StoredValue<T> backendValue, Action onChangeCallback,
                [CallerMemberName] string propertyName = null, params string[] linked) {
            if (Equals(value, backendValue)) return false;
            backendValue.Value = value;
            RaiseApplyEvent(target, propertyName, linked);
            onChangeCallback();
            return true;
        }
    }
}
