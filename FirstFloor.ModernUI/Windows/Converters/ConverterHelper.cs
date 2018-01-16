using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Converters {
    public static class ConverterHelper {
        public static bool XamlEquals([CanBeNull] this object properValue, [CanBeNull] object xamlValue) {
            if (properValue == null) return xamlValue == null;
            if (xamlValue == null) return false;

            switch (properValue) {
                case double d:
                    return Equals(d, xamlValue.As<double>());
                case int i:
                    return Equals(i, xamlValue.As<int>());
                case bool b:
                    return Equals(b, xamlValue.As<bool>());
                case string s:
                    return Equals(s, xamlValue.ToString());
            }

            return Equals(properValue, xamlValue);
        }
    }
}