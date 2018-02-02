using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AcManager.Tools.Helpers;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.GameProperties {
    public class AcShadowsPatcher : Game.GameHandler {
        public AcShadowsPatcher(IAcsStarter starter) {
            starter.PreviewRun += OnPreviewRun;
        }

        private static void OnPreviewRun(object sender, AcsRunEventArgs acsRunEventArgs) {
            if (acsRunEventArgs.AcsFilename != null && acsRunEventArgs.Use32BitVersion.HasValue) {
                PatchAc(acsRunEventArgs.AcsFilename, acsRunEventArgs.Use32BitVersion.Value);
            }
        }

        private static string ComputeChecksum(string filename) {
            using (var s = SHA256.Create())
            using (var f = File.OpenRead(filename)){
                return s.ComputeHash(f).ToHexString();
            }
        }

        private static IStorage _patchedStorage = new Substorage(ValuesStorage.Storage, "AcShadowsPatcher");

        private struct CommandToPatch {
            [JsonProperty("o")]
            public int Offset;

            [JsonProperty("r")]
            public int OriginalOpCode;

            [JsonProperty("a")]
            public int PatchedOpCode;
        }

        private static void PatchFile(string filename, CommandToPatch toPatch, bool save) {
            filename = FileUtils.NormalizePath(filename);

            if (toPatch.PatchedOpCode < byte.MinValue || toPatch.PatchedOpCode > byte.MaxValue) {
                // Just in case, this is delicate stuff
                throw new PatchException($"New opcode is out of bounds: {toPatch.PatchedOpCode}");
            }

            var data = File.ReadAllBytes(filename);
            if (toPatch.Offset <= 0 || data.Length <= toPatch.Offset) {
                throw new PatchException($"Invalid offset 0x{toPatch.Offset:X}");
            }

            if (data[toPatch.Offset] != toPatch.OriginalOpCode) {
                throw new PatchException($"Current code doesn’t match expected: 0x{data[toPatch.Offset]:X2}≠0x{toPatch.OriginalOpCode:X2}");
            }

            data[toPatch.Offset] = (byte)toPatch.PatchedOpCode;

            var backupFilename = filename.ApartFromLast(".exe", StringComparison.OrdinalIgnoreCase) + @"_backup_shadows.exe";
            if (save) {
                if (File.Exists(backupFilename) && !FileUtils.TryToDelete(backupFilename)) {
                    throw new PatchException("Place for backup is already taken");
                }

                File.Move(filename, backupFilename);
                File.WriteAllBytes(filename, data);
                _patchedStorage.SetObject(filename, toPatch);
            } else if (File.Exists(backupFilename) && FileUtils.TryToDelete(filename)) {
                File.Move(backupFilename, filename);
            } else {
                File.WriteAllBytes(filename, data);
            }
        }

        private class Entry {
            [JsonProperty("offset")]
            public int Offset;

            [JsonProperty("original")]
            public string Original;

            [JsonProperty("patched")]
            public string Patched;

            [JsonProperty("originalOpCode")]
            public int OriginalOpCode;

            [JsonProperty("patchedOpCode")]
            public int PatchedOpCode;

            public CommandToPatch ToPatch() {
                return new CommandToPatch {
                    Offset = Offset,
                    OriginalOpCode = OriginalOpCode,
                    PatchedOpCode = PatchedOpCode
                };
            }
        }

        private static void PatchAc([NotNull] string acsFilename, bool use32BitVersion) {
            try {
                var data = FilesStorage.Instance.GetContentFile(ContentCategory.Miscellaneous, "DisableShadows.json");
                if (!data.Exists) {
                    throw new PatchException("DisableShadows.json doesn’t exist");
                }

                var dictionary = JObject.Parse(File.ReadAllText(data.Filename))[use32BitVersion ? "x86" : "x64"]?.ToObject<Dictionary<string, Entry>>();
                if (dictionary == null) {
                    throw new PatchException("DisableShadows.json doesn’t exist");
                }

                var checksum = ComputeChecksum(acsFilename);
                var toPatch = dictionary.FirstOrDefault(x => string.Equals(x.Value.Original, checksum, StringComparison.OrdinalIgnoreCase));
                if (toPatch.Value != null) {
                    Logging.Write($"Description found: version={toPatch.Key}, checksum={checksum}");
                    PatchFile(acsFilename, toPatch.Value.ToPatch(), true);
                } else {
                    var alreadyPatched = dictionary.FirstOrDefault(x => string.Equals(x.Value.Patched, checksum, StringComparison.OrdinalIgnoreCase));
                    if (alreadyPatched.Value != null) {
                        Logging.Write($"Already patched: version={toPatch.Key}, checksum={checksum}");
                    } else {
                        throw new PatchException($"Description for executable is missing: {checksum}");
                    }
                }
            } catch (PatchException e) {
                NonfatalError.NotifyBackground("Can’t patch AC to disable shadows", e.Message.ToSentence());
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t patch AC to disable shadows", e);
            }
        }

        public static void Revert() {
            foreach (var key in _patchedStorage.Keys.ToList()) {
                var data = _patchedStorage.GetObject<CommandToPatch>(key);
                if (data.Offset > 0) {
                    ObjectExtension.Swap(ref data.OriginalOpCode, ref data.PatchedOpCode);
                    try {
                        PatchFile(key, data, false);
                        _patchedStorage.Remove(key);
                    } catch (PatchException e) {
                        NonfatalError.NotifyBackground("Can’t revert AC to its original state", e.Message.ToSentence());
                    } catch (Exception e) {
                        NonfatalError.NotifyBackground("Can’t revert AC to its original state", e);
                    }
                }
            }
        }

        public override IDisposable Set(Process process) {
            process?.WaitForExitAsync().ContinueWith(t => Revert());
            return null;
        }

        private class PatchException : Exception {
            public PatchException(string message) : base(message) { }
        }
    }
}