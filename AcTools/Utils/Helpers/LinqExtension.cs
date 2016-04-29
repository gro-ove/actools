using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class LinqExtension {
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

        public static int IndexOf<T>([NotNull] this IEnumerable<T> source, T value) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var index = 0;
            foreach (var item in source) {
                if (Equals(item, value)) return index;
                index++;
            }
            return -1;
        }

        [CanBeNull]
        public static T MaxOrDefault<T>([NotNull] this IEnumerable<T> source) where T : IComparable<T> {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = default(T);
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
        public static T MinOrDefault<T>([NotNull] this IEnumerable<T> source) where T : IComparable<T> {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var result = default(T);
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
        public static T MaxEntryOrDefault<T, TResult>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, TResult> selector) where TResult : IComparable<TResult> {
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
                }
            }

            return result;
        }

        [CanBeNull]
        public static T MinEntryOrDefault<T, TResult>([NotNull] this IEnumerable<T> source, [NotNull] Func<T, TResult> selector) where TResult : IComparable<TResult> {
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

        public static IEnumerable<IEnumerable<T>> Chunk<T>([NotNull] this IEnumerable<T> source, int chunksize) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var list = source as IList<T> ?? source.ToList();

            var pos = 0;
            while (list.Skip(pos).Any()) {
                yield return list.Skip(pos).Take(chunksize);
                pos += chunksize;
            }
        }

        public static TimeSpan Sum([NotNull] this IEnumerable<TimeSpan> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            return enumerable.Aggregate(TimeSpan.Zero, (current, timeSpan) => current + timeSpan);
        }

        public static T RandomElement<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            var list = enumerable as IList<T> ?? enumerable.ToList();
            return list.ElementAt(new Random().Next(0, list.Count));
        }

        [NotNull]
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

        [NotNull]
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

        [NotNull]
        public static string JoinToString<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            var sb = new StringBuilder();
            foreach (var e in enumerable) {
                sb.Append(e);
            }

            return sb.ToString();
        }

        public static List<T> ToListIfItsNot<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            return enumerable as List<T> ?? enumerable.ToList();
        }

        public static IList<T> ToIListIfItsNot<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            return enumerable as IList<T> ?? enumerable.ToList();
        }

        public static IReadOnlyList<T> ToIReadOnlyListIfItsNot<T>([NotNull] this IEnumerable<T> enumerable) {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            return enumerable as IReadOnlyList<T> ?? enumerable.ToList();
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
        public static IEnumerable<T> NonNull<T>([NotNull] this IEnumerable<T> source) where T : class {
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
            var list = source.ToIReadOnlyListIfItsNot();
            return list.Skip(Math.Max(list.Count - count, 0));
        }

        [Pure]
        public static IEnumerable<T> SkipLast<T>([NotNull] this IEnumerable<T> source, int count) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var list = source.ToIReadOnlyListIfItsNot();
            return list.Take(Math.Max(list.Count - count, 0));
        }

        [Pure]
        public static IEnumerable<T> Append<T>([NotNull] this IEnumerable<T> source, params T[] additionalItems) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Union(additionalItems);
        }

        [Pure]
        public static IEnumerable<T> Prepend<T>([NotNull] this IEnumerable<T> source, params T[] additionalItems) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return additionalItems.Union(source);
        }

        [Pure]
        public static IEnumerable<T> ApartFrom<T>([NotNull] this IEnumerable<T> source, params T[] additionalItems) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return additionalItems.Where(x => !additionalItems.Contains(x));
        }

        private class FuncBasedComparer<T> : IComparer<T> {
            private readonly Func<T, T, int> _fn;

            public FuncBasedComparer(Func<T, T, int> fn) {
                _fn = fn;
            }

            public int Compare(T x, T y) => _fn(x, y);
        }

        [NotNull]
        [Pure]
        public static T GetById<T>([NotNull] this IEnumerable<T> source, string id) where T : IWithId {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source) {
                if (Equals(i.Id, id)) return i;
            }
            return default(T);
        }

        [Pure]
        [CanBeNull]
        public static T GetByIdOrDefault<T>([NotNull] this IEnumerable<T> source, string id) where T : IWithId {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.FirstOrDefault(x => x.Id == id);
        }

        [NotNull]
        [Pure]
        public static T GetById<T, TId>([NotNull] this IEnumerable<T> source, TId id) where T : IWithId<TId> {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.First(x => Equals(x.Id, id));
        }

        [Pure]
        [CanBeNull]
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
    }

    public interface IWithId {
        string Id { get; }
    }

    public interface IWithId<out T> {
        T Id { get; }
    }
}
