using System;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class EnumExtension {
        public static string GetDescription([NotNull] this Enum value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            if (name == null) return null;

            var field = type.GetField(name);
            if (field == null) return null;

            var attr = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attr?.Description;
        }

        public static T[] GetValues<T>() where T : struct {
            return Enum.GetValues(typeof(T)).OfType<T>().ToArray();
        }
    }
}
