using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public static class Lazier {
        public static Lazier<T> Create<T>(Func<T> fn) {
            return new Lazier<T>(fn);
        }

        public static Lazier<T> Create<T>(Func<Task<T>> fn, T loadingValue = default(T)) {
            return new Lazier<T>(fn, loadingValue);
        }

        public static Lazier<T> Static<T>(T obj) {
            return new Lazier<T>(obj);
        }
    }

    public class Lazier<T> : INotifyPropertyChanged {
        [CanBeNull]
        private readonly Func<T> _fn;

        [CanBeNull]
        private readonly Func<Task<T>> _fnTask;
        private readonly T _loadingValue;

        private bool _isSetting;
        private int _isSettingId;
        private T _value;

        [CanBeNull]
        public T Value {
            get {
                if (!_isSet) {
                    if (_fnTask != null) {
                        if (!_isSetting) {
                            SetTask().Forget();
                        }

                        return _loadingValue;
                    }

                    IsSet = true;
                    _value = _fn == null ? default(T) : _fn.Invoke();
                }

                return _value;
            }
        }

        private async Task SetTask() {
            if (_fnTask == null) return;

            var setting = ++_isSettingId;
            _isSetting = true;

            try {
                var ready = await _fnTask();
                if (_isSettingId != setting) return;

                _value = ready;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                if (_isSettingId != setting) return;
                _value = _loadingValue;
            }

            IsSet = true;
            _isSetting = false;
            OnPropertyChanged(nameof(Value));
        }

        private bool _isSet;

        public bool IsSet {
            get => _isSet;
            set {
                if (value == _isSet) return;
                _isSet = value;
                OnPropertyChanged();
            }
        }

        public void Reset() {
            if (_fn == null && _fnTask == null) return;
            _value = default(T);
            OnPropertyChanged(nameof(Value));
            IsSet = false;

            if (_isSetting) {
                _isSettingId++;
                _isSetting = false;
            }
        }

        public Lazier(Func<Task<T>> fn, T loadingValue = default(T)) {
            _fnTask = fn;
            _loadingValue = loadingValue;
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