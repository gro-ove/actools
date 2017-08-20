using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class LinqExtension {
        public static int GetEnumerableHashCode<T>([CanBeNull] this IEnumerable<T> items) {
            return items?.Aggregate(0, (x, o) => (x * 397) ^ o.GetHashCode()) ?? 0;
        }

        public static IEnumerable<T[]> Partition<T>([NotNull] this IEnumerable<T> items, int partitionSize) {
            if (items == null) throw new ArgumentNullException(nameof(items));

            var buffer = new T[partitionSize];
            var n = 0;
            foreach (var item in items) {
                buffer[n] = item;
                n += 1;
                if (n != partitionSize) continue;

                yield return buffer;
                buffer = new T[partitionSize];
                n = 0;
            }

            if (n > 0) yield return buffer;
        }

        public static int Median(this IEnumerable<int> source, IComparer<int> comparer = null) => source.MedianInner(comparer, (x, y) => (x + y) / 2);
        public static float Median(this IEnumerable<float> source, IComparer<float> comparer = null) => source.MedianInner(comparer, (x, y) => (x + y) / 2);
        public static double Median(this IEnumerable<double> source, IComparer<double> comparer = null) => source.MedianInner(comparer, (x, y) => (x + y) / 2);
        public static int? Median(this IEnumerable<int?> source, IComparer<int?> comparer = null) => source.MedianInner(comparer, (x, y) => (x + y) / 2);
        public static float? Median(this IEnumerable<float?> source, IComparer<float?> comparer = null) => source.MedianInner(comparer, (x, y) => (x + y) / 2);
        public static double? Median(this IEnumerable<double?> source, IComparer<double?> comparer = null) => source.MedianInner(comparer, (x, y) => (x + y) / 2);

        public static TimeSpan Median(this IEnumerable<TimeSpan> source, IComparer<TimeSpan> comparer = null)
                => source.MedianInner(comparer, (x, y) => TimeSpan.FromSeconds((x.TotalSeconds + y.TotalSeconds) / 2d));

        public static TimeSpan? Median(this IEnumerable<TimeSpan?> source, IComparer<TimeSpan?> comparer = null)
                => source.MedianInner(comparer, (x, y) => TimeSpan.FromSeconds(((x?.TotalSeconds ?? 0d) + (y?.TotalSeconds ?? 0d)) / 2d));

        private static T MedianInner<T>(this IEnumerable<T> source, IComparer<T> comparer, Func<T, T, T> average) {
            if (comparer == null) comparer = Comparer<T>.Default;

            var temp = source.ToArray();
            Array.Sort(temp, comparer);

            var count = temp.Length;
            if (count == 0) {
                throw new InvalidOperationException("Empty collection");
            }

            if (count % 2 == 0 && average != null) {
                var a = temp[count / 2 - 1];
                var b = temp[count / 2];
                return average(a, b);
            }

            return temp[count / 2];
        }

        public static int IndexOf<T>([NotNull] this IEnumerable<T> source, T value) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var index = 0;
            foreach (var item in source) {
                if (Equals(item, value)) return index;
                index++;
            }
            return -1;
        }

        [NotNull]
        public static T ElementAtOr<T>([NotNull] this IEnumerable<T> source, int index, [NotNull] T defaultValue) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            foreach (var item in source) {
                if (--index < 0) return item;
            }

            return defaultValue;
        }

        public static void DragAndDrop<T>(this IList<T> target, int index, T obj, IList<T> source = null) {
            source?.Remove(obj);

            var oldIndex = target.IndexOf(obj);
            if (oldIndex != -1) {
                if (index == oldIndex) return;

                target.RemoveAt(oldIndex);
                /*if (oldIndex < index) {
                    index--;
                }*/
            }

            if (index == -1) {
                target.Add(obj);
            } else {
                target.Insert(index, obj);
            }
        }

        [CanBeNull]
        public static T MaxOr<T>([NotNull] this IEnumerable<T> source, T defaultValue) where T : IComparable<T> {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = defaultValue;
            var first = true;

            foreach (var item in source) {
                var value = item;
                if (first) {
                    result = item;
                    first = false;
                } else if (value.CompareTo(result) > 0) {
                    result = item;
                }
            }

            return result;
        }

        [CanBeNull]
        public static T MaxOrDefault<T>([NotNull] this IEnumerable<T> source) where T : IComparable<T> {
            return MaxOr(source, default(T));
        }

        [CanBeNull]
        public static T MinOr<T>([NotNull] this IEnumerable<T> source, T defaultValue) where T : IComparable<T> {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = defaultValue;
            var first = true;

            foreach (var item in source) {
                var value = item;
                if (first) {
                    result = item;
                    first = false;
                } else if (value.CompareTo(result) < 0) {
                    result = item;
                }
            }

            return result;
        }

        [CanBeNull]
        public static T MinOrDefault<T>([NotNull] this IEnumerable<T> source) where T : IComparable<T> {
            return MinOr(source, default(T));
        }

        public static T MaxEntry<T, TResult>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, TResult> selector) where TResult : IComparable<TResult> {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            using (var en = source.GetEnumerator()) {
                if (!en.MoveNext()) {
                    throw new InvalidOperationException("Sequence contains no elements");
                }

                var result = en.Current;
                var maxValue = selector(result);

                while (en.MoveNext()) {
                    var item = en.Current;
                    var value = selector(item);
                    if (value.CompareTo(maxValue) > 0) {
                        result = item;
                        maxValue = value;
                    }
                }

                return result;
            }
        }

        [CanBeNull]
        public static T MaxEntryOrDefault<T, TResult>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, TResult> selector)
                where TResult : IComparable<TResult> {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            var result = default(T);
            var maxValue = default(TResult);
            var first = true;

            foreach (var item in source) {
                var value = selector(item);
                if (first) {
                    result = item;
                    maxValue = value;
                    first = false;
                } else if (value.CompareTo(maxValue) > 0) {
                    result = item;
                    maxValue = value;
                }
            }

            return result;
        }

        [CanBeNull]
        public static T MaxEntryOrDefault<T, TResult>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, TResult> selector, IComparer<TResult> comparer) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            var result = default(T);
            var maxValue = default(TResult);
            var first = true;

            foreach (var item in source) {
                var value = selector(item);
                if (first) {
                    result = item;
                    maxValue = value;
                    first = false;
                } else if (comparer.Compare(value, maxValue) > 0) {
                    result = item;
                    maxValue = value;
                }
            }

            return result;
        }

        public static T MinEntry<T, TResult>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, TResult> selector) where TResult : IComparable<TResult> {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            using (var en = source.GetEnumerator()) {
                if (!en.MoveNext()) {
                    throw new InvalidOperationException("Sequence contains no elements");
                }

                var result = en.Current;
                var minValue = selector(result);

                while (en.MoveNext()) {
                    var item = en.Current;
                    var value = selector(item);
                    if (value.CompareTo(minValue) < 0) {
                        result = item;
                        minValue = value;
                    }
                }

                return result;
            }
        }

        [CanBeNull]
        public static T MinEntryOrDefault<T, TResult>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, TResult> selector)
                where TResult : IComparable<TResult> {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            var result = default(T);
            var minValue = default(TResult);
            var first = true;

            foreach (var item in source) {
                var value = selector(item);
                if (first) {
                    result = item;
                    minValue = value;
                    first = false;
                } else if (value.CompareTo(minValue) < 0) {
                    result = item;
                    minValue = value;
                }
            }

            return result;
        }

        public static int FindIndex<T>([NotNull] this IEnumerable<T> items, [NotNull] Func<T, bool> predicate) {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            var retVal = 0;
            foreach (var item in items) {
                if (predicate(item)) return retVal;
                retVal++;
            }

            return -1;
        }

        [Pure, NotNull]
        public static IEnumerable<IEnumerable<T>> Chunk<T>([NotNull] this IEnumerable<T> source, int chunksize) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var list = source as IList<T> ?? source.ToList();

            var pos = 0;
            while (list.Skip(pos).Any()) {
                yield return list.Skip(pos).Take(chunksize);
                pos += chunksize;
            }
        }

        [Pure]
        public static TimeSpan Sum([NotNull] this IEnumerable<TimeSpan> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            return enumerable.Aggregate(TimeSpan.Zero, (current, timeSpan) => current + timeSpan);
        }

        [Pure, NotNull]
        public static IEnumerable<T> Repeat<T>([NotNull] this IList<T> enumerable, int repetition) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            for (var i = 0; i < repetition; i++) {
                for (var j = 0; j < enumerable.Count; j++) {
                    yield return enumerable[j];
                }
            }
        }

        [Pure, NotNull]
        public static IEnumerable<T> Repeat<T>([NotNull] this IEnumerable<T> enumerable, int repetition) {
            return repetition == 1 ? enumerable : enumerable.ToList().Repeat(repetition);
        }

        [Pure]
        public static T RandomElement<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            var list = enumerable.ToIReadOnlyListIfItIsNot();
            var random = MathUtils.Random(0, list.Count);
            return list[random];
        }

        [Pure, CanBeNull]
        public static T RandomElementOrDefault<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            var list = enumerable.ToIReadOnlyListIfItIsNot();
            if (list.Count == 0) return default(T);

            var random = MathUtils.Random(0, list.Count);
            return list[random];
        }

        [Pure, NotNull]
        public static string JoinToString([NotNull] this IEnumerable<uint> enumerable, string s) {
            return JoinToString(enumerable.Select(x => x.ToString(CultureInfo.InvariantCulture)), s);
        }

        [Pure, NotNull]
        public static string JoinToString([NotNull] this IEnumerable<ushort> enumerable, string s) {
            return JoinToString(enumerable.Select(x => x.ToString(CultureInfo.InvariantCulture)), s);
        }

        [Pure, NotNull]
        public static string JoinToString([NotNull] this IEnumerable<float> enumerable, string s) {
            return JoinToString(enumerable.Select(x => x.ToString(CultureInfo.InvariantCulture)), s);
        }

        [Pure, NotNull]
        public static string JoinToString([NotNull] this IEnumerable<double> enumerable, string s) {
            return JoinToString(enumerable.Select(x => x.ToString(CultureInfo.InvariantCulture)), s);
        }

        [Pure, NotNull]
        public static string JoinToString([NotNull] this IEnumerable<int> enumerable, string s) {
            return JoinToString(enumerable.Select(x => x.ToString(CultureInfo.InvariantCulture)), s);
        }

        [Pure, NotNull]
        public static string JoinToString<T>([NotNull] this IEnumerable<T> enumerable, string s) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            var sb = new StringBuilder();
            foreach (var e in enumerable) {
                if (sb.Length > 0) {
                    sb.Append(s);
                }
                sb.Append(e);
            }

            return sb.ToString();
        }

        [Pure, NotNull]
        public static string JoinToString<T>([NotNull] this IEnumerable<T> enumerable, char s) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            var sb = new StringBuilder();
            foreach (var e in enumerable) {
                if (sb.Length > 0) {
                    sb.Append(s);
                }
                sb.Append(e);
            }

            return sb.ToString();
        }

        public static void Pass<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            using (var e = enumerable.GetEnumerator()) {
                while (e.MoveNext()) { }
            }
        }

        [NotNull]
        public static string JoinToString<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            var sb = new StringBuilder();
            foreach (var e in enumerable) {
                sb.Append(e);
            }

            return sb.ToString();
        }

        [NotNull]
        public static List<T> ToListIfItIsNot<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            return enumerable as List<T> ?? enumerable.ToList();
        }

        [NotNull]
        public static IList<T> ToIListIfItIsNot<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            return enumerable as IList<T> ?? enumerable.ToList();
        }

        [NotNull]
        public static IReadOnlyList<T> ToIReadOnlyListIfItIsNot<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            return enumerable as IReadOnlyList<T> ?? enumerable.ToList();
        }

        [NotNull]
        public static T[] ToArrayIfItIsNot<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            return enumerable as T[] ?? enumerable.ToArray();
        }

        /// <summary>
        /// Dispose everything.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        public static void DisposeEverything<T>([NotNull] this IEnumerable<T> source) where T : IDisposable {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source) {
                i?.Dispose();
            }
        }

        /// <summary>
        /// Dispose everything and clear list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        public static void DisposeEverything<T>([NotNull] this ICollection<T> source) where T : IDisposable {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source.Where(x => x != null)) {
                i.Dispose();
            }
            if (!source.IsReadOnly) {
                source.Clear();
            }
        }

        /// <summary>
        /// Dispose everything and clear dictionary.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="source"></param>
        public static void DisposeEverything<TKey, T>([NotNull] this IDictionary<TKey, T> source) where T : IDisposable {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source.Values.Where(x => x != null)) {
                i.Dispose();
            }
            if (!source.IsReadOnly) {
                source.Clear();
            }
        }

        [Pure]
        public static IEnumerable<T> SelectManyRecursive<T>([NotNull] this IEnumerable<T> source, Func<T, IEnumerable<T>> childrenSelector) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            foreach (var i in source) {
                yield return i;

                var children = childrenSelector(i);
                if (children == null) continue;

                foreach (var child in SelectManyRecursive(children, childrenSelector)) {
                    yield return child;
                }
            }
        }

        [Pure]
        public static Dictionary<TKey, TValue> ManyToDictionaryKv<T, TKey, TValue>([NotNull] this IEnumerable<T> source, Func<T, IEnumerable<TKey>> keySelector,
                Func<T, IEnumerable<TValue>> valueSelector) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = new Dictionary<TKey, TValue>();
            foreach (var i in source) {
                foreach (var key in keySelector(i)) {
                    foreach (var value in valueSelector(i)) {
                        result[key] = value;
                    }
                }
            }

            return result;
        }

        [Pure]
        public static Dictionary<TKey, TValue> ManyToDictionaryV<T, TKey, TValue>([NotNull] this IEnumerable<T> source, Func<T, TKey> keySelector,
                Func<T, IEnumerable<TValue>> valueSelector) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = new Dictionary<TKey, TValue>();
            foreach (var i in source) {
                foreach (var value in valueSelector(i)) {
                    result[keySelector(i)] = value;
                }
            }

            return result;
        }

        [Pure]
        public static Dictionary<TKey, TValue> ManyToDictionaryK<T, TKey, TValue>([NotNull] this IEnumerable<T> source, Func<T, IEnumerable<TKey>> keySelector,
                Func<T, TValue> valueSelector) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = new Dictionary<TKey, TValue>();
            foreach (var i in source) {
                foreach (var key in keySelector(i)) {
                    result[key] = valueSelector(i);
                }
            }

            return result;
        }

        [Pure]
        public static Dictionary<TKey, TValue> ToDictionary<T, TKey, TValue>([NotNull] this IEnumerable<T> source, Func<T, int, TKey> keySelector,
                Func<T, int, TValue> valueSelector) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = new Dictionary<TKey, TValue>();
            var index = 0;
            foreach (var item in source) {
                result[keySelector(item, index)] = valueSelector(item, index);
                index++;
            }

            return result;
        }

        [ItemNotNull, Pure]
        public static IEnumerable<T> NonNull<T>([ItemCanBeNull, NotNull] this IEnumerable<T> source) where T : class {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Where(i => i != null);
        }

        [Pure]
        public static IEnumerable<int> RangeFrom(int from = 0) {
            for (var i = from; i < int.MaxValue; i++) {
                yield return i;
            }
        }

        [Pure]
        public static bool IsOrdered<T>([NotNull] this IEnumerable<T> source, [NotNull] IComparer<T> comparer) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));

            var prevEntry = default(T);
            var first = true;
            foreach (var i in source) {
                if (first) {
                    prevEntry = i;
                    first = false;
                } else if (comparer.Compare(prevEntry, i) > 0) {
                    return false;
                }
            }

            return true;
        }

        [Pure]
        public static bool IsOrdered<T>(this IEnumerable<T> source) {
            return source.IsOrdered(Comparer<T>.Default);
        }

        [Pure]
        public static IEnumerable<T> Sort<T>([NotNull] this IEnumerable<T> source) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.OrderBy(x => x, Comparer<T>.Default);
        }

        [Pure]
        public static IEnumerable<T> Sort<T>([NotNull] this IEnumerable<T> source, IComparer<T> comparer) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.OrderBy(x => x, comparer);
        }

        [Pure]
        public static IEnumerable<T> Sort<T>([NotNull] this IEnumerable<T> source, Func<T, T, int> fn) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.OrderBy(x => x, new FuncBasedComparer<T>(fn));
        }

        [Pure]
        public static IEnumerable<T> TakeLast<T>([NotNull] this IEnumerable<T> source, int count) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var list = source.ToList();
            return list.Skip(Math.Max(list.Count - count, 0));
        }

        [Pure]
        public static IEnumerable<T> TakeLast<T>([NotNull] this IList<T> source, int count) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            for (var i = Math.Max(source.Count - count, 0); i < source.Count; i++) {
                yield return source[i];
            }
        }

        [Pure]
        public static IEnumerable<T> SkipLast<T>([NotNull] this IEnumerable<T> source, int count) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var list = source.ToIReadOnlyListIfItIsNot();
            return list.Take(Math.Max(list.Count - count, 0));
        }

        [Pure]
        public static IEnumerable<T> Append<T>([NotNull] this IEnumerable<T> source, params T[] additionalItems) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Concat(additionalItems);
        }

        [Pure]
        public static IEnumerable<T> Prepend<T>([NotNull] this IEnumerable<T> source, params T[] additionalItems) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return additionalItems.Concat(source);
        }

        [Pure]
        public static IEnumerable<T> ApartFrom<T>([NotNull] this IEnumerable<T> source, object item) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Where(x => !ReferenceEquals(x, item));
        }

        [Pure]
        public static IEnumerable<T> ApartFrom<T>([NotNull] this IEnumerable<T> source, T item) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Where(x => !Equals(x, item));
        }

        [Pure]
        public static IEnumerable<T> ApartFrom<T>([NotNull] this IEnumerable<T> source, [CanBeNull] params T[] additionalItems) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (additionalItems == null) return source;
            return source.Where(x => !additionalItems.Contains(x));
        }

        [Pure]
        public static IEnumerable<T> If<T>([NotNull] this IEnumerable<T> source, [NotNull] Func<IEnumerable<T>, IEnumerable<T>> fn) {
            return fn(source);
        }

        [Pure]
        public static IEnumerable<T> If<T>([NotNull] this IEnumerable<T> source, bool condition, [NotNull] Func<IEnumerable<T>, IEnumerable<T>> fn) {
            return condition ? fn(source) : source;
        }

        [Pure]
        public static IEnumerable<T> IfWhere<T>([NotNull] this IEnumerable<T> source, bool condition, [NotNull] Func<T, bool> fn) {
            return condition ? source.Where(fn) : source;
        }


        [Pure]
        public static IEnumerable<T> IfSelect<T>([NotNull] this IEnumerable<T> source, bool condition, [NotNull] Func<T, T> fn) {
            return condition ? source.Select(fn) : source;
        }

        [Pure]
        public static IEnumerable<T> ApartFrom<T>([NotNull] this IEnumerable<T> source, [CanBeNull] IEnumerable<T> additionalItems) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (additionalItems == null) return source;
            var list = additionalItems.ToIReadOnlyListIfItIsNot();
            return source.Where(x => !list.Contains(x));
        }

        [Pure]
        public static int Count<T>([NotNull] this IEnumerable<T> source, T item) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Count(x => Equals(x, item));
        }

        [Pure]
        public static int Count<T>([NotNull] this IEnumerable<T> source, T[] additionalItems) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Count(additionalItems.Contains);
        }

        [Pure]
        public static TValue Aggregate<T, TValue>([NotNull] this IEnumerable<T> source, TValue seed, Func<TValue, T, int, TValue> func) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var j = 0;
            return source.Aggregate(seed, (c, i) => func(c, i, j++));
        }

        private class FuncBasedComparer<T> : IComparer<T> {
            private readonly Func<T, T, int> _fn;

            public FuncBasedComparer(Func<T, T, int> fn) {
                _fn = fn;
            }

            public int Compare(T x, T y) => _fn(x, y);
        }

        [Pure]
        public static bool AreIdentical<T>([NotNull] this IEnumerable<T> source) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            using (var enumerator = source.GetEnumerator()) {
                if (!enumerator.MoveNext()) return true;
                var first = enumerator.Current;
                while (enumerator.MoveNext()) {
                    if (!Equals(enumerator.Current, first)) return false;
                }

                return true;
            }
        }

        [Pure, NotNull]
        public static T GetById<T>([NotNull] this IEnumerable<T> source, string id) where T : IWithId {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source) {
                if (Equals(i.Id, id)) return i;
            }
            throw new Exception("Element with given ID not found");
        }

        [Pure, CanBeNull]
        public static T GetByIdOrDefault<T>([NotNull] this IEnumerable<T> source, [Localizable(false)] string id) where T : IWithId {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.FirstOrDefault(x => x.Id == id);
        }

        [Pure, CanBeNull]
        public static T GetByIdOrDefault<T>([NotNull] this IEnumerable<T> source, string id, StringComparison comparison) where T : IWithId {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.FirstOrDefault(x => string.Equals(x.Id, id, comparison));
        }

        [Pure, NotNull]
        public static T GetById<T, TId>([NotNull] this IEnumerable<T> source, TId id) where T : IWithId<TId> {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.First(x => Equals(x.Id, id));
        }

        [Pure, CanBeNull]
        public static T GetByIdOrDefault<T, TId>([NotNull] this IEnumerable<T> source, TId id) where T : IWithId<TId> {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source) {
                if (Equals(i.Id, id)) return i;
            }
            return default(T);
        }

        [Pure]
        public static bool All<T>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, int, bool> predicate) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var j = 0;
            return source.All(i => predicate(i, j++));
        }

        [Pure]
        public static bool Any<T>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, int, bool> predicate) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            var j = 0;
            return source.Any(i => predicate(i, j++));
        }

        public static IEnumerable<T> CollectRest<T>([NotNull] this IEnumerator<T> source) {
            while (source.MoveNext()) {
                yield return source.Current;
            }
        }

        public static void ForEach<T>([NotNull] this IEnumerable<T> source, Action<T> action) {
            using (var enumerator = source.GetEnumerator()) {
                while (enumerator.MoveNext()) {
                    action(enumerator.Current);
                }
            }
        }

        public static void ForEach<TFirst, TSecond>([NotNull] this IEnumerable<TFirst> source, IEnumerable<TSecond> second, Action<TFirst, TSecond> action) {
            using (var enumeratorFirst = source.GetEnumerator())
            using (var enumeratorSecond = second.GetEnumerator()) {
                while (enumeratorFirst.MoveNext() && enumeratorSecond.MoveNext()) {
                    action(enumeratorFirst.Current, enumeratorSecond.Current);
                }
            }
        }

        public static int AddSorted<T>([NotNull] this IList<T> list, T value, IComparer<T> comparer = null) {
            if (comparer == null) comparer = Comparer<T>.Default;

            var end = list.Count - 1;

            // Array is empty or new item should go in the end
            if (end == -1 || comparer.Compare(value, list[end]) > 0) {
                list.Add(value);
                return end + 1;
            }

            // Simplest version for small arrays
            if (end < 20) {
                for (end--; end >= 0; end--) {
                    if (comparer.Compare(value, list[end]) >= 0) {
                        list.Insert(end + 1, value);
                        return end + 1;
                    }
                }

                list.Insert(0, value);
                return list.Count - 1;
            }

            // Sort of binary search
            var start = 0;
            while (true) {
                if (end == start) {
                    list.Insert(start, value);
                    return start;
                }

                if (end == start + 1) {
                    if (comparer.Compare(value, list[start]) <= 0) {
                        list.Insert(start, value);
                        return start;
                    }

                    list.Insert(end, value);
                    return end;
                }

                var m = start + (end - start) / 2;

                var c = comparer.Compare(value, list[m]);
                if (c == 0) {
                    list.Insert(m, value);
                    return m;
                }

                if (c < 0) {
                    end = m;
                } else {
                    start = m + 1;
                }
            }
        }
    }

    public interface IWithId<out T> {
        T Id { get; }
    }

    public interface IWithId : IWithId<string> {}
}
