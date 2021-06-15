using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.ExtraKn5Utils.FbxUtils;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;
using AcTools.ExtraKn5Utils.Kn5Utils;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public class CarLodGenerator : IDisposable {
        private readonly ICarLodGeneratorService _service;
        private readonly string _carDirectory;
        private readonly string _temporaryDirectory;

        public bool GenerateCockpitLr { get; set; }
        public bool GenerateLodB { get; set; }
        public bool GenerateLodC { get; set; }
        public bool GenerateLodD { get; set; }

        public string[] NewModels { get; }
        public bool LodsIniNeedsSaving { get; }
        public bool HasCockpitHr { get; }

        private DataWrapper _carData;
        private IniFile _lodsIniFile;
        private byte[] _originalKn5Data;

        [CanBeNull]
        public List<string> BodyMaterials;

        public CarLodGenerator(ICarLodGeneratorService service, string carDirectory, string temporaryDirectory) {
            _service = service;
            _carDirectory = carDirectory;
            _temporaryDirectory = temporaryDirectory;
            _carData = DataWrapper.FromCarDirectory(carDirectory);
            _lodsIniFile = _carData.GetIniFile("lods.ini");

            var modelOriginal = AcPaths.GetMainCarFilename(carDirectory, _carData, false);
            if (modelOriginal == null) throw new Exception("Failed to find main car model");

            var lodsSections = _lodsIniFile.GetSections("LOD").ToList();
            if (lodsSections.Count < 4) {
                var pathPrefix = Path.Combine(carDirectory, Path.GetFileNameWithoutExtension(modelOriginal));
                LodsIniNeedsSaving = true;
                _lodsIniFile["COCKPIT_HR"].Set("DISTANCE_SWITCH", 7d);
                _lodsIniFile["LOD_0"].Set("OUT", 15d);
                _lodsIniFile["LOD_1"].Set("FILE", _lodsIniFile["LOD_1"].GetNonEmpty("FILE") ?? pathPrefix + "_lod_b.kn5");
                _lodsIniFile["LOD_1"].Set("IN", 15d);
                _lodsIniFile["LOD_1"].Set("OUT", 45d);
                _lodsIniFile["LOD_2"].Set("FILE", _lodsIniFile["LOD_2"].GetNonEmpty("FILE") ?? pathPrefix + "_lod_c.kn5");
                _lodsIniFile["LOD_2"].Set("IN", 45d);
                _lodsIniFile["LOD_2"].Set("OUT", 201d);
                _lodsIniFile["LOD_3"].Set("FILE", _lodsIniFile["LOD_3"].GetNonEmpty("FILE") ?? pathPrefix + "_lod_d.kn5");
                _lodsIniFile["LOD_3"].Set("IN", 201d);
                _lodsIniFile["LOD_3"].Set("OUT", 2000d);
            } else if (lodsSections.Count > 4) {
                throw new Exception("Unsupported LODs arrangement, 4 is required");
            }

            _originalKn5Data = File.ReadAllBytes(modelOriginal);

            var originalKn5 = Kn5.FromBytes(_originalKn5Data, SkippingTextureLoader.Instance);
            BodyMaterials = originalKn5.FindBodyMaterials();
            HasCockpitHr = originalKn5.HasCockpitHr();

            NewModels = new[] {
                Path.Combine(temporaryDirectory, _lodsIniFile["LOD_0"].GetNonEmpty("FILE") ?? throw new Exception("Unsupported LODs arrangement")),
                Path.Combine(temporaryDirectory, _lodsIniFile["LOD_1"].GetNonEmpty("FILE") ?? throw new Exception("Unsupported LODs arrangement")),
                Path.Combine(temporaryDirectory, _lodsIniFile["LOD_2"].GetNonEmpty("FILE") ?? throw new Exception("Unsupported LODs arrangement")),
                Path.Combine(temporaryDirectory, _lodsIniFile["LOD_3"].GetNonEmpty("FILE") ?? throw new Exception("Unsupported LODs arrangement"))
            };
        }

        public void ApplyLods() {
            foreach (var model in NewModels) {
                var destination = Path.Combine(_carDirectory, Path.GetFileName(model));
                using (var replacement = FileUtils.RecycleOriginal(destination)) {
                    FileUtils.Move(model, replacement.Filename);
                }
            }
        }

        public void SaveLodsIni(bool saveUnpacked) {
            if (saveUnpacked) {
                var destination = Path.Combine(_carDirectory, "data", "lods.ini");
                FileUtils.EnsureFileDirectoryExists(destination);
                using (var replacement = FileUtils.RecycleOriginal(destination)) {
                    _lodsIniFile.Save(replacement.Filename);
                }
            } else if (LodsIniNeedsSaving) {
                _lodsIniFile.Save();
            }
        }

        public async Task RunAsync(IProgress<CarLodGeneratorProgressUpdate> progress = null, CancellationToken cancellationToken = default) {
            await new[] {
                HasCockpitHr && GenerateCockpitLr ? GenerateLodStageAsync(0, @"CockpitLr", NewModels[0],
                        CreateProgress("CockpitLr"), cancellationToken) : null,
                GenerateLodB ? GenerateLodStageAsync(1, @"LodB", NewModels[1],
                        CreateProgress("LodB"), cancellationToken) : null,
                GenerateLodC ? GenerateLodStageAsync(2, @"LodC", NewModels[2],
                        CreateProgress("LodC"), cancellationToken) : null,
                GenerateLodD ? GenerateLodStageAsync(3, @"LodD", NewModels[3],
                        CreateProgress("LodD"), cancellationToken) : null
            }.NonNull().WhenAll();

            IProgress<double?> CreateProgress(string key) {
                return progress == null ? null : new Progress<double?>(v => progress.Report(new CarLodGeneratorProgressUpdate {
                    Key = key,
                    Value = v
                }));
            }
        }

        public void Dispose() { }

        private static void MergeMeshes(Kn5Node root, List<Tuple<Kn5Node, double, Matrix>> children) {
            if (children.Count == 0) return;

            var builder = new Kn5MeshBuilder();
            var mesh = children[0].Item1;
            var priority = children[0].Item2;

            var extraCounter = 0;
            foreach (var child in children) {
                var transform = child.Item3 * Matrix.Scaling(new Vector3((float)priority));
                for (var i = 0; i < child.Item1.Indices.Length; ++i) {
                    builder.AddVertex(child.Item1.Vertices[child.Item1.Indices[i]].Transform(transform));
                    if (i % 3 == 2 && builder.IsCloseToLimit) {
                        builder.SetTo(mesh);
                        root.Children.Add(mesh);
                        mesh.Tag = child.Item2;

                        builder.Clear();
                        mesh = Kn5MeshUtils.Create(children[0].Item1.Name + $"___$extra:{extraCounter}", children[0].Item1.MaterialId);
                    }
                }
            }

            if (builder.Count > 0) {
                builder.SetTo(mesh);
                root.Children.Add(mesh);
                mesh.Tag = priority;
            }
        }

        private static IKn5 PrepareForGeneration(CarLodGeneratorMergeRules mergeRules, byte[] kn5Data) {
            var toMerge = new Dictionary<Kn5Node, List<Tuple<Kn5Node, double, Matrix>>>();
            var nodeIndices = new Dictionary<Kn5Node, int>();
            var ret = Kn5.FromBytes(kn5Data, SkippingTextureLoader.Instance);
            MergeNode(ret.RootNode, ret.RootNode, 1d);
            foreach (var pair in toMerge) {
                foreach (var group in pair.Value.GroupBy(x => mergeRules.MergeGroup(ret, x.Item1, x.Item2))
                        .OrderBy(v => mergeRules.GroupOrder(ret, v, nodeIndices)).Select(v => v.ToList()).ToList()) {
                    MergeMeshes(pair.Key, group);
                }
            }
            mergeRules.FinalizeKn5(ret);
            return ret;

            void ApplyPriority(Kn5Node mesh, double priority) {
                foreach (var v in mesh.Vertices) {
                    v.Position[0] *= (float)priority;
                    v.Position[1] *= (float)priority;
                    v.Position[2] *= (float)priority;
                }
                mesh.Tag = priority;
            }

            bool MergeNode(Kn5Node node, Kn5Node mergeRoot, double priority) {
                nodeIndices[node] = nodeIndices.Count;

                if (mergeRules.CanSkipNodeCompletely(ret, node)) {
                    if (node.NodeClass == Kn5NodeClass.Base && !mergeRules.CanRemoveEmptyNode(node)) {
                        node.Children.Clear();
                        return true;
                    }
                    return false;
                }

                var priorityAdjustment = mergeRules.CalculateReductionPriority(ret, node);
                if (priorityAdjustment != 1d && (priorityAdjustment < priority || node.NodeClass == Kn5NodeClass.Mesh)) {
                    priority = priorityAdjustment;
                }

                if (node.NodeClass == Kn5NodeClass.Mesh) {
                    if (mergeRoot != null && mergeRules.CanMergeMesh(node)) {
                        if (!toMerge.ContainsKey(mergeRoot)) {
                            toMerge[mergeRoot] = new List<Tuple<Kn5Node, double, Matrix>>();
                        }
                        toMerge[mergeRoot].Add(Tuple.Create(node, priority, node.CalculateTransformRelativeToParent(mergeRoot)));
                        return false;
                    } else if (priority != 1d) {
                        ApplyPriority(node, priority);
                    }
                    return true;
                }

                if (node.NodeClass == Kn5NodeClass.SkinnedMesh) {
                    return true;
                }

                if (mergeRoot != null) {
                    if (!mergeRules.CanMergeInsideNode(node)) {
                        mergeRoot = null;
                    } else if (mergeRules.IsNodeMergeRoot(node)) {
                        mergeRoot = node;
                    }
                }

                for (var i = 0; i < node.Children.Count; ++i) {
                    if (!MergeNode(node.Children[i], mergeRoot, priority)) {
                        node.Children.RemoveAt(i);
                        --i;
                    }
                }

                return node.Children.Count > 0 || mergeRoot == node || !mergeRules.CanRemoveEmptyNode(node);
            }
        }

        private static IKn5 LodFbxToKn5(IKn5 preparedKn5, string fbxFilename) {
            var fbx = FbxIO.Read(fbxFilename);
            var geometries = fbx.GetGeometryIds()
                    .Select(id => new { id, name = fbx.GetNode("Model", fbx.GetConnection(id)).GetName("Model") })
                    .GroupBy(v => v.name).ToDictionary(x => x.Key, x => x.Select(v => v.id).ToList());
            MergeNode(preparedKn5.RootNode);
            foreach (var geometry in geometries.Where(g => g.Value.Count > 0)) {
                var parent = preparedKn5.FirstByName(geometry.Key);
                if (parent == null) {
                    AcToolsLogging.Write($"Error: parent {geometry.Key} is missing");
                    continue;
                }

                var mesh = Kn5MeshUtils.Create($"{geometry.Key}__mesh_", parent.Children[0].MaterialId);
                if (parent.Children.Count != 1) throw new Exception("Unexpected arrangement");
                MergeMeshWith(mesh, geometry.Value.Select(x => Tuple.Create(fbx.GetGeometry(x), 1d)));
                parent.Children.Add(mesh);
            }
            RemoveEmpty(preparedKn5.RootNode);
            return preparedKn5;

            void MergeMeshWith(Kn5Node node, IEnumerable<Tuple<FbxNode, double>> geometriesList) {
                var mesh = new Kn5MeshBuilder();
                foreach (var geometry in geometriesList) {
                    var fbxIndices = geometry?.Item1?.GetRelative("PolygonVertexIndex")?.Value?.GetAsIntArray();
                    if (fbxIndices == null) {
                        continue;
                    }

                    var fbxVertices = geometry.Item1.GetRelative("Vertices").Value.GetAsFloatArray();
                    var fbxNormals = geometry.Item1.GetRelative("LayerElementNormal").GetRelative("Normals").Value.GetAsFloatArray();
                    var fbxUvs = geometry.Item1.GetRelative("LayerElementUV").GetRelative("UV").Value.GetAsFloatArray();
                    var scale = (float)geometry.Item2;

                    // $"Finalizing: {node.Name}, scale={scale}".Dump();
                    for (var i = 0; i < fbxIndices.Length; ++i) {
                        var index = fbxIndices[i] < 0 ? -fbxIndices[i] - 1 : fbxIndices[i];
                        mesh.AddVertex(new Kn5Node.Vertex {
                            Position = new[] { fbxVertices[index * 3] * scale, fbxVertices[index * 3 + 1] * scale, fbxVertices[index * 3 + 2] * scale },
                            Normal = new[] { fbxNormals[i * 3], fbxNormals[i * 3 + 1], fbxNormals[i * 3 + 2] },
                            TexC = new[] { fbxUvs[i * 2], 1f - fbxUvs[i * 2 + 1] }
                        });
                    }
                }
                mesh.SetTo(node);
                node.RecalculateTangents();
            }

            void MergeMesh(Kn5Node node, IEnumerable<Kn5Node> merges) {
                if (Regex.IsMatch(node.Name, @"___\$extra:\d+$")) {
                    node.Vertices = new Kn5Node.Vertex[0];
                    return;
                }

                MergeMeshWith(node, Enumerable.Range(-1, 100).Select(i => GetGeometry(node, i)).TakeWhile(i => i != null)
                        .Concat(merges.Select(n => GetGeometry(n))));
            }

            Tuple<FbxNode, double> GetGeometry(Kn5Node node, int extraBit = -1) {
                var name = extraBit < 0 ? node.Name : $"{node.Name}___$extra:{extraBit}";
                if (geometries.TryGetValue(name, out var list)) {
                    if (list.Count == 0) return null;
                    var geometryId = list[0];
                    list.RemoveAt(0);
                    return Tuple.Create(fbx.GetGeometry(geometryId), node.Tag is double priority ? 1d / priority : 1d);
                }
                return null;
            }

            void MergeNode(Kn5Node node) {
                foreach (var child in node.Children) {
                    if (child.NodeClass == Kn5NodeClass.Base) {
                        MergeNode(child);
                    } else if (child.NodeClass == Kn5NodeClass.Mesh && child.Vertices.Length > 0) {
                        var mergeKey = MergeKey(child);
                        var merge = node.Children.ApartFrom(child).Where(c => c.NodeClass == Kn5NodeClass.Mesh && MergeKey(c) == mergeKey).ToList();
                        MergeMesh(child, merge);
                        merge.ForEach(m => m.Vertices = new Kn5Node.Vertex[0]);
                    }
                }
            }

            int MergeKey(Kn5Node node) {
                return (int)(node.MaterialId * 397) | (node.IsTransparent ? 1 << 31 : 0) | (node.CastShadows ? 1 << 30 : 0);
            }

            bool RemoveEmpty(Kn5Node node) {
                if (node.NodeClass == Kn5NodeClass.Mesh) {
                    if (Regex.IsMatch(node.Name, @"___\$extra:\d+$")) return false;
                    return node.Vertices.Length > 0;
                }
                if (node.NodeClass == Kn5NodeClass.SkinnedMesh) {
                    return false;
                }
                for (var i = 0; i < node.Children.Count; ++i) {
                    if (!RemoveEmpty(node.Children[i])) {
                        node.Children.RemoveAt(i);
                        --i;
                    }
                }
                return node.Children.Count > 0;
            }
        }

        private async Task<IKn5> GenerateLodAsync(string simplygonFbx, IKn5 originalKn5, string rulesKey, string tmpPostfix,
                IProgress<double?> progress, CancellationToken cancellationToken) {
            var temporaryOutputFilename = Path.Combine(_temporaryDirectory, $"out_{tmpPostfix}.fbx");
            await _service.GenerateLodAsync(rulesKey, simplygonFbx, temporaryOutputFilename,
                    progress.SubrangeDouble(0d, 0.98), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var result = await Task.Run(() => LodFbxToKn5(originalKn5, temporaryOutputFilename)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(0.99);
            return result;
        }

        private async Task<string> PrepareSimplygonFbxAsync(IKn5 kn5, string tmpPostfix) {
            var temporaryFilename = Path.Combine(_temporaryDirectory, $"in_{tmpPostfix}.fbx");
            await Task.Run(() => kn5.ExportFbx(temporaryFilename));
            return temporaryFilename;
        }

        private async Task GenerateLodStageAsync(int lodIndex, string rulesKey, string outputFilename,
                IProgress<double?> progress, CancellationToken cancellationToken) {
            var mergeRules = new CarLodGeneratorMergeRules(_carDirectory, _carData, lodIndex) { MaterialsToIncreasePriority = BodyMaterials };
            if (lodIndex == 3) {
                mergeRules.MaterialsToNotJoin = BodyMaterials;
            }

            var preparedKn5 = await Task.Run(() => PrepareForGeneration(mergeRules, _originalKn5Data))
                    .ConfigureAwait(false);
            progress.Report(0.01);
            cancellationToken.ThrowIfCancellationRequested();

            if (lodIndex == 0) {
                var cockpitHr = preparedKn5.FirstByName("COCKPIT_HR");
                if (cockpitHr == null) throw new Exception("COCKPIT_HR node is missing");
                preparedKn5.RootNode.Children = new List<Kn5Node> { cockpitHr };
            }

            var preparedSimplygonFbx = await PrepareSimplygonFbxAsync(preparedKn5, $"{lodIndex}")
                    .ConfigureAwait(false);
            progress.Report(0.02);
            cancellationToken.ThrowIfCancellationRequested();

            var generated = await GenerateLodAsync(preparedSimplygonFbx, preparedKn5, rulesKey, $"{lodIndex}",
                    progress.SubrangeDouble(0.03, 0.98), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Run(() => {
                if (lodIndex == 0) {
                    var cockpitLr = generated.RootNode.Children[0].NodeClass == Kn5NodeClass.Mesh ? generated.RootNode : generated.RootNode.Children[0];
                    cockpitLr.Active = false;
                    cockpitLr.Name = "COCKPIT_LR";

                    generated = Kn5.FromBytes(_originalKn5Data);
                    foreach (var node in generated.Nodes.ToList()) {
                        node.Children.Remove(node.Children.FirstOrDefault(c => c.Name == "COCKPIT_LR"));
                    }
                    generated.RootNode.Children.Add(cockpitLr);
                    cancellationToken.ThrowIfCancellationRequested();
                    progress.Report(0.99);
                }

                generated.Save(outputFilename);
                progress.Report(1d);
                AcToolsLogging.Write($"Ready: {Path.GetFileName(outputFilename)}");
            }).ConfigureAwait(false);
        }
    }
}