using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public static class Lazier {
        public static Lazier<T> Create<T>(Func<T> fn) {
            return new Lazier<T>(fn);
        }

        public static Lazier<T> Create<T>(T obj) {
            return new Lazier<T>(obj);
        }
    }
    
    public class Lazier<T> : INotifyPropertyChanged {
        private readonly Func<T> _fn;
        
        private T _value;

        [CanBeNull]
        public T Value {
            get {
                if (!_isSet) {
                    IsSet = true;
                    _value = _fn == null ? default(T) : _fn.Invoke();
                }
                return _value;
            }
        }

        private bool _isSet;

        public bool IsSet {
            get { return _isSet; }
            set {
                if (value == _isSet) return;
                _isSet = value;
                OnPropertyChanged();
            }
        }

        public void Reset() {
            if (_fn == null) return;
            _value = default(T);
            OnPropertyChanged(nameof(Value));
            IsSet = false;
        }

        public Lazier(Func<T> fn) {
            _fn = fn;
        }

        public Lazier(T obj) {
            _value = obj;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}