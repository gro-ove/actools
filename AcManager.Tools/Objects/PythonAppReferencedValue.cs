using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppReferencedValue<T> : NotifyPropertyChanged where T : struct {
        [CanBeNull]
        private readonly string _referenceKey;

        public PythonAppReferencedValue(T value) {
            Value = value;
        }

        public PythonAppReferencedValue([NotNull] string referenceKey) {
            _referenceKey = referenceKey;
        }

        public T Value { get; private set; }

        public bool Refresh([NotNull] IPythonAppConfigValueProvider provider) {
            if (_referenceKey == null) return false;

            var value = provider.GetValue(_referenceKey).As(default(T));
            if (Equals(value, Value)) return false;

            Value = value;
            OnPropertyChanged(nameof(Value));
            return true;
        }

        public static PythonAppReferencedValue<T> Parse(string value) {
            if (value.Length > 1 && (value[0] == '\'' || value[0] == '"' || value[0] == '`'
                    || value[0] == '“' || value[0] == '”' || value[0] == '_' || char.IsLetter(value[0]))) {
                return new PythonAppReferencedValue<T>(value.Trim(' ', '\t', '\n', '\r', '"', '\'', '`', '“', '”'));
            }
            return new PythonAppReferencedValue<T>(value.As<T>());
        }
    }
}