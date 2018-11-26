using System;
using System.Globalization;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Commands {
    public abstract class CommandExt<T> : CommandBase {
        public bool XamlCompatible { get; set; } = true;

        protected CommandExt(bool alwaysCanExecute, bool isAutomaticRequeryDisabled) : base(alwaysCanExecute, isAutomaticRequeryDisabled) {}

        protected bool ConvertXamlCompatible([NotNull] object parameter, out T result) {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));

            if (typeof(T) == typeof(string)) {
                /* we don’t need to check on null or if it’s already a string — those
                 * cases would be solved before, by Convert() */
                result = (T)(object)parameter.ToString();
                return true;
            }

            if (parameter is T valueT) {
                result = valueT;
                return true;
            }

            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            var asString = parameter as string ?? parameter.ToString();
            if (type == typeof(bool)) {
                if (bool.TryParse(asString, out var value)) {
                    result = (T)(object)value;
                    return true;
                }
            } else if (typeof(T) == typeof(int)) {
                if (int.TryParse(asString, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) {
                    result = (T)(object)value;
                    return true;
                }
            } else if(typeof(T) == typeof(double)) {
                if (double.TryParse(asString, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) {
                    result = (T)(object)value;
                    return true;
                }
            } else if (typeof(T) == typeof(long)) {
                if (long.TryParse(asString, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) {
                    result = (T)(object)value;
                    return true;
                }
            } else if(typeof(T) == typeof(float)) {
                if (float.TryParse(asString, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) {
                    result = (T)(object)value;
                    return true;
                }
            }else if(typeof(T) == typeof(Uri)) {
                result = (T)(object)new Uri(asString, UriKind.RelativeOrAbsolute);
                return true;
            }

            result = default;
            return false;
        }

        private bool Convert(object value, out T result) {
            if (value is T variable) {
                result = variable;
                return true;
            }

            if (value == null) {
                /* nulls are appropriate only for non-value or nullable types */
                if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null) {
                    result = default;
                    return false;
                }

                result = (T)(object)null;
                return true;
            }

            if (XamlCompatible && ConvertXamlCompatible(value, out result)) {
                return true;
            }

            result = default;
            return false;
        }

        protected sealed override void ExecuteOverride(object parameter) {
            if (Convert(parameter, out var value)) {
                ExecuteOverride(value);
            } else {
                Logging.Error($"Invalid type: {parameter?.GetType().Name ?? @"<NULL>"} (required: {typeof(T)})");
            }
        }

        protected sealed override bool CanExecuteOverride(object parameter) {
            if (Convert(parameter, out var value)) {
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
}