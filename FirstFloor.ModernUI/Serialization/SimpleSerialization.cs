using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Serialization {
    [Localizable(false)]
    public static partial class SimpleSerialization {
        static SimpleSerialization() {
            Register(s => TimeSpan.Parse(s, CultureInfo.InvariantCulture));
            Register(s => new Uri(s, UriKind.RelativeOrAbsolute));
            Register(TimeZoneInfo.FromSerializedString, t => t.ToSerializedString());
            Register(Convert.FromBase64String, Convert.ToBase64String);
            RegisterWpfTypes();
            RegisterDrawingTypes();
        }

        #region Methods for adding support for extra types
        /// <summary>
        /// Register new type for auto-conversion. Feel free to throw any exceptions within provided functions.
        /// </summary>
        /// <param name="deserialize">Deserialize function.</param>
        /// <param name="serialize">Serialize function.</param>
        /// <typeparam name="T">Registered type.</typeparam>
        public static void Register<T>([NotNull] Func<string, T> deserialize, [NotNull] Func<T, string> serialize) {
            Registered[typeof(T)] = new Proc<T>(deserialize, serialize);
        }

        /// <summary>
        /// Register new type for auto-conversion. Feel free to throw any exceptions within provided functions. Will be serialized
        /// using regular ToString() call.
        /// </summary>
        /// <param name="deserialize">Deserialize function.</param>
        /// <typeparam name="T">Registered type.</typeparam>
        public static void Register<T>([NotNull] Func<string, T> deserialize) {
            Registered[typeof(T)] = new Proc<T>(deserialize);
        }
        #endregion

        #region Main magical method
        /// <summary>
        /// Convert anything to anything. Mostly, strings to primitive types, or primitive types to strings.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="targetNullValue">Return value by default, in case conversion failed or original value is null.</param>
        /// <typeparam name="T">Type of the result.</typeparam>
        /// <returns>Converted value.</returns>
        [ContractAnnotation("targetNullValue:null, value:null => canbenull; targetNullValue:notnull => notnull")]
        public static T As<T>([CanBeNull] this object value, T targetNullValue = default(T)) {
            switch (value) {
                case T variable:
                    return variable;
                case null:
                    return targetNullValue;
                default:
                    if (typeof(T) == typeof(string)) {
                        var s = ToString(value);
                        return s == null ? targetNullValue : (T)(object)s;
                    } else {
                        return (T)FromString(typeof(T), value, targetNullValue);
                    }
            }
        }

        /// <summary>
        /// Convert anything to anything. Mostly, strings to primitive types, or primitive types to strings. Alternative version,
        /// for compatibility with Storage storing booleans as “0” or “1”.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="targetNullValue">Return value by default, in case conversion failed or original value is null.</param>
        /// <typeparam name="T">Type of the result.</typeparam>
        /// <returns>Converted value.</returns>
        [ContractAnnotation("targetNullValue:null => canbenull; targetNullValue:notnull => notnull")]
        public static T AsShort<T>(this object value, T targetNullValue = default(T)) {
            if (typeof(T) == typeof(string)) {
                if (value == null) return targetNullValue;

                var t = value.GetType();
                if ((Nullable.GetUnderlyingType(t) ?? t) == typeof(bool)) {
                    return (T)(object)((bool)value ? "1" : "0");
                }
            }

            return value.As<T>();
        }
        #endregion

        #region Non-generic version
        private static readonly ConcurrentDictionary<Type, object> TypeDefaults = new ConcurrentDictionary<Type, object>();

        public static object GetDefaultValue(this Type type) {
            return type.IsValueType ? TypeDefaults.GetOrAdd(type, Activator.CreateInstance) : null;
        }

        /// <summary>
        /// Convert anything to anything. Mostly, strings to primitive types, or primitive types to strings.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="type">Target type.</param>
        /// <param name="targetNullValue">Return value by default, in case conversion failed or original value is null.</param>
        /// <returns>Converted value.</returns>
        [ContractAnnotation("targetNullValue:null => canbenull; targetNullValue:notnull => notnull")]
        public static object As([CanBeNull] this object value, [NotNull] Type type, object targetNullValue) {
            if (value == null) {
                return targetNullValue;
            }

            if (value.GetType() == type) {
                return value;
            }

            if (type == typeof(string)) {
                return ToString(value) ?? targetNullValue;
            }

            return FromString(type, value, targetNullValue);
        }

        /// <summary>
        /// Convert anything to anything. Mostly, strings to primitive types, or primitive types to strings.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="type">Target type.</param>
        /// <returns>Converted value.</returns>
        [CanBeNull]
        public static object As([CanBeNull] this object value, [NotNull] Type type) {
            return value.As(type, type.GetDefaultValue());
        }

        public static bool IsSupported(Type type) {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return t.IsEnum || Type.GetTypeCode(t) != TypeCode.Object || Registered.ContainsKey(t);
        }

        public static bool IsSupported<T>() {
            return IsSupported(typeof(T));
        }
        #endregion

        #region Registered types and custom conversions
        private static readonly Dictionary<Type, ProcBase> Registered = new Dictionary<Type, ProcBase>();

        private abstract class ProcBase {
            public abstract object Deserialize([NotNull] string serialized);
            public abstract string Serialize([NotNull] object value);
        }

        private class Proc<T> : ProcBase {
            [NotNull]
            private readonly Func<string, T> _deserialize;

            [NotNull]
            private readonly Func<T, string> _serialize;

            public Proc([NotNull] Func<string, T> deserialize, [NotNull] Func<T, string> serialize) {
                _deserialize = deserialize;
                _serialize = serialize;
            }

            public Proc([NotNull] Func<string, T> deserialize) {
                _deserialize = deserialize;
                _serialize = x => x.ToString();
            }

            public override object Deserialize(string serialized) {
                return _deserialize.Invoke(serialized);
            }

            public override string Serialize(object value) {
                return _serialize.Invoke((T)value);
            }
        }
        #endregion

        #region Some internal params
        private const NumberStyles Ns = NumberStyles.Any;
        private static readonly CultureInfo Cu = CultureInfo.InvariantCulture;
        #endregion

        #region Conversions according to type
        [ContractAnnotation("targetNullValue:null => canbenull; targetNullValue:notnull => notnull")]
        private static object FromString(Type targetType, object v, object targetNullValue) {
            var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var s = v.ToString();
            if (t.IsEnum) {
                try {
                    return Enum.Parse(t, s);
                } catch {
                    Logging.Error($"Invalid {t} value: {s}");
                    return targetNullValue;
                }
            }

            switch (Type.GetTypeCode(t)) {
                case TypeCode.Empty:
                    return null;
                case TypeCode.DBNull:
                    return DBNull.Value;
                case TypeCode.Boolean:
                    var byteString = s;
                    return byteString == "1" || string.Equals(byteString, "true", StringComparison.OrdinalIgnoreCase) ? true
                            : byteString == "0" || string.Equals(byteString, "false", StringComparison.OrdinalIgnoreCase) ? false : targetNullValue;
                case TypeCode.Char:
                    return char.TryParse(s, out var char_) ? char_ : targetNullValue;
                case TypeCode.SByte:
                    return sbyte.TryParse(s, Ns, Cu, out var sbyte_) ? sbyte_ : targetNullValue;
                case TypeCode.Byte:
                    return byte.TryParse(s, Ns, Cu, out var byte_) ? byte_ : targetNullValue;
                case TypeCode.Int16:
                    return short.TryParse(s, Ns, Cu, out var short_) ? short_ : targetNullValue;
                case TypeCode.UInt16:
                    return ushort.TryParse(s, Ns, Cu, out var ushort_) ? ushort_ : targetNullValue;
                case TypeCode.Int32:
                    return int.TryParse(s, Ns, Cu, out var int_) ? int_ : targetNullValue;
                case TypeCode.UInt32:
                    return uint.TryParse(s, Ns, Cu, out var uint_) ? uint_ : targetNullValue;
                case TypeCode.Int64:
                    return long.TryParse(s, Ns, Cu, out var long_) ? long_ : targetNullValue;
                case TypeCode.UInt64:
                    return ulong.TryParse(s, Ns, Cu, out var ulong_) ? ulong_ : targetNullValue;
                case TypeCode.Single:
                    return float.TryParse(s, Ns, Cu, out var float_) ? float_ : targetNullValue;
                case TypeCode.Double:
                    return double.TryParse(s, Ns, Cu, out var double_) ? double_ : targetNullValue;
                case TypeCode.Decimal:
                    return decimal.TryParse(s, Ns, Cu, out var decimal_) ? decimal_ : targetNullValue;
                case TypeCode.DateTime:
                    return DateTime.TryParse(s, Cu, DateTimeStyles.AssumeLocal, out var dateTime) ? dateTime : targetNullValue;
                case TypeCode.String:
                    return s;
                case TypeCode.Object:
                    if (!Registered.TryGetValue(t, out var p)) {
                        Logging.Error($"Not supported type: {t}");
                    } else {
                        try {
                            return p.Deserialize(s) ?? targetNullValue;
                        } catch (Exception e) {
                            Logging.Error(t.FullName);
                            Logging.Error(e);
                        }
                    }
                    return targetNullValue;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [CanBeNull]
        private static string ToString(object v) {
            if (v == null) return null;

            var t = Nullable.GetUnderlyingType(v.GetType()) ?? v.GetType();
            if (t.IsEnum) return v.ToString();

            switch (Type.GetTypeCode(t)) {
                case TypeCode.Empty:
                case TypeCode.DBNull:
                    return null;
                case TypeCode.Boolean:
                    return ((bool)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Char:
                    return ((char)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.SByte:
                    return ((sbyte)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Byte:
                    return ((byte)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Int16:
                    return ((short)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.UInt16:
                    return ((ushort)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Int32:
                    return ((int)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.UInt32:
                    return ((uint)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Int64:
                    return ((long)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.UInt64:
                    return ((ulong)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Single:
                    return ((float)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Double:
                    return ((double)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return ((decimal)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.DateTime:
                    return ((DateTime)v).ToString(CultureInfo.InvariantCulture);
                case TypeCode.String:
                    return (string)v;
                case TypeCode.Object:
                    if (!Registered.TryGetValue(t, out var p)) {
                        Logging.Error($"Not supported type: {t}");
                    } else {
                        try {
                            return p.Serialize(v);
                        } catch (Exception e) {
                            Logging.Error(e);
                        }
                    }
                    return null;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion
    }
}