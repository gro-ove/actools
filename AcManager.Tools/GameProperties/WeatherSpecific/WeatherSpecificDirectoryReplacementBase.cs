using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using StringBasedFilter.Utils;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    internal class WeatherSpecificDirectoryReplacementBase : TemporaryDirectoryReplacementBase, IWeatherSpecificReplacement {
        [CanBeNull]
        public string[] SourceList { get; set; }

        public string RelativeSource { get; }

        internal WeatherSpecificDirectoryReplacementBase(string relativeSource, string relativeDestination)
                : base(relativeDestination) {
            RelativeSource = relativeSource;
        }

        public bool Apply(WeatherObject weather) {
            return Apply(weather.Location);
        }

        // More information, tests and usage examples:
        // https://gist.github.com/gro-ove/a9e946ce271f89fe5b5f537c1b50c0c8?ts=4
        // TODO: Move tests somewhere nearby
        private IEnumerable<string> GetFiles(string query, string relativeQueryOrigin, bool optionalExtension) {
            var normalized = FileUtils.NormalizePath(Path.IsPathRooted(query) ? query : Path.Combine(relativeQueryOrigin, query));
            var pieces = normalized.Split('\\').Select(x => new { Value = x, IsQuery = RegexFromQuery.IsQuery(x) }).ToArray();
            return pieces.All(x => !x.IsQuery) || pieces.Length < 2
                    ? File.Exists(normalized)
                            ? new[] { normalized }
                            : optionalExtension
                                    ? Directory.GetFiles(Path.GetDirectoryName(normalized) ?? @".", Path.GetFileName(normalized) + ".*")
                                    : new string[0]
                    : pieces.Skip(1).Aggregate(
                            pieces[0].IsQuery
                                    ? DriveInfo.GetDrives().Select(x => x.RootDirectory.FullName).ToArray()
                                    : new[] { pieces[0].Value + Path.DirectorySeparatorChar },
                            (loc, piece) => {
                                var isLast = piece == pieces[pieces.Length - 1];
                                return (piece.IsQuery
                                        ? loc.SelectMany(x => isLast ? Directory.GetFiles(x, piece.Value) : Directory.GetDirectories(x, piece.Value))
                                        : loc.Select(x => Path.Combine(x, piece.Value)).Where(x => isLast ? File.Exists(x) : Directory.Exists(x))).ToArray();
                            });
        }

        public IEnumerable<string> GetFiles(string query, string relativeQueryOrigin, string localOffsetForIds) {
            try {
                if (query.IndexOfAny(new[] { '/', '\\' }) == -1 && localOffsetForIds != null) {
                    return GetFiles(Path.Combine(localOffsetForIds, query), relativeQueryOrigin, true);
                }
                return GetFiles(query, relativeQueryOrigin, false);
            } catch (Exception e) {
                Logging.Warning(e);
                return new string[0];
            }
        }

        protected override bool IsActive(string source) {
            return SourceList != null ? base.IsActive(source) : Directory.Exists(Path.Combine(source, RelativeSource));
        }

        protected override void Apply(string source, string destination) {
            FileUtils.EnsureDirectoryExists(destination);

            if (SourceList != null) {
                foreach (var item in SourceList) {
                    foreach (var file in GetFiles(item, source, RelativeSource)) {
                        var fileName = Path.GetFileName(file);
                        if (fileName != null) {
                            FileUtils.HardLinkOrCopy(file, Path.Combine(destination, fileName), true);
                        }
                    }
                }
            } else if (Directory.Exists(Path.Combine(source, RelativeSource))) {
                base.Apply(Path.Combine(source, RelativeSource), destination);
            }

            if (Directory.GetFiles(destination).Length == 0) {
                CreateEmptyTexture(Path.Combine(destination, "nothing.dds"));
            }

            void CreateEmptyTexture(string filename) {
                var data = @"c3EJVqhhYGBgF+BiYAHSIOzAAAHMDKQBBSB2RGIzMPyHITCHQ8ABUxONAAA".FromCutBase64();
                if (data == null) return;

                using (var memory = new MemoryStream(data))
                using (var output = new MemoryStream()) {
                    using (var decomp = new DeflateStream(memory, CompressionMode.Decompress)) {
                        decomp.CopyTo(output);
                    }
                    File.WriteAllBytes(filename, output.ToArray());
                }
            }
        }

        protected override string GetAbsolutePath(string relative) {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, relative);
        }
    }
}