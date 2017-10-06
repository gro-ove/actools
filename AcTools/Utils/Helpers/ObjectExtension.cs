using System;
using System.Globalization;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class ObjectExtension {
        public static T If<T>(this T input, bool condition, Func<T, T> c) {
            return condition ? c(input) : input;
        }

        public static T DoIf<T>(this T input, bool condition, Action<T> c) {
            if (condition) c(input);
            return input;
        }

        public static T If<T>(this T input, Func<bool> condition, Func<T, T> c) {
            return condition() ? c(input) : input;
        }

        public static T If<T>(this T input, Func<bool> condition, Action<T> c) {
            if (condition()) c(input);
            return input;
        }

        public static T If<T>(this T input, Func<T, bool> condition, Func<T, T> c) {
            return condition(input) ? c(input) : input;
        }

        public static T If<T>(this T input, Func<T, bool> condition, Action<T> c) {
            if (condition(input)) c(input);
            return input;
        }

        public static TOutput With<TInput, TOutput>(this TInput input, Func<TInput, TOutput> fn) {
            return fn(input);
        }

        [Pure]
        public static string ToInvariantString(this float o) {
            return o.ToString(CultureInfo.InvariantCulture);
        }

        [Pure]
        public static string ToInvariantString(this double o) {
            return o.ToString(CultureInfo.InvariantCulture);
        }
        [Pure]
        public static string ToInvariantString(this int o) {
            return o.ToString(CultureInfo.InvariantCulture);
        }

        [Pure]
        public static string ToInvariantString(this uint o) {
            return o.ToString(CultureInfo.InvariantCulture);
        }

        [Pure]
        public static string ToInvariantString(this short o) {
            return o.ToString(CultureInfo.InvariantCulture);
        }

        [Pure]
        public static string ToInvariantString(this char o) {
            return o.ToString(CultureInfo.InvariantCulture);
        }

        [Pure]
        public static string ToInvariantString(this ushort o) {
            return o.ToString(CultureInfo.InvariantCulture);
        }

        [Pure]
        public static string ToInvariantString([NotNull] this object o) {
            if (o == null) throw new ArgumentNullException(nameof(o));

            var s = o as string;
            if (s != null) return s;

            if (o is double) return ((double)o).ToInvariantString();
            if (o is float) return ((float)o).ToInvariantString();
            if (o is int) return ((int)o).ToInvariantString();
            if (o is uint) return ((uint)o).ToInvariantString();
            if (o is short) return ((short)o).ToInvariantString();
            if (o is ushort) return ((ushort)o).ToInvariantString();

            return o.ToString();
        }
    }
}