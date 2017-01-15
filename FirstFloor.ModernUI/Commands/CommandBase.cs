using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Commands {
    public abstract class CommandBase : ICommand, INotifyPropertyChanged {
        public readonly bool AlwaysCanExecute;
        public readonly bool IsAutomaticRequeryDisabled;

        protected CommandBase(bool alwaysCanExecute, bool isAutomaticRequeryDisabled) {
            AlwaysCanExecute = alwaysCanExecute;
            IsAutomaticRequeryDisabled = isAutomaticRequeryDisabled;
        }

        private List<WeakReference> _canExecuteChangedHandlers;

        public void RaiseCanExecuteChanged() {
            OnPropertyChanged(nameof(IsAbleToExecute));
            CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
            if (IsAutomaticRequeryDisabled) {
                CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
            } else {
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsAbleToExecute => AlwaysCanExecute || CanExecuteOverride(null);

        protected abstract bool CanExecuteOverride(object parameter);

        protected abstract void ExecuteOverride(object parameter);

        bool ICommand.CanExecute(object parameter) {
            return CanExecuteOverride(parameter);
        }

        void ICommand.Execute(object parameter) {
            ExecuteOverride(parameter);
        }

        public event EventHandler CanExecuteChanged {
            add {
                if (!AlwaysCanExecute) {
                    if (IsAutomaticRequeryDisabled) {
                        CommandManagerHelper.AddWeakReferenceHandler(ref _canExecuteChangedHandlers, value, 2);
                    } else {
                        CommandManager.RequerySuggested += value;
                    }
                }
            }
            remove {
                if (!AlwaysCanExecute) {
                    if (IsAutomaticRequeryDisabled) {
                        CommandManagerHelper.RemoveWeakReferenceHandler(_canExecuteChangedHandlers, value);
                    } else {
                        CommandManager.RequerySuggested -= value;
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public abstract class CommandExt : CommandBase {
        protected CommandExt(bool alwaysCanExecute, bool isAutomaticRequeryDisabled) : base(alwaysCanExecute, isAutomaticRequeryDisabled) { }

        protected sealed override void ExecuteOverride(object parameter) {
            ExecuteOverride();
        }

        protected sealed override bool CanExecuteOverride(object parameter) {
            return AlwaysCanExecute || CanExecuteOverride();
        }

        public bool CanExecute() {
            return AlwaysCanExecute || CanExecuteOverride();
        }

        public void Execute() {
            if (AlwaysCanExecute || CanExecuteOverride()) {
                ExecuteOverride();
            }
        }

        protected abstract bool CanExecuteOverride();

        protected abstract void ExecuteOverride();
    }

    public abstract class CommandExt<T> : CommandBase {
        public bool XamlCompatible { get; set; } = true;

        protected CommandExt(bool alwaysCanExecute, bool isAutomaticRequeryDisabled) : base(alwaysCanExecute, isAutomaticRequeryDisabled) {}

        private bool ConvertXamlCompatible([NotNull] object parameter, out T result) {
            if (typeof(T) == typeof(string)) {
                /* we don’t need to check on null or if it’s already a string — those
                 * cases would be solved before, by Convert() */
                result = (T)(object)parameter.ToString();
                return true;
            }

            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            var asString = parameter as string ?? parameter.ToString();
            if (type == typeof(bool)) {
                bool value;
                if (bool.TryParse(asString, out value)) {
                    result = (T)(object)value;
                    return true;
                }
            } else if (typeof(T) == typeof(int)) {
                int value;
                if (int.TryParse(asString, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                    result = (T)(object)value;
                    return true;
                }
            } else if(typeof(T) == typeof(double)) {
                double value;
                if (double.TryParse(asString, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                    result = (T)(object)value;
                    return true;
                }
            } else if (typeof(T) == typeof(long)) {
                long value;
                if (long.TryParse(asString, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                    result = (T)(object)value;
                    return true;
                }
            } else if(typeof(T) == typeof(float)) {
                float value;
                if (float.TryParse(asString, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
                    result = (T)(object)value;
                    return true;
                }
            }

            result = default(T);
            return false;
        }

        private bool Convert(object value, out T result) {
            if (value is T) {
                result = (T)value;
                return true;
            }

            if (value == null) {
                /* nulls are appropriate only for non-value or nullable types */
                if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null) {
                    result = default(T);
                    return false;
                }

                result = (T)(object)null;
                return true;
            }

            if (XamlCompatible && ConvertXamlCompatible(value, out result)) {
                return true;
            }

            result = default(T);
            return false;
        }

        protected sealed override void ExecuteOverride(object parameter) {
            T value;
            if (Convert(parameter, out value)) {
                ExecuteOverride(value);
            } else {
                Logging.Error($"Invalid type: {parameter?.GetType().Name ?? @"<NULL>"} (required: {typeof(T)})");
            }
        }

        protected sealed override bool CanExecuteOverride(object parameter) {
            T value;
            if (Convert(parameter, out value)) {
                return AlwaysCanExecute || CanExecuteOverride(value);
            }

            Logging.Error($"Invalid type: {parameter?.GetType().Name ?? @"<NULL>"} (required: {typeof(T)})");
            return false;
        }

        public bool CanExecute(T parameter) {
            return AlwaysCanExecute || CanExecuteOverride(parameter);
        }

        public void Execute(T parameter) {
            if (AlwaysCanExecute || CanExecuteOverride(parameter)) {
                ExecuteOverride(parameter);
            }
        }

        protected abstract bool CanExecuteOverride(T parameter);

        protected abstract void ExecuteOverride(T parameter);
    }
}