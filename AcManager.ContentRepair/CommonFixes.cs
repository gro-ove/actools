using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.ContentRepair.Repairs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcLog;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using SharpCompress.Archives.Zip;

namespace AcManager.ContentRepair {
    public static class CommonFixes {
        public static Task FixMissingKsSkyBoxShader(string filename, CancellationToken cancellation) {
            return Task.Run(() => {
                var backup = FileUtils.EnsureUnique(filename + @".bak");
                File.Move(filename, backup);
                cancellation.ThrowIfCancellationRequested();

                var kn5 = Kn5.FromFile(backup);
                cancellation.ThrowIfCancellationRequested();

                var changed = false;
                foreach (var material in kn5.Materials.Values) {
                    if (material.ShaderName == @"ksSkyBox") {
                        material.ShaderName = @"ksSky";
                        material.TextureMappings = new Kn5Material.TextureMapping[0];
                        changed = true;
                    }
                }

                if (!changed) {
                    throw new InformativeException("Nothing to fix", "None of materials is using ksSkyBox shader");
                }

                cancellation.ThrowIfCancellationRequested();
                kn5.Save(filename);
            }, cancellation); 
        }

        public static Task FixMissingDefaultPpFilter(CancellationToken cancellation) {
            return Task.Run(() => {
                var original = CmApiProvider.GetData("static/get/pp_default");
                cancellation.ThrowIfCancellationRequested();
                if (original == null) throw new InformativeException("Can’t load original filter");

                using (var stream = new MemoryStream(original))
                using (var zip = ZipArchive.Open(stream)) {
                    var entry = zip.Entries.FirstOrDefault(x => x.Key == @"default.ini");
                    if (entry == null) throw new Exception("Invalid data");

                    File.WriteAllBytes(PpFiltersManager.Instance.DefaultFilename, entry.OpenEntryStream().ReadAsBytesAndDispose());
                }
            }, cancellation); 
        }

        public static Func<CancellationToken, Task> FixOldFlames(IEnumerable<string> carIds) {
            return async c => { 
                foreach (var car in carIds.Select(x => CarsManager.Instance.GetById(x)).Where(x => x != null)) {
                    await CarFlamesRepair.UpgradeToSecondVersionAsync(car, cancellation: c);
                    if (c.IsCancellationRequested) return;
                }
            };
        }

        public static Func<CancellationToken, Task> FixMissingFlamesTextures(IEnumerable<string> carIds) {
            return async c => {
                foreach (var car in carIds.Select(x => CarsManager.Instance.GetById(x)).Where(x => x != null)) {
                    await CarFlamesRepair.FixMissingTexturesAsync(car, cancellation: c);
                }
            };
        }

        public static Func<CancellationToken, Task> FixMissingSuspNodes(IEnumerable<string> carIds) {
            return async c => {
                foreach (var car in carIds.Select(x => CarsManager.Instance.GetById(x)).Where(x => x != null)) {
                    await CarModelRepair.FixSuspensionNodesAsync(car, cancellation: c);
                }
            };
        }

        public static void Initialize() {
            AcLogHelper.AddExtension(new LogExtension());
        }

        public class LogExtension : IAcLogHelperExtension {
            private static IEnumerable<string> GetCarsIds(string log) {
                return Regex.Matches(log, @"content/cars/(\w+)").Cast<Match>().Select(x => x.Groups[1].Value).Distinct().ToList();
            }

            public WhatsGoingOn Detect(string log, string crash) {
                if (log.Contains(@"Flame V1: texture not found")) {
                    return new WhatsGoingOn(WhatsGoingOnType.FlamesV1TextureNotFound) {
                        Fix = FixOldFlames(GetCarsIds(log))
                    };
                }

                if (log.Contains(@"FlameStart : Flash textures are missing!")) {
                    return new WhatsGoingOn(WhatsGoingOnType.FlamesFlashTexturesAreMissing) {
                        Fix = FixMissingFlamesTextures(GetCarsIds(log))
                    };
                }

                if (log.Contains(@"COULD NOT FIND SUSPENSION OBJECT SUSP_")) {
                    return new WhatsGoingOn(WhatsGoingOnType.SuspensionIsMissing) {
                        Fix = FixMissingSuspNodes(GetCarsIds(log))
                    };
                }

                if (log.Contains(@"Error, cannot initialize Post Processing, system/cfg/ppfilters/default.ini not found")) {
                    return new WhatsGoingOn(WhatsGoingOnType.DefaultPpFilterIsMissing) {
                        Fix = FixMissingDefaultPpFilter
                    };
                }

                {
                    var match = Regex.Match(log, @"ERROR: Shader (.+) NOT FOUND, RETURNING NULL");
                    if (match.Success) {
                        var shader = match.Groups[1].Value;
                        Func<CancellationToken, Task> fix = null;

                        if (shader == @"ksSkyBox" && AcRootDirectory.Instance.Value != null) {
                            var model = Regex.Matches(log, @"LOADING MODEL (.+\.kn5)").OfType<Match>().LastOrDefault();
                            if (model?.Success == true) {
                                var filename = Path.Combine(AcRootDirectory.Instance.Value, model.Groups[1].Value);
                                fix = c => FixMissingKsSkyBoxShader(filename, c);
                            }
                        }

                        return new WhatsGoingOn(WhatsGoingOnType.ShaderIsMissing, match.Groups[1].Value) {
                            Fix = fix
                        };
                    }
                }

                return null;
            }
        }
    }
}