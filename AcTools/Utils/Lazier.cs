using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public abstract class Lazier : INotifyPropertyChanged {
        // TODO: Is it even a solution? Maybe there is a better way?
        public static Action<Action> SyncAction = a => a.Invoke();

        [NotNull]
        public static Lazier<T> Create<T>([CanBeNull] Func<T> fn) {
            return new Lazier<T>(fn);
        }

        [NotNull]
        public static Lazier<T> CreateAsync<T>([CanBeNull] Func<Task<T>> fn, T loadingValue = default) {
            return new Lazier<T>(fn, loadingValue);
        }

        [NotNull]
        public static Lazier<T> Static<T>(T obj) {
            return new Lazier<T>(obj);
        }

        public abstract object GenericValue { get; }
        public abstract void StartSetting();

        private bool _isSet;

        public bool IsSet {
            get => _isSet;
            set {
                if (value == _isSet) return;
                _isSet = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Lazier<T> : Lazier {
        public Lazier([CanBeNull] Func<T> fn) {
            _fn = fn;
        }

        public Lazier([CanBeNull] T obj) {
            _value = obj;
        }

        public Lazier([CanBeNull] Func<Task<T>> fn, T loadingValue = default) {
            _fnTask = fn;
            _loadingValue = loadingValue;
        }

        [CanBeNull]
        private readonly Func<T> _fn;

        [CanBeNull]
        private readonly Func<Task<T>> _fnTask;
        private readonly T _loadingValue;

        private Task<T> _settingTask;
        private int _isSettingId;
        private T _value;

        [NotNull]
        public T RequireValue {
            get {
                var v = Value;
                if (v == null) {
                    throw new Exception($"Value of type {typeof(T).Name} is missing");
                }
                return v;
            }
        }

        public bool CheckIfIsSetting() {
            return _settingTask != null;
        }

        private readonly object _lock = new object();

        [CanBeNull]
        public T Value => GetValue();

        private T GetValue() {
            if (!IsSet) {
                if (_fnTask != null) {
                    if (_settingTask == null) {
                        _settingTask = SetTask();
                    }

                    return IsSet ? _value : _loadingValue;
                }

                lock (_lock) {
                    // TODO: SHOULD ISSET GO FIRST?
                    _value = _fn == null ? default : _fn.Invoke();
                    IsSet = true;
                }
            }
            return _value;
        }

        public override void StartSetting() {
            GetValue();
        }

        public override object GenericValue => Value;

        [ItemCanBeNull]
        public Task<T> GetValueAsync() {
            if (IsSet || _fnTask == null) return Task.FromResult(Value);
            return _settingTask ?? (_settingTask = SetTask());
        }

        private async Task<T> SetTask() {
            if (_fnTask == null) return default;

            var setting = ++_isSettingId;
            try {
                var task = _fnTask();
                if (task.IsCompleted && !task.IsFaulted) {
                    _value = task.Result;
                    IsSet = true;
                    _settingTask = null;
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(GenericValue));
                    return _value;
                }

                var ready = await task.ConfigureAwait(false);
                if (_isSettingId != setting) return default;

                _value = ready;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                if (_isSettingId != setting) return default;
                _value = _loadingValue;
            }

            SyncAction(() => {
                IsSet = true;
                _settingTask = null;
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(GenericValue));
            });

            return _value;
        }

        private bool _autoDispose;

        public bool AutoDispose {
            get => _autoDispose;
            set {
                if (Equals(value, _autoDispose)) return;
                _autoDispose = value;
                OnPropertyChanged();
            }
        }

        public void Reset() {
            if (_fn == null && _fnTask == null) return;

            if (AutoDispose) {
                (_value as IDisposable)?.Dispose();
            }

            _value = default;
            IsSet = false;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(GenericValue));

            if (_settingTask != null) {
                _isSettingId++;
                _settingTask = null;
            }
        }
    }
}