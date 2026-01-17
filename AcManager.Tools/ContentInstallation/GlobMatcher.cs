using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AcManager.Tools.ContentInstallation {
    public static class GlobMatcher {
        public static IEnumerable<Tuple<string, string>> Find(string dir, string pattern, string replaceTemplate) {
            pattern = pattern.Replace('\\', '/');
            string[] parts = pattern.Split('/');

            // Count all '*' occurrences to know how many capture groups exist
            int totalCaptures = 0;
            foreach (var p in parts)
                totalCaptures += CountStars(p);

            var captures = new string[totalCaptures];
            return FindRecursive(parts, 0, dir, replaceTemplate, captures, 0);
        }

        private static IEnumerable<Tuple<string, string>> FindRecursive(string[] parts, int index, string path,
                string replaceTemplate, string[] captures, int capIndex) {
            bool last = index == parts.Length - 1;
            string part = parts[index];

            if (!HasWildcard(part)) {
                // No wildcard → descend directly
                string next = Path.Combine(path, part);
                if (Directory.Exists(next) || (last && File.Exists(next))) {
                    foreach (var r in FindRecursive(parts, index + 1, next,
                            replaceTemplate, captures, capIndex))
                        yield return r;
                }
                yield break;
            }

            // Convert glob part to regex with numbered captures
            string regex = "^" + GlobPartToRegex(part, capIndex, out int added) + "$";
            var re = new Regex(regex, RegexOptions.IgnoreCase);

            if (last) {
                // Match files
                string current = path == "" ? "." : path;
                if (Directory.Exists(current)) {
                    foreach (var file in Directory.EnumerateFiles(current)) {
                        var name = Path.GetFileName(file);
                        var m = re.Match(name);
                        if (!m.Success) continue;

                        // fill captures
                        for (int i = 0; i < added; i++)
                            captures[capIndex + i] = m.Groups[i + 1].Value;

                        yield return Tuple.Create(file, ApplyReplacement(replaceTemplate, captures));
                    }
                }
                yield break;
            }

            // Otherwise: match directories
            string cur = path == "" ? "." : path;
            if (Directory.Exists(cur)) {
                foreach (var dir in Directory.EnumerateDirectories(cur)) {
                    var name = Path.GetFileName(dir);
                    var m = re.Match(name);
                    if (!m.Success) continue;

                    // fill captures
                    for (int i = 0; i < added; i++)
                        captures[capIndex + i] = m.Groups[i + 1].Value;

                    foreach (var r in FindRecursive(parts, index + 1, dir,
                            replaceTemplate, captures, capIndex + added))
                        yield return r;
                }
            }
        }

        // Convert a single glob part (e.g. "white_*_skin*") into a regex
        private static string GlobPartToRegex(string part, int startIndex, out int added) {
            added = 0;
            var sb = new System.Text.StringBuilder();
            foreach (char c in part) {
                if (c == '*') {
                    added++;
                    sb.Append("(.*?)"); // non-greedy
                } else if (c == '?') {
                    sb.Append("."); // does NOT capture
                } else {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }
            return sb.ToString();
        }

        private static string ApplyReplacement(string template, string[] captures) {
            string output = template;
            for (int i = 0; i < captures.Length; i++)
                output = output.Replace($"${i + 1}", captures[i]);

            return output;
        }

        private static bool HasWildcard(string s) => s.IndexOf('*') != -1 || s.IndexOf('?') != -1;

        private static int CountStars(string s) {
            int count = 0;
            foreach (char c in s)
                if (c == '*') count++;
            return count;
        }
    }
}