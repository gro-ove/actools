using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    [Localizable(false)]
    public static class UriExtension {
        [Pure]
        [NotNull]
        public static Uri FromFilenameSafe(string filename) {
            return new Uri(filename.Replace("%", "%25"));
            // return new Uri(filename);
        }

        [Pure]
        [NotNull]
        public static string Format(string uri, params object[] args) {
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
        [Pure]
        [NotNull]
        public static Uri Create(string uri, params object[] args) {
            return new Uri(Format(uri, args), UriKind.Relative);
        }

        [Pure]
        [NotNull]
        public static Uri AddQueryParam(this Uri uri, Dictionary<string, object> dictionary) {
            if (dictionary == null) return uri;

            var query = string.Join("&", from param in dictionary
                                         where param.Value != null
                                         select param.Key + "=" + Uri.EscapeDataString(param.Value.ToString()));
            var uriAsString = uri.ToString();
            return new Uri(uriAsString + (uriAsString.Contains("?") ? "&" : "?") + query, UriKind.Relative);
        }

        [Pure]
        [NotNull]
        public static Uri AddQueryParam(this Uri uri, string key, object value) {
            if (value == null) return uri;

            var query = key + "=" + Uri.EscapeDataString(value.ToString());
            var uriAsString = uri.ToString();
            return new Uri(uriAsString + (uriAsString.Contains("?") ? "&" : "?") + query, UriKind.Relative);
        }

        [Pure]
        [CanBeNull]
        public static string GetQueryParam(this Uri uri, [Localizable(false)] string key) {
            key = key + "=";
            return (from s in uri.ToString().Split('?', '&')
                    where s.StartsWith(key)
                    select Uri.UnescapeDataString(s.Substring(key.Length))).FirstOrDefault();
        }

        [Pure]
        public static T GetQueryParamEnum<T>(this Uri uri, string key) where T : struct, IConvertible {
            var value = uri.GetQueryParam(key);
            if (value == null) return default(T);
            return (T)Enum.Parse(typeof(T), value);
        }
    }
}
