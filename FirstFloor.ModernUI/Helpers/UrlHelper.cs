using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class UrlHelper {
        [Localizable(false), ContractAnnotation(@"s: null => null; s: notnull => notnull")]
        public static string AddQueryParameter(this string s, string key, string value = null) {
            return s == null ? null : $@"{s}{(s.IndexOf('?') == -1 ? "?" : "&")}{key}{(string.IsNullOrWhiteSpace(value) ? "" : Uri.EscapeDataString(value))}";
        }

        [ContractAnnotation(@"s: null => null; s: notnull => notnull")]
        public static string GetWebsiteFromUrl(this string s) {
            return s == null ? null : Regex.Replace(s, @"(?<=\w)/.*$", "", RegexOptions.IgnoreCase);
        }

        [ContractAnnotation(@"s: null => null; s: notnull => notnull")]
        public static string GetDomainNameFromUrl(this string s) {
            return s == null ? null : Regex.Replace(s, @"^(?:(?:https?)?://)?(?:www\.)?|(?<=\w)/.*$", "", RegexOptions.IgnoreCase);
        }

        [ContractAnnotation(@"s: null => false")]
        public static bool IsAnyUrl(this string s) {
            if (s == null) return false;
            var i = s.IndexOf(@"://", StringComparison.Ordinal);
            if (i == -1) return false;
            for (; i > 0; i--) {
                if (!char.IsLetterOrDigit(s[i - 1])) return false;
            }
            return true;
        }

        [ContractAnnotation(@"s: null => false")]
        public static bool IsWebUrl([CanBeNull] this string s) {
            return s != null && (s.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase) ||
                    s.StartsWith(@"https://", StringComparison.OrdinalIgnoreCase));
        }

        [ContractAnnotation(@"s: null => null; s: notnull => notnull")]
        public static string Urlify([CanBeNull] this string s) {
            if (s == null) return null;
            return s.StartsWith(@"/") || s.IsAnyUrl() ? s : s.IndexOf('@') != -1 ? @"mailto:" + s : @"http://" + s;
        }

        public static IEnumerable<string> GetUrls([CanBeNull] this string s) {
            if (s == null) yield break;
            for (var i = 0; i < s.Length; i++) {
                if (IsWebUrl(s, i, false, out var l)) {
                    yield return s.Substring(i, l).Urlify();
                }
            }
        }

        private static bool IsDomainZone(string s, int index, int length) {
            /* JS-code to generate this piece:

var red = `ac
ad
…
zw`.split('\n').map(x => x.trim()).reduce((a, b) => { (a[b[0]] || (a[b[0]] = [])).push(b.substr(1)); return a; }, {})

function toRange(g){
  g = g.map(x => x.charCodeAt(0));
  var a = g.reduce((a, b) => { if (a.indexOf(+b) == -1) a.push(+b); return a; }, []).sort((a, b) => a - b);
  var s = [], p = -1, r = 0;
  for (var i = 0; i <= a.length; i++){
    if (a[i] == p + 1){
      if (!r){ r = p; s.pop(); }
    } else {
      if (r){ s.push(p == r + 1 ? `c == '${String.fromCharCode(r)}' || c == '${String.fromCharCode(p)}'` : `c >= '${String.fromCharCode(r)}' && c <= '${String.fromCharCode(p)}'`); r = 0 }
      if (a[i]) s.push(`c == '${String.fromCharCode(a[i])}'`);
    }
    p = a[i];
  }
  return s.join(' || ');
}

var result = 'if (length == 2){\n\tvar c = s[index + 1];\n\tswitch (s[index]){\n';
for (var n in red){
  var f = red[n].filter(x => x.length == 1);
  if (f.length == 0) continue;
  f.push.apply(f, f.map(x => x.toUpperCase()));
  result += `\t\tcase '${n}':\n\t\tcase '${n.toUpperCase()}':\n\t\t\treturn ${toRange(f)};\n`;
}
result += '\t}\n} else if (length == 3) {\n\tchar c, d;\n\tswitch (s[index]){\n';
for (var n in red){
  var r = red[n].filter(x => x.length == 2);
  if (r.length == 0) continue;
  var rc = r.map(x => `(c == '${x[0]}' || c == '${x[0].toUpperCase()}') && (d == '${x[1]}' || d == '${x[1].toUpperCase()}')`).join(' || ');
  result += `\t\tcase '${n}':\n\t\tcase '${n.toUpperCase()}':\n\t\t\tc = s[index + 1];\n\t\t\td = s[index + 2];\n`;
  result += `\t\t\treturn ${rc};\n`;
}
result += '\t}\n} else {\n\tswitch (s[index]){\n';
for (var n in red){
  var r = red[n].filter(x => x.length > 2);
  if (r.length == 0) continue;
  var rc = r.map(x => `"${x}"`).join(', ');
  result += `\t\tcase '${n}':\n\t\tcase '${n.toUpperCase()}':\n\t\t\treturn Contains(${rc});\n`;
}
result += '\t}\n}\n';
console.log(result);

             */

            if (length == 2) {
                var c = s[index + 1];
                switch (s[index]) {
                    case 'a':
                    case 'A':
                        return c >= 'C' && c <= 'G' || c == 'I' || c >= 'L' && c <= 'U' || c == 'W' || c == 'X' || c == 'Z' || c >= 'c' && c <= 'g' || c == 'i'
                                || c >= 'l' && c <= 'u' || c == 'w' || c == 'x' || c == 'z';
                    case 'b':
                    case 'B':
                        return c == 'A' || c == 'B' || c >= 'D' && c <= 'J' || c >= 'M' && c <= 'O' || c >= 'R' && c <= 'T' || c == 'V' || c == 'W' || c == 'Y'
                                || c == 'Z' || c == 'a' || c == 'b' || c >= 'd' && c <= 'j' || c >= 'm' && c <= 'o' || c >= 'r' && c <= 't' || c == 'v'
                                || c == 'w' || c == 'y' || c == 'z';
                    case 'c':
                    case 'C':
                        return c == 'A' || c == 'C' || c == 'D' || c >= 'F' && c <= 'I' || c >= 'K' && c <= 'O' || c == 'R' || c == 'S' || c == 'U' || c == 'V'
                                || c >= 'X' && c <= 'Z' || c == 'a' || c == 'c' || c == 'd' || c >= 'f' && c <= 'i' || c >= 'k' && c <= 'o' || c == 'r'
                                || c == 's' || c == 'u' || c == 'v' || c >= 'x' && c <= 'z';
                    case 'd':
                    case 'D':
                        return c == 'E' || c == 'J' || c == 'K' || c == 'M' || c == 'O' || c == 'Z' || c == 'e' || c == 'j' || c == 'k' || c == 'm' || c == 'o'
                                || c == 'z';
                    case 'e':
                    case 'E':
                        return c == 'C' || c == 'E' || c == 'G' || c == 'H' || c >= 'R' && c <= 'U' || c == 'c' || c == 'e' || c == 'g' || c == 'h'
                                || c >= 'r' && c <= 'u';
                    case 'f':
                    case 'F':
                        return c >= 'I' && c <= 'K' || c == 'M' || c == 'O' || c == 'R' || c >= 'i' && c <= 'k' || c == 'm' || c == 'o' || c == 'r';
                    case 'g':
                    case 'G':
                        return c == 'A' || c == 'B' || c >= 'D' && c <= 'I' || c >= 'L' && c <= 'N' || c >= 'P' && c <= 'U' || c == 'W' || c == 'Y' || c == 'a'
                                || c == 'b' || c >= 'd' && c <= 'i' || c >= 'l' && c <= 'n' || c >= 'p' && c <= 'u' || c == 'w' || c == 'y';
                    case 'h':
                    case 'H':
                        return c == 'K' || c == 'M' || c == 'N' || c == 'R' || c == 'T' || c == 'U' || c == 'k' || c == 'm' || c == 'n' || c == 'r' || c == 't'
                                || c == 'u';
                    case 'i':
                    case 'I':
                        return c == 'D' || c == 'E' || c >= 'L' && c <= 'O' || c >= 'Q' && c <= 'T' || c == 'd' || c == 'e' || c >= 'l' && c <= 'o'
                                || c >= 'q' && c <= 't';
                    case 'j':
                    case 'J':
                        return c == 'E' || c == 'M' || c == 'O' || c == 'P' || c == 'e' || c == 'm' || c == 'o' || c == 'p';
                    case 'k':
                    case 'K':
                        return c == 'E' || c >= 'G' && c <= 'I' || c == 'M' || c == 'N' || c == 'P' || c == 'R' || c == 'W' || c == 'Y' || c == 'Z' || c == 'e'
                                || c >= 'g' && c <= 'i' || c == 'm' || c == 'n' || c == 'p' || c == 'r' || c == 'w' || c == 'y' || c == 'z';
                    case 'l':
                    case 'L':
                        return c >= 'A' && c <= 'C' || c == 'I' || c == 'K' || c >= 'R' && c <= 'V' || c == 'Y' || c >= 'a' && c <= 'c' || c == 'i' || c == 'k'
                                || c >= 'r' && c <= 'v' || c == 'y';
                    case 'm':
                    case 'M':
                        return c == 'A' || c >= 'C' && c <= 'E' || c == 'G' || c == 'H' || c >= 'K' && c <= 'Z' || c == 'a' || c >= 'c' && c <= 'e' || c == 'g'
                                || c == 'h' || c >= 'k' && c <= 'z';
                    case 'n':
                    case 'N':
                        return c == 'A' || c == 'C' || c >= 'E' && c <= 'G' || c == 'I' || c == 'L' || c == 'O' || c == 'P' || c == 'R' || c == 'U' || c == 'Z'
                                || c == 'a' || c == 'c' || c >= 'e' && c <= 'g' || c == 'i' || c == 'l' || c == 'o' || c == 'p' || c == 'r' || c == 'u'
                                || c == 'z';
                    case 'o':
                    case 'O':
                        return c == 'M' || c == 'm';
                    case 'p':
                    case 'P':
                        return c == 'A' || c >= 'E' && c <= 'H' || c >= 'K' && c <= 'N' || c >= 'R' && c <= 'T' || c == 'W' || c == 'Y' || c == 'a'
                                || c >= 'e' && c <= 'h' || c >= 'k' && c <= 'n' || c >= 'r' && c <= 't' || c == 'w' || c == 'y';
                    case 'q':
                    case 'Q':
                        return c == 'A' || c == 'a';
                    case 'r':
                    case 'R':
                        return c == 'E' || c == 'O' || c == 'U' || c == 'W' || c == 'e' || c == 'o' || c == 'u' || c == 'w';
                    case 's':
                    case 'S':
                        return c >= 'A' && c <= 'E' || c >= 'G' && c <= 'O' || c == 'R' || c == 'T' || c == 'V' || c == 'Y' || c == 'Z' || c >= 'a' && c <= 'e'
                                || c >= 'g' && c <= 'o' || c == 'r' || c == 't' || c == 'v' || c == 'y' || c == 'z';
                    case 't':
                    case 'T':
                        return c == 'C' || c == 'D' || c >= 'F' && c <= 'H' || c >= 'J' && c <= 'P' || c == 'R' || c == 'T' || c == 'V' || c == 'W' || c == 'Z'
                                || c == 'c' || c == 'd' || c >= 'f' && c <= 'h' || c >= 'j' && c <= 'p' || c == 'r' || c == 't' || c == 'v' || c == 'w'
                                || c == 'z';
                    case 'u':
                    case 'U':
                        return c == 'A' || c == 'G' || c == 'K' || c == 'M' || c == 'S' || c == 'Y' || c == 'Z' || c == 'a' || c == 'g' || c == 'k' || c == 'm'
                                || c == 's' || c == 'y' || c == 'z';
                    case 'v':
                    case 'V':
                        return c == 'A' || c == 'C' || c == 'E' || c == 'G' || c == 'I' || c == 'N' || c == 'U' || c == 'a' || c == 'c' || c == 'e' || c == 'g'
                                || c == 'i' || c == 'n' || c == 'u';
                    case 'w':
                    case 'W':
                        return c == 'F' || c == 'S' || c == 'f' || c == 's';
                    case 'y':
                    case 'Y':
                        return c == 'E' || c == 'T' || c == 'U' || c == 'e' || c == 't' || c == 'u';
                    case 'z':
                    case 'Z':
                        return c == 'A' || c == 'M' || c == 'W' || c == 'a' || c == 'm' || c == 'w';
                }
            } else if (length == 3) {
                char c, d;
                switch (s[index]) {
                    case 'b':
                    case 'B':
                        c = s[index + 1];
                        d = s[index + 2];
                        return (c == 'i' || c == 'I') && (d == 'z' || d == 'Z');
                    case 'c':
                    case 'C':
                        c = s[index + 1];
                        d = s[index + 2];
                        return (c == 'a' || c == 'A') && (d == 't' || d == 'T') || (c == 'o' || c == 'O') && (d == 'm' || d == 'M');
                    case 'e':
                    case 'E':
                        c = s[index + 1];
                        d = s[index + 2];
                        return (c == 'd' || c == 'D') && (d == 'u' || d == 'U');
                    case 'g':
                    case 'G':
                        c = s[index + 1];
                        d = s[index + 2];
                        return (c == 'o' || c == 'O') && (d == 'v' || d == 'V');
                    case 'm':
                    case 'M':
                        c = s[index + 1];
                        d = s[index + 2];
                        return (c == 'i' || c == 'I') && (d == 'l' || d == 'L');
                    case 'n':
                    case 'N':
                        c = s[index + 1];
                        d = s[index + 2];
                        return (c == 'e' || c == 'E') && (d == 't' || d == 'T');
                    case 'o':
                    case 'O':
                        c = s[index + 1];
                        d = s[index + 2];
                        return (c == 'r' || c == 'R') && (d == 'g' || d == 'G');
                    case 'p':
                    case 'P':
                        c = s[index + 1];
                        d = s[index + 2];
                        return (c == 'r' || c == 'R') && (d == 'o' || d == 'O');
                }
            } else if (length == 4) {
                switch (s[index]) {
                    case 'a':
                    case 'A':
                        return Contains(@"ero", @"rpa");
                    case 'c':
                    case 'C':
                        return Contains(@"oop", @"lub");
                    case 'i':
                    case 'I':
                        return Contains(@"nfo");
                    case 'j':
                    case 'J':
                        return Contains(@"obs");
                    case 'm':
                    case 'M':
                        return Contains(@"obi");
                    case 'n':
                    case 'N':
                        return Contains(@"ame");
                    case 'w':
                    case 'W':
                        return Contains(@"ork");
                }
            } else if (length == 5) {
                switch (s[index]) {
                    case 's':
                    case 'S':
                        return Contains(@"tore");
                }
            } else if (length == 6) {
                switch (s[index]) {
                    case 'm':
                    case 'M':
                        return Contains(@"useum");
                    case 't':
                    case 'T':
                        return Contains(@"ravel");
                }
            }

            return false;

            bool Contains(params string[] o) {
                var a = s.Substring(index + 1, length - 1);
                for (var i = o.Length - 1; i >= 0; i--) {
                    if (string.Equals(a, o[i], StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
                return false;
            }
        }

        internal static bool IsWebUrl(string s, int index, bool bbCodeMode, out int urlLength) {
            int start = index, length = s.Length;
            if (start >= length) {
                urlLength = 0;
                return false;
            }

            if (start > 0) {
                var previous = s[start - 1];
                if (previous == '=' || previous == '.' || previous == '-'
                        || previous == '%' || previous == '&' || previous == '?' || previous == '='
                        || previous == '@' || previous == '#'
                        || previous == '"' || previous == '\'' || previous == '`'
                        || previous == '/' || previous == '\\'
                        || char.IsLetterOrDigit(previous)) {
                    urlLength = 0;
                    return false;
                }
            }

            if (Expect(@"http")) {
                if (char.ToLowerInvariant(s[index]) == 's') {
                    index++;
                }
                if (!Expect(@"://")) {
                    urlLength = 0;
                    return false;
                }
            } else if (Expect(@"mumble") || Expect("ts")) {
                goto EatTheRest;
            }

            var lastDot = -1;
            for (; index < length; index++) {
                var c = s[index];
                if (c == '.') {
                    if (lastDot == index - 1) {
                        urlLength = 0;
                        return false;
                    }

                    lastDot = index;
                } else if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-')) {
                    break;
                }
            }

            if (lastDot <= 0 || index - start <= 3 || !IsDomainZone(s, lastDot + 1, index - lastDot - 1)) {
                urlLength = 0;
                return false;
            }

            if (index < length - 3 && s[index] == ':' && char.IsDigit(s[index + 1])) {
                for (index++; index < length && char.IsDigit(s[index]); index++) { }
            }

            if (index >= length || s[index] != '/') {
                urlLength = index - start;
                return true;
            }

            EatTheRest:
            var last = '\0';
            if (bbCodeMode) {
                for (char c; index < length && !char.IsWhiteSpace(c = s[index]) && c != '[' && c != ']'; index++) {
                    last = c;
                }
            } else {
                for (char c; index < length && !char.IsWhiteSpace(c = s[index]); index++) {
                    last = c;
                }
            }

            urlLength = last == '.' || last == ',' || last == ':' || last == ';' || last == '!' || last == ']' ? index - start - 1 : index - start;
            return true;

            bool Expect(string p) {
                for (var i = 0; i < p.Length; i++) {
                    var j = index + i;
                    if (j >= length || char.ToLowerInvariant(s[j]) != p[i]) return false;
                }

                index += p.Length;
                return true;
            }
        }
    }
}