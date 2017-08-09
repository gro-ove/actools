using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.AcdFile;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Miscellaneous {
    /// <summary>
    /// Used for AI limitations feature, likely will be removed in the future. Also, see FakeCarIds in AcTools library.
    /// </summary>
    public static class FakeCarsHelper {
        public class Fake {
            public Fake(string fakeId, string sourceId) {
                FakeId = fakeId;
                SourceId = sourceId;
            }

            public string FakeId { get; }
            public string SourceId { get; }
        }

        [ItemNotNull]
        public static Task<List<Fake>> GetFakeCarsIds(string replayFilename) {
            return Task.Run(() => {
                var result = new List<Fake>();
                using (var reader = new ReplayReader(replayFilename)) {
                    var bytes = Encoding.UTF8.GetBytes("__cm_tmp");
                    for (var i = 0; i < 100; i++) {
                        var fakeId = reader.TryToReadNextString(bytes[0], bytes[1]);
                        if (fakeId == null) break;
                        if (result.All(x => x.FakeId != fakeId) && FakeCarIds.IsFake(fakeId, out var sourceId)) {
                            result.Add(new Fake(fakeId, sourceId));
                        }
                    }
                }

                return result;
            });
        }

        public static void CreateFakeCar([NotNull] CarObject source, string fakeCarId, [CanBeNull] Action<Acd> dataPreprocessor) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var sw = Stopwatch.StartNew();
            try {
                var path = Path.Combine(CarsManager.Instance.Directories.GetMainDirectory(), fakeCarId);
                var directory = new DirectoryInfo(path);
                if (!directory.Exists) {
                    FileUtils.HardLinkOrCopyRecursive(source.Location, path, (filename, isDirectory) => {
                        if (isDirectory) return false;
                        var relative = FileUtils.GetRelativePath(filename, source.Location).ToLower();
                        return Regex.IsMatch(relative,
                                @"^(?:animations\\\w*\.ksanim|skins\\[^\\]+\\(?!preview(?:_original)?\.jpg|ui_skin\.json)|texture\\|[^\\]+\.kn5|body_shadow\.png|tyre_shadow_\d\.png|driver_base_pos\.knh)");
                    }, true);

                    CarObject.ReplaceSound(source, path);
                } else {
                    var now = DateTime.Now;
                    if ((now - directory.LastWriteTime).TotalMinutes > 1d) {
                        return;
                    }

                    directory.LastWriteTime = now;
                }

                var dataFilename = Path.Combine(source.Location, "data.acd");
                Acd acd;
                if (File.Exists(dataFilename)) {
                    acd = Acd.FromFile(dataFilename);
                } else {
                    var dataDirectory = Path.Combine(source.Location, "data");
                    if (Directory.Exists(dataDirectory)) {
                        acd = Acd.FromDirectory(dataDirectory);
                    } else {
                        return;
                    }
                }

                dataPreprocessor?.Invoke(acd);
                acd.Save(Path.Combine(path, "data.acd"));
            } finally {
                Logging.Debug($"Time taken to create {fakeCarId}: {sw.Elapsed.TotalMilliseconds:F1} ms");
            }
        }
    }
}