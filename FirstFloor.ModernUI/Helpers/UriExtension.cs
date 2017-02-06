using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    [Localizable(false)]
    public static class UriExtension {
        [Pure, NotNull]
        public static Uri FromFilenameSafe([NotNull] string filename) {
            return new Uri(filename.Replace("%", "%25"));
            // return new Uri(filename);
        }

        [Pure, NotNull, StringFormatMethod("uri")]
        public static string Format([NotNull] string uri, params object[] args) {
            return string.Format(uri, args.Select(x => {
                if (x == null || x is double || x is float || x is int) return x;
                return Uri.EscapeDataString(x.ToString());
            }).ToArray());
        }

        /// <summary>
        /// Create relative URI
        /// </summary>
        /// <param name="uri">Basic string</param>
        /// <param name="args">Arguments (will be escaped)</param>
        /// <returns>Relative URI</returns>
        [Pure, NotNull, StringFormatMethod("uri")]
        public static Uri Create([NotNull] string uri, params object[] args) {
            return new Uri(Format(uri, args), UriKind.Relative);
        }

        [Pure, NotNull]
        public static Uri AddQueryParam([NotNull] this Uri uri, [CanBeNull] Dictionary<string, object> dictionary) {
            if (dictionary == null) return uri;

            var query = string.Join("&", from param in dictionary
                                         where param.Value != null
                                         select param.Key + "=" + Uri.EscapeDataString(param.Value.ToString()));
            var uriAsString = uri.OriginalString;
            return new Uri(uriAsString + (uriAsString.Contains("?") ? "&" : "?") + query, UriKind.Relative);
        }

        [Pure, NotNull]
        public static Uri AddQueryParam([NotNull] this Uri uri, [NotNull] string key, object value) {
            if (value == null) return uri;

            var query = key + "=" + Uri.EscapeDataString(value.ToString());
            var uriAsString = uri.OriginalString;
            return new Uri(uriAsString + (uriAsString.Contains("?") ? "&" : "?") + query, UriKind.Relative);
        }

        [Pure, CanBeNull]
        public static string GetQueryParam([NotNull] this Uri uri, [NotNull, Localizable(false)] string key) {
            key = key + "=";
            return (from s in uri.ToString().Split('?', '&')
                    where s.StartsWith(key)
                    select Uri.UnescapeDataString(s.Substring(key.Length))).FirstOrDefault();
        }

        [Pure]
        public static bool GetQueryParamBool([NotNull] this Uri uri, [NotNull, Localizable(false)] string key) {
            return string.Equals(uri.GetQueryParam(key), "true", StringComparison.OrdinalIgnoreCase);
        }

        [Pure]
        public static bool SamePath([NotNull] this Uri a, [NotNull] Uri b) {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            return a.ToString().Split('?', '&')[0] == b.ToString().Split('?', '&')[0];
        }

        [Pure]
        public static T GetQueryParamEnum<T>([NotNull] this Uri uri, string key) where T : struct, IConvertible {
            var value = uri.GetQueryParam(key);
            if (value == null) return default(T);
            return (T)Enum.Parse(typeof(T), value);
        }
    }
}
