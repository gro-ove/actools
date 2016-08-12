using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public class CustomUriRequest {
        /// <summary>
        /// In lowercase! Values like “something” (without leading slash).
        /// </summary>
        public string Path { get; private set; }

        public NameValueCollection Params { get; private set; }

        /// <summary>
        /// In original case, without leading hash.
        /// </summary>
        public string Hash { get; private set; }

        /// <summary>
        /// Parse request URI.
        /// </summary>
        /// <param name="uri">Something like “acmanager://something?firstKey=value&amp;secondKey=anotherValue”.</param>
        /// <exception cref="Exception">Thrown if string is in invalid format.</exception>
        /// <returns>Parsed request.</returns>
        [NotNull]
        public static CustomUriRequest Parse([NotNull] string uri) {
            if (!uri.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.OrdinalIgnoreCase)) {
                throw new Exception(ToolsStrings.Common_InvalidFormat);
            }

            var s = uri.SubstringExt(CustomUriSchemeHelper.UriScheme.Length);
            var m = Regex.Match(s, @"^/((?:/[\w\.-]+)+)/?([?&][^#]*)?(?:#(.*))?");
            if (!m.Success) {
                throw new Exception(ToolsStrings.Common_InvalidFormat);
            }

            return new CustomUriRequest {
                Path = m.Groups[1].Value.Substring(1),
                Params = HttpUtility.ParseQueryString(m.Groups[2].Value),
                Hash = m.Groups[3].Value
            };
        }

        /// <summary>
        /// Parse request URI.
        /// </summary>
        /// <param name="uri">Something like “acmanager://something?firstKey=value&amp;secondKey=anotherValue”.</param>
        /// <returns>Parsed request or null if string can’t be parsed.</returns>
        [CanBeNull]
        public static CustomUriRequest TryParse([CanBeNull] string uri) {
            if (uri?.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.OrdinalIgnoreCase) != true) {
                return null;
            }

            var s = uri.SubstringExt(CustomUriSchemeHelper.UriScheme.Length);
            var m = Regex.Match(s, @"^/((?:/[\w\.-]+)+)/?([?&][^#]*)?(?:#(.*))?");
            return m.Success ? new CustomUriRequest {
                Path = m.Groups[1].Value.Substring(1).ToLowerInvariant(),
                Params = HttpUtility.ParseQueryString(m.Groups[2].Value),
                Hash = m.Groups[3].Value
            } : null;
        }
    }
}