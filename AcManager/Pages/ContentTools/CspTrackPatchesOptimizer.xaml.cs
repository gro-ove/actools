using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using NotifyPropertyChanged = FirstFloor.ModernUI.Presentation.NotifyPropertyChanged;

namespace AcManager.Pages.ContentTools {
    public partial class CspTrackPatchesOptimizer {
        private class TextureNamesCollector : IKn5TextureLoader {
            private readonly HashSet<string> _textures = new HashSet<string>();

            public void OnNewKn5(string kn5Filename) { }

            public byte[] LoadTexture(string textureName, ReadAheadBinaryReader reader, int textureSize) {
                _textures.Add(textureName);
                reader.Skip(textureSize);
                return null;
            }

            private static readonly Dictionary<string, TextureNamesCollector> _cache = new Dictionary<string, TextureNamesCollector>();

            private static TextureNamesCollector Get(string kn5Filename) {
                if (!_cache.TryGetValue(kn5Filename, out var ret)) {
                    var loader = new TextureNamesCollector();
                    Kn5.FromFile(kn5Filename, loader, SkippingMaterialLoader.Instance, SkippingNodeLoader.Instance);
                    if (FileUtils.IsAffectedBy(kn5Filename, Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "tracks"))) {
                        var kn5Dir = Path.GetDirectoryName(kn5Filename) ?? string.Empty;
                        var prefixingKn5s = Directory.GetFiles(kn5Dir, "models*.ini").Select(d => {
                            var cfg = new AcTools.DataFile.IniFile(d);
                            var files = cfg.GetExistingSectionNames("MODEL").Select(c => cfg[c].GetNonEmpty("FILE")).ToList();
                            var i = files.IndexOf(Path.GetFileName(kn5Filename));
                            if (i == -1) {
                                return null;
                            }
                            return files.Take(i).ToList();
                        }).Where(d => d != null).ToArray();
                        if (prefixingKn5s.Length > 0) {
                            var unionOfPrefixingKn5s = prefixingKn5s[0].Where(c => prefixingKn5s.Skip(1).All(y => y.Contains(c))).Distinct().ToList();
                            foreach (var kn5Name in unionOfPrefixingKn5s) {
                                var existing = Path.Combine(kn5Dir, kn5Name);
                                if (File.Exists(existing)) {
                                    var inheritingFrom = Get(existing);
                                    foreach (var tex in inheritingFrom._textures) {
                                        loader._textures.Add(tex);
                                    }
                                }
                            }
                        }
                    }
                    ret = loader;
                    _cache[kn5Filename] = ret;
                }
                return ret;
            }

            public static HashSet<string> Collect(string kn5Filename) {
                return Get(kn5Filename)._textures;
            }
        }

        private class NodeNamesCollector : IKn5NodeLoader {
            private readonly HashSet<string> _nodeNames = new HashSet<string>();

            public void OnNewKn5(string kn5Filename) { }

            private Kn5Node LoadNode(IKn5Reader reader) {
                var node = reader.ReadNodeHierarchy();
                _nodeNames.Add(node.Name);
                var capacity = node.Children.Capacity;
                for (var i = 0; i < capacity; i++) {
                    node.Children.Add(LoadNode(reader));
                }
                return node;
            }

            public Kn5Node LoadNode(ReadAheadBinaryReader reader) {
                return LoadNode((IKn5Reader)reader);
            }

            private static readonly Dictionary<string, NodeNamesCollector> _cache = new Dictionary<string, NodeNamesCollector>();

            private static NodeNamesCollector Get(string kn5Filename) {
                if (!_cache.TryGetValue(kn5Filename, out var ret)) {
                    var loader = new NodeNamesCollector();
                    Kn5.FromFile(kn5Filename, SkippingTextureLoader.Instance, SkippingMaterialLoader.Instance, loader);
                    ret = loader;
                    _cache[kn5Filename] = ret;
                }
                return ret;
            }

            public static bool HasNode(string kn5Filename, string name) {
                return Get(kn5Filename)._nodeNames.Contains(name);
            }
        }

        private static void CleanReplacementTextures(string kn5, List<string> texturesToRemove) {
            try {
                using (var reader = new ReadAheadBinaryReader(kn5)) {
                    if (new string(reader.ReadChars(6)) != "sc6969") throw new Exception("Invalid header");
                    if (reader.ReadInt32() > 5 && reader.ReadInt32() != 0) throw new Exception("Invalid version");
                    var textureCount = reader.ReadInt32();
                    if (textureCount == 0) return;
                    var texturesToKeep = new Dictionary<string, byte[]>();
                    for (var i = 0; i < textureCount; ++i) {
                        var activeFlag = reader.ReadInt32();
                        var name = reader.ReadString();
                        if (name == ".cleaned") return;
                        var length = (int)reader.ReadUInt32();
                        if (activeFlag != 1) throw new Exception("Disabled texture");
                        if (texturesToRemove.Contains(name)) {
                            reader.Skip(length);
                        } else {
                            texturesToKeep[name] = reader.ReadBytes(length);
                        }
                    }
                    if (texturesToKeep.Count == textureCount) return;
                    texturesToKeep[".cleaned"] = new byte[0];
                    using (var data = new ExtendedBinaryWriter(kn5 + ".filtered")) {
                        data.Write(Encoding.ASCII.GetBytes("sc6969"));
                        data.Write(5);
                        data.Write(texturesToKeep.Count);
                        foreach (var p in texturesToKeep) {
                            data.Write(1);
                            data.Write(p.Key);
                            Console.WriteLine($"Encoding: {p.Key}, {p.Value.Length} bytes");
                            if (p.Value.Length > 0) {
                                try {
                                    var repacked = Bc7Encoder.EncodeTextureAsync(new Bc7Encoder.StreamSpan(p.Value), new Bc7Encoder.EncodeParams {
                                        ResizeMode = Bc7Encoder.ResizeMode.Crop
                                    }, CancellationToken.None).Result;
                                    data.Write(repacked.Length);
                                    repacked.WriteTo(data.BaseStream);
                                    continue;
                                } catch (Exception e) {
                                    Logging.Warning($"Failed to reencode {p.Key}: {e}");
                                }
                            }
                            data.Write(p.Value.Length);
                            data.Write(p.Value);
                        }
                        reader.CopyTo(data.BaseStream);
                    }
                }
                File.Delete(kn5);
                File.Move(kn5 + ".filtered", kn5);
            } finally {
                FileUtils.TryToDelete(kn5 + ".filtered");
            }
        }

        private static List<string> CollectTexturesToRemove(string kn5Filename, HashSet<string> namesToRemove) {
            var texturesToRemove = new List<string>();
            try {
                using (var reader = new ReadAheadBinaryReader(kn5Filename)) {
                    if (new string(reader.ReadChars(6)) != "sc6969") throw new Exception("Invalid header");
                    if (reader.ReadInt32() > 5 && reader.ReadInt32() != 0) throw new Exception("Invalid version");
                    var textureCount = reader.ReadInt32();
                    if (textureCount == 0) return texturesToRemove;
                    for (var i = 0; i < textureCount; ++i) {
                        var activeFlag = reader.ReadInt32();
                        var name = reader.ReadString();
                        if (name == ".cleaned") return new List<string>();
                        var length = (int)reader.ReadUInt32();
                        if (activeFlag != 1) throw new Exception("Disabled texture");
                        if (namesToRemove.Contains(name)) {
                            texturesToRemove.Add(name);
                        }
                        reader.Skip(length);
                    }
                }
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Failed to check KN5 for a cleanup", e);
            }
            return texturesToRemove;
        }

        private static HashSet<string> HashUnion(HashSet<string> a, HashSet<string> b) {
            var ret = new HashSet<string>();
            foreach (var v in a) {
                if (b.Contains(v)) {
                    ret.Add(v);
                }
            }
            return ret;
        }

        public class Kn5ToOptimize : NotifyPropertyChanged {
            public string Kn5 { get; }

            private readonly List<string> _texturesToRemove;

            public string DisplayTexturesToRemove => $"Unnecessary textures to remove: {_texturesToRemove.JoinToString("; ")}";

            public Kn5ToOptimize(string kn5, List<string> texturesToRemove) {
                Kn5 = kn5;
                _texturesToRemove = texturesToRemove;
            }

            private bool _optimized;

            public bool Optimized {
                get => _optimized;
                set => Apply(value, ref _optimized);
            }

            private AsyncCommand _optimizeCommand;

            public AsyncCommand OptimizeCommand => _optimizeCommand ?? (_optimizeCommand = new AsyncCommand(async () => {
                if (Optimized) return;
                try {
                    await Task.Run(() => CleanReplacementTextures(Kn5, _texturesToRemove));
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Failed to optimize inserting KN5", e);
                }
                Optimized = true;
            }, () => !_optimized));
        }

        private AsyncCommand _optimizeAllCommand;

        public AsyncCommand OptimizeAllCommand => _optimizeAllCommand ?? (_optimizeAllCommand = new AsyncCommand(async () => {
            using (var waiting = new WaitingDialog("Optimizing…")) {
                var list = Found.ToList();
                await list.Select((x, i) => {
                    waiting.Report(new AsyncProgressEntry(Path.GetFileName(x.Kn5), i, list.Count));
                    return x.OptimizeCommand.ExecuteAsync();
                }).WhenAll(4);
            }
        }));

        public List<Kn5ToOptimize> Found { get; private set; } = new List<Kn5ToOptimize>();

        protected override async Task<bool> LoadAsyncOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var list = new List<Tuple<string, string, string>>();
            var found = new List<Kn5ToOptimize>();
            progress.Report(AsyncProgressEntry.FromStringIndetermitate("Collecting configs…"));
            await Task.Run(() => {
                void ProcessFiles(string configDir, string acDir) {
                    foreach (var configFilename in Directory.GetFiles(configDir, "*.ini")) {
                        var contentId = Path.GetFileNameWithoutExtension(configFilename);
                        var contentDir = Path.Combine(acDir, contentId);
                        if (Directory.Exists(contentDir)) {
                            list.Add(Tuple.Create(contentId, configFilename, contentDir));
                        }
                    }
                }

                ProcessFiles(Path.Combine(AcRootDirectory.Instance.RequireValue, @"extension\config\cars\kunos"),
                        Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "cars"));
                ProcessFiles(Path.Combine(AcRootDirectory.Instance.RequireValue, @"extension\config\tracks"),
                        Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "tracks"));

                void FoundMatch(string kn5, HashSet<string> texturesToRemove) {
                    var removeEntries = CollectTexturesToRemove(kn5, texturesToRemove);
                    if (removeEntries.Count == 0) return;
                    found.Add(new Kn5ToOptimize(kn5, removeEntries));
                }

                for (var i = 0; i < list.Count; i++) {
                    var entry = list[i];
                    progress.Report(entry.Item1, i, list.Count);

                    var configData = File.ReadAllText(entry.Item2);
                    var pieces = configData.Split('[');
                    foreach (var piece in pieces) {
                        if (piece.StartsWith("MODEL_REPLACEMENT_")) {
                            var file = Regex.Match(piece, @"\bFILE\s*=\s*(.+\.kn5)");
                            var insert = Regex.Match(piece, @"\bINSERT\s*=\s*(.+\.kn5)");
                            if (!insert.Success) {
                                continue;
                            }

                            var insertKn5 = Path.Combine(Path.GetDirectoryName(entry.Item2) ?? string.Empty, insert.Groups[1].Value);
                            if (!File.Exists(insertKn5)) {
                                continue;
                            }

                            var origKn5Name = file.Success ? file.Groups[1].Value : null;
                            if (origKn5Name == null || origKn5Name == "?.kn5") {
                                var kn5s = Directory.GetFiles(entry.Item3, "*.kn5");
                                if (kn5s.Length == 1) {
                                    origKn5Name = Path.GetFileName(kn5s[0]);
                                } else {
                                    var insertAfter = Regex.Match(piece, @"\bINSERT_AFTER\s*=\s*(.+)").Groups[1].Value.Trim();
                                    if (insertAfter == "") {
                                        continue;
                                    }
                                    var cands = kn5s.Where(k => NodeNamesCollector.HasNode(k, insertAfter)).ToList();
                                    if (cands.Count == 1) {
                                        origKn5Name = Path.GetFileName(cands[0]);
                                    } else {
                                        if (cands.Count == 2) {
                                            var tex0 = TextureNamesCollector.Collect(cands[0]);
                                            var tex1 = TextureNamesCollector.Collect(cands[1]);
                                            FoundMatch(insertKn5, HashUnion(tex0, tex1));
                                        }
                                        continue;
                                    }
                                }
                            }
                            if (origKn5Name == null) {
                                continue;
                            }

                            var origKn5 = Path.Combine(entry.Item3, origKn5Name);
                            if (!File.Exists(origKn5)) {
                                continue;
                            }
                            FoundMatch(insertKn5, TextureNamesCollector.Collect(origKn5));
                        }
                    }
                }
            });
            Found = found;
            OnPropertyChanged(nameof(Found));
            return Found.Count > 0;
        }

        protected override void InitializeOverride(Uri uri) { }
    }
}