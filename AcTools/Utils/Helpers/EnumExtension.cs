using System;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
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

        public static T FindClosest<T>(this int value) where T : struct {
            if (Enum.IsDefined(typeof(T), value)){
                return (T)(object)value;
            }

            var minDistance = int.MaxValue;
            var minValue = default(T);
            foreach (var enumValue in Enum.GetValues(typeof(T))){
                var intValue = (int)enumValue;
                if (intValue == value) return (T)enumValue;
                var distance = Math.Abs(intValue - value);
                if (distance < minDistance) {
                    minDistance = distance;
                    minValue = (T)enumValue;
                }
            }

            return minValue;
        }

        public static T[] GetValues<T>() where T : struct {
            return Enum.GetValues(typeof(T)).OfType<T>().ToArray();
        }

        public static T NextValue<T>(this T value) where T : struct {
            return Enum.GetValues(typeof(T)).OfType<T>().SkipWhile(x => !Equals(x, value)).Skip(1).FirstOrDefault();
        }

        public static T NValue<T>(int n) where T : struct {
            return Enum.GetValues(typeof(T)).OfType<T>().Skip(n).FirstOrDefault();
        }

        public static T PreviousValue<T>(this T value) where T : struct {
            T? previous = null;
            foreach (var val in Enum.GetValues(typeof(T)).OfType<T>()) {
                if (Equals(val, value)) {
                    return previous ?? Enum.GetValues(typeof(T)).OfType<T>().Last();
                }

                previous = val;
            }
            return default(T);
        }
    }
}
