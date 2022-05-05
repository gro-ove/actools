using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.ExtraKn5Utils.FbxUtils;
using AcTools.ExtraKn5Utils.FbxUtils.Extensions;
using AcTools.ExtraKn5Utils.Kn5Utils;
using AcTools.Kn5File;
using AcTools.Numerics;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public class CarLodGenerator {
        private static Vec3 MoveAsideDistance(double priority, CarLodGeneratorStageParams stage) {
            if (priority == 1d || !stage.SeparatePriorityGroups) return Vec3.Zero;
            return new Vec3((float)(priority - 1f) * 0.5f, (float)((priority - 1f) * 7f % 0.2f - 0.1f), (float)((priority - 1f) * 11f % 0.5f - 0.25f));
        }

        private readonly ICarLodGeneratorService _service;
        private readonly string _carDirectory;
        private readonly string _temporaryDirectory;
        private readonly DataWrapper _carData;
        private readonly byte[] _originalKn5Data;

        public CarLodGeneratorStageParams[] Stages { get; }

        public CarLodGenerator(IEnumerable<CarLodGeneratorStageParams> stages, ICarLodGeneratorService service, string carDirectory, string temporaryDirectory) {
            _service = service;
            _carDirectory = carDirectory;
            _temporaryDirectory = temporaryDirectory;
            _carData = DataWrapper.FromCarDirectory(carDirectory);

            var modelOriginal = AcPaths.GetMainCarFilename(carDirectory, _carData, false);
            if (modelOriginal == null) {
                throw new Exception("Failed to find main car model");
            }

            Stages = stages.ToArray();
            _originalKn5Data = File.ReadAllBytes(modelOriginal);
        }

        public Task RunAsync([CanBeNull] CarLodGeneratorExceptionCallback exceptionCallback, [CanBeNull] CarLodGeneratorResultCallback resultCallback,
                IProgress<CarLodGeneratorProgressUpdate> progress = null, CancellationToken cancellationToken = default) {
            return Stages.Select(async (x, i) => {
                try {
                    var generated = await GenerateLodStageAsync(i, x, CreateProgress(x.Id), cancellationToken).ConfigureAwait(false);
                    if (generated != null) {
                        resultCallback?.Invoke(x.Id, generated.Item1, generated.Item2);
                    }
                } catch (Exception e) when (exceptionCallback != null) {
                    if (!e.IsCancelled()) {
                        AcToolsLogging.Write(e);
                        exceptionCallback.Invoke(x.Id, e);
                    }
                }
            }).WhenAll();

            IProgress<double?> CreateProgress(string key) {
                return progress == null ? null : new Progress<double?>(v => progress.Report(new CarLodGeneratorProgressUpdate {
                    Key = key,
                    Value = v
                }));
            }
        }

        private static void MergeMeshes(Kn5Node root, List<Tuple<Kn5Node, double, Mat4x4>> children, CarLodGeneratorMergeRules mergeRules,
                CarLodGeneratorStageParams stage) {
            if (children.Count == 0) return;

            var mesh = children[0].Item1;
            var priority = children[0].Item2;
            var considerDetails = mesh.MaterialId != uint.MaxValue;
            var builder = new Kn5MeshBuilder(considerDetails, considerDetails);
            AcToolsLogging.Write($"Merging together: {children.Select(x => $"{x.Item1.Name} [{x.Item2}]").JoinToString(", ")}");

            var extraCounter = 0;
            foreach (var child in children) {
                var transform = child.Item3 * Mat4x4.CreateScale(new Vec3((float)priority)) * Mat4x4.CreateTranslation(MoveAsideDistance(priority, stage));
                var offset = mergeRules.GetOffsetAlongNormal(child.Item1);
                for (var i = 0; i < child.Item1.Indices.Length; ++i) {
                    builder.AddVertex(child.Item1.Vertices[child.Item1.Indices[i]].Transform(transform, offset));
                    if (i % 3 == 2 && builder.IsCloseToLimit) {
                        builder.SetTo(mesh);
                        root.Children.Add(mesh);
                        mesh.Tag = priority;

                        builder.Clear();
                        mesh = Kn5MeshUtils.Create(children[0].Item1.Name + $"___$extra:{extraCounter}", children[0].Item1.MaterialId);
                        ++extraCounter;
                    }
                }
            }

            if (builder.Count > 0) {
                builder.SetTo(mesh);
                root.Children.Add(mesh);
                mesh.Tag = priority;
            }
        }

        private static async Task PrepareForGenerationAsync(Kn5NodeFilterContext filterContext, IKn5 kn5, CarLodGeneratorStageParams stage, bool printNodes) {
            var mergeRules = new CarLodGeneratorMergeRules(filterContext, stage);
            var toMerge = new Dictionary<Kn5Node, List<Tuple<Kn5Node, double, Mat4x4>>>();
            var nodeIndices = new Dictionary<Kn5Node, int>();
            MergeNode(kn5.RootNode, kn5.RootNode, 1d);
            foreach (var pair in toMerge) {
                var mergeData = pair.Value.GroupBy(x => mergeRules.MergeGroup(x.Item1, x.Item2))
                        .OrderBy(v => mergeRules.GroupOrder(kn5, v, nodeIndices)).Select(v => v.ToList()).ToList();
                await Task.Run(() => {
                    foreach (var group in mergeData) {
                        MergeMeshes(pair.Key, group, mergeRules, stage);
                    }
                });
            }
            foreach (var node in kn5.Nodes.Where(x => x.Children?.Count > 1).ToList()) {
                var meshesList = node.Children
                        .Where(x => x.NodeClass != Kn5NodeClass.Base)
                        .Select((x, i) => new { x, i = OrderIndex(x, i) })
                        .OrderBy(x => x.i)
                        .Select(x => x.x).ToList();
                if (meshesList.Count > 0) {
                    if (node.Children.Any(x => x.NodeClass == Kn5NodeClass.Base)) {
                        node.Children = node.Children.Where(x => x.NodeClass == Kn5NodeClass.Base)
                                .Prepend(Kn5Node.CreateBaseNode($"__{meshesList[0].Name}_wrap_", meshesList, true))
                                .Select((x, i) => new { x, i = OrderIndex(x, i) })
                                .OrderBy(x => x.i)
                                .Select(x => x.x).ToList();
                    } else {
                        node.Children = meshesList;
                    }
                }
            }
            if (printNodes) {
                PrintNode(kn5.RootNode, 0, 0);
            }
            mergeRules.FinalizeKn5(kn5);

            var duplicateNames = kn5.Nodes.GroupBy(x => $"{x.NodeClass}/{x.Name}")
                    .Select(x => x.ToList()).Where(x => x.Count > 1).ToList();
            foreach (var group in duplicateNames) {
                AcToolsLogging.Write($"Duplicate name: {group[0].Name} ({group[0].NodeClass})");
                foreach (var toRename in group.Skip(1).Select((x, i) => new { x, i })) {
                    toRename.x.Name = $"{toRename.x.Name}___$unique:{toRename.i}";
                }
            }

            int OrderIndex(Kn5Node node, int index) {
                return index + (AnyTransparent(node) ? 1 << 10 : 0);
            }

            void PrintNode(Kn5Node node, int level, int index) {
                var postfix = node.NodeClass == Kn5NodeClass.Base ? "" :
                        $"{(node.IsTransparent ? ", transparent" : "")}, material: {kn5.GetMaterial(node.MaterialId)?.Name}";
                AcToolsLogging.Write(
                        $"{new string('\t', level)}{node.Name} [{node.NodeClass}{postfix}, index: {OrderIndex(node, index)}, aabb: {filterContext.GetAabb3(node)}]");
                if (node.NodeClass == Kn5NodeClass.Base) {
                    for (var i = 0; i < node.Children.Count; i++) {
                        PrintNode(node.Children[i], level + 1, i);
                    }
                }
            }

            bool AnyTransparent(Kn5Node node) {
                return node.NodeClass == Kn5NodeClass.Base
                        ? node.Children.Any(AnyTransparent)
                        : node.IsTransparent || kn5.GetMaterial(node.MaterialId)?.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
            }

            void ApplyPriority(Kn5Node mesh, double priority) {
                var offset = MoveAsideDistance(priority, stage);
                for (var i = 0; i < mesh.Vertices.Length; i++) {
                    Apply(ref mesh.Vertices[i]);
                }
                mesh.Tag = priority;

                void Apply(ref Kn5Node.Vertex v) {
                    v.Position[0] = v.Position[0] * (float)priority + offset.X;
                    v.Position[1] = v.Position[1] * (float)priority + offset.Y;
                    v.Position[2] = v.Position[2] * (float)priority + offset.Z;
                }
            }

            bool MergeNode(Kn5Node node, Kn5Node mergeRoot, double priority) {
                nodeIndices[node] = nodeIndices.Count;

                if (node != mergeRoot) {
                    if (mergeRules.CanSkipNode(node)) {
                        if (node.NodeClass == Kn5NodeClass.Base && !mergeRules.HasParentWithSameName(node) && !mergeRules.CanRemoveEmptyNode(node)) {
                            node.Children.Clear();
                            return true;
                        }
                        return false;
                    }

                    var priorityAdjustment = mergeRules.CalculateReductionPriority(node);
                    if (priorityAdjustment != 1d && (priorityAdjustment < priority || priority == 1d || node.NodeClass == Kn5NodeClass.Mesh)) {
                        priority = priorityAdjustment;
                    }

                    if (node.NodeClass == Kn5NodeClass.Mesh) {
                        if (mergeRoot != null && mergeRules.CanMerge(node)) {
                            if (!toMerge.ContainsKey(mergeRoot)) {
                                toMerge[mergeRoot] = new List<Tuple<Kn5Node, double, Mat4x4>>();
                            }
                            toMerge[mergeRoot].Add(Tuple.Create(node, priority, node.CalculateTransformRelativeToParent(mergeRoot)));
                            return false;
                        }
                        if (priority != 1d) {
                            ApplyPriority(node, priority);
                        }
                        return true;
                    }

                    if (node.NodeClass == Kn5NodeClass.SkinnedMesh) {
                        return true;
                    }

                    if (mergeRoot != null) {
                        if (!mergeRules.CanMerge(node)) {
                            mergeRoot = null;
                        } else if (node.Name != mergeRoot.Name && mergeRules.IsNodeMergeRoot(node)) {
                            mergeRoot = node;
                        }
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

        private static IKn5 LodFbxToKn5(IKn5 preparedKn5, string fbxFilename, CarLodGeneratorStageParams stage) {
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
                MergeMeshWith(parent, mesh, geometry.Value.Select(x => Tuple.Create(fbx.GetGeometry(x), 1d)));
                parent.Children.Add(mesh);
            }
            RemoveEmpty(preparedKn5.RootNode);
            return preparedKn5;

            void MergeMeshWith(Kn5Node parent, Kn5Node mesh, IEnumerable<Tuple<FbxNode, double>> geometriesList) {
                var builder = new Kn5MeshBuilder();
                var subCounter = 1;
                foreach (var geometry in geometriesList) {
                    var fbxIndices = geometry?.Item1?.GetRelative("PolygonVertexIndex")?.Value?.GetAsIntArray();
                    if (fbxIndices == null) {
                        continue;
                    }

                    var fbxVertices = geometry.Item1.GetRelative("Vertices").Value.GetAsFloatArray();

                    var layerNormal = geometry.Item1.GetRelative("LayerElementNormal");
                    var fbxNormals = layerNormal.GetRelative("Normals").Value.GetAsFloatArray();
                    var fbxNormalsMappingType = layerNormal.GetRelative("MappingInformationType").Value.GetAsString();
                    var fbxNormalsReferenceType = layerNormal.GetRelative("ReferenceInformationType").Value.GetAsString();
                    var fbxNormalsIndex = fbxNormalsReferenceType == "IndexToDirect" ? layerNormal.GetRelative("NormalsIndex").Value.GetAsIntArray() : null;

                    Vec3 GetNormal(int i, int j) {
                        if (fbxNormalsMappingType == "ByVertex") i = j;
                        if (fbxNormalsIndex != null) i = fbxNormalsIndex[i];
                        return new Vec3(fbxNormals[i * 3], fbxNormals[i * 3 + 1], fbxNormals[i * 3 + 2]);
                    }

                    var layerUV = geometry.Item1.GetRelative("LayerElementUV");
                    var fbxUvs = layerUV.GetRelative("UV").Value.GetAsFloatArray();
                    var fbxUvsMappingType = layerUV.GetRelative("MappingInformationType").Value.GetAsString();
                    var fbxUvsReferenceType = layerUV.GetRelative("ReferenceInformationType").Value.GetAsString();
                    var fbxUvIndex = fbxUvsReferenceType == "IndexToDirect" ? layerUV.GetRelative("UVIndex").Value.GetAsIntArray() : null;

                    Vec2 GetUV(int i, int j) {
                        if (fbxUvsMappingType == "ByVertex") i = j;
                        if (fbxUvIndex != null) i = fbxUvIndex[i];
                        return new Vec2(fbxUvs[i * 2], 1f - fbxUvs[i * 2 + 1]);
                    }

                    var offset = MoveAsideDistance(geometry.Item2, stage);
                    var scale = (float)(1d / geometry.Item2);

                    for (var i = 0; i < fbxIndices.Length; ++i) {
                        if (i % 3 == 0 && builder.IsCloseToLimit) {
                            builder.SetTo(mesh);
                            mesh.RecalculateTangents();

                            builder.Clear();
                            var oldIndex = parent.Children.IndexOf(mesh);
                            mesh = Kn5MeshUtils.Create(mesh.Name + $"___$sub:{subCounter}", mesh.MaterialId);
                            ++subCounter;
                            if (oldIndex != -1 && oldIndex < parent.Children.Count - 1) {
                                parent.Children.Insert(oldIndex + 1, mesh);
                            } else {
                                parent.Children.Add(mesh);
                            }
                        }

                        var index = fbxIndices[i] < 0 ? -fbxIndices[i] - 1 : fbxIndices[i];
                        builder.AddVertex(new Kn5Node.Vertex {
                            Position = new Vec3(
                                    (fbxVertices[index * 3] - offset.X) * scale,
                                    (fbxVertices[index * 3 + 1] - offset.Y) * scale,
                                    (fbxVertices[index * 3 + 2] - offset.Z) * scale),
                            Normal = GetNormal(i, index),
                            Tex = GetUV(i, index)
                        });
                    }
                }
                builder.SetTo(mesh);
                mesh.RecalculateTangents();
            }

            void MergeMesh(Kn5Node parent, Kn5Node node, IEnumerable<Kn5Node> merges) {
                if (Regex.IsMatch(node.Name, @"___\$extra:\d+$")) {
                    node.Vertices = new Kn5Node.Vertex[0];
                    return;
                }

                MergeMeshWith(parent, node, Enumerable.Range(-1, 100).Select(i => GetGeometry(node, i)).TakeWhile(i => i != null)
                        .Concat(merges.Select(n => GetGeometry(n))));
            }

            Tuple<FbxNode, double> GetGeometry(Kn5Node node, int extraBit = -1) {
                var name = extraBit < 0 ? node.Name : $"{node.Name}___$extra:{extraBit}";
                if (geometries.TryGetValue(name, out var list)) {
                    if (list.Count == 0) return null;
                    var geometryId = list[0];
                    list.RemoveAt(0);
                    return Tuple.Create(fbx.GetGeometry(geometryId), node.Tag is double priority ? priority : 1d);
                }
                return null;
            }

            void MergeNode(Kn5Node node) {
                foreach (var child in node.Children.ToList()) {
                    if (child.NodeClass == Kn5NodeClass.Base) {
                        MergeNode(child);
                    } else if (child.NodeClass == Kn5NodeClass.Mesh && child.Vertices.Length > 0) {
                        var mergeKey = MergeKey(child);
                        var merge = node.Children.ApartFrom(child).Where(c => c.NodeClass == Kn5NodeClass.Mesh && MergeKey(c) == mergeKey).ToList();
                        MergeMesh(node, child, merge);
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
                var found = node.Name.IndexOf("___$unique:", StringComparison.Ordinal);
                if (found != -1) {
                    node.Name = node.Name.Substring(0, found);
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

        private async Task<IKn5> GenerateLodAsync(string preparedFbx, IKn5 originalKn5, CarLodGeneratorStageParams stage, string modelChecksum,
                IProgress<double?> progress, CancellationToken cancellationToken) {
            var temporaryOutputFilename = await _service.GenerateLodAsync(stage.Id, preparedFbx, modelChecksum,
                    progress.SubrangeDouble(0d, 0.98), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var result = await Task.Run(() => LodFbxToKn5(originalKn5, temporaryOutputFilename, stage)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(0.99);
            return result;
        }

        private static string CalculateChecksum(IKn5 kn5) {
            using (var sha1 = SHA1.Create()) {
                return sha1.ComputeHash(kn5.ToArray()).ToHexString();
            }
        }

        private static Task<string> CalculateChecksumAsync(IKn5 kn5) {
            return Task.Run(() => CalculateChecksum(kn5));
        }

        public async Task SaveInputModelAsync(CarLodGeneratorStageParams stage, string filename) {
            var kn5 = Kn5.FromBytes(_originalKn5Data, SkippingTextureLoader.Instance);
            var filterContext = new Kn5NodeFilterContext(stage.DefinitionsData, stage.UserDefined, _carDirectory, _carData, kn5);
            await PrepareForGenerationAsync(filterContext, kn5, stage, true);

            if (stage.InlineGeneration?.Source != null) {
                var inlineHr = kn5.Nodes.FirstOrDefault(filterContext.CreateFilter(stage.InlineGeneration.Source).Test);
                if (inlineHr == null) throw new Exception($"{stage.InlineGeneration.Source} node is missing");
                kn5.RootNode.Children = new List<Kn5Node> { inlineHr };
            }

            kn5.Save(filename);
        }

        [ItemCanBeNull]
        private async Task<Tuple<string, string>> GenerateLodStageAsync(int index, CarLodGeneratorStageParams stage,
                IProgress<double?> progress, CancellationToken cancellationToken) {
            var kn5 = await Task.Run(() => Kn5.FromBytes(_originalKn5Data, SkippingTextureLoader.Instance));
            progress.Report(0.01);

            var filterContext = new Kn5NodeFilterContext(stage.DefinitionsData, stage.UserDefined, _carDirectory, _carData, kn5);
            await PrepareForGenerationAsync(filterContext, kn5, stage, false);

            progress.Report(0.02);
            cancellationToken.ThrowIfCancellationRequested();

            if (stage.InlineGeneration?.Source != null) {
                var inlineHr = kn5.Nodes.FirstOrDefault(filterContext.CreateFilter(stage.InlineGeneration.Source).Test);
                if (inlineHr == null) throw new Exception($"{stage.InlineGeneration.Source} node is missing");
                kn5.RootNode.Children = new List<Kn5Node> { inlineHr };
            }

            var temporaryFilenamePrefix = Path.Combine(_temporaryDirectory, $"{Path.GetFileName(_carDirectory)}_{index}");
            var preparedFbxFilename = FileUtils.EnsureUnique($"{temporaryFilenamePrefix}_in.fbx");
            try {
                await Task.Run(() => kn5.ExportFbx(preparedFbxFilename));
                progress.Report(0.03);
                cancellationToken.ThrowIfCancellationRequested();

                var checksum = await CalculateChecksumAsync(kn5);
                progress.Report(0.04);
                cancellationToken.ThrowIfCancellationRequested();

                var generated = await GenerateLodAsync(preparedFbxFilename, kn5, stage, checksum,
                        progress.SubrangeDouble(0.04, 0.98), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                return await Task.Run(() => {
                    if (stage.InlineGeneration?.Destination != null) {
                        var inlineLr = generated.RootNode.Children[0].NodeClass == Kn5NodeClass.Mesh ? generated.RootNode : generated.RootNode.Children[0];
                        inlineLr.Active = false;
                        inlineLr.Name = stage.InlineGeneration.Destination;

                        generated = Kn5.FromBytes(_originalKn5Data);
                        IterateChildren(generated.RootNode);
                        if (inlineLr != null) {
                            generated.RootNode.Children.Add(inlineLr);
                        }

                        void IterateChildren(Kn5Node node) {
                            if (stage.InlineGeneration == null) return;
                            for (var i = node.Children.Count - 1; i >= 0; i--) {
                                var child = node.Children[i];
                                if (child.Name == stage.InlineGeneration.Destination) {
                                    node.Children.RemoveAt(i);
                                } else if (inlineLr != null && child.Name == stage.InlineGeneration.Source) {
                                    node.Children.Insert(i + 1, inlineLr);
                                    inlineLr = null;
                                } else {
                                    IterateChildren(child);
                                }
                            }
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        progress.Report(0.99);
                    }

                    // This way it would kind of rename them all at once, allowing to swap names if necessary
                    stage.Rename?.Where(x => x.OldName != null).Select(x => new {
                        Nodes = generated.Nodes.Where(filterContext.CreateFilter(x.OldName ?? string.Empty).Test).ToList(),
                        x.NewName
                    }).Where(x => x.NewName != null).ToList()
                            .ForEach(x => x.Nodes.ForEach(y => y.Name = string.Format(x.NewName, y.Name)));

                    var resultFilename = FileUtils.EnsureUnique($"{temporaryFilenamePrefix}_out.kn5");
                    generated.Save(resultFilename);
                    generated.RemoveUnusedMaterials();
                    progress.Report(1d);
                    return Tuple.Create(resultFilename, CalculateChecksum(generated));
                });
            } finally {
                if (!stage.KeepTemporaryFiles) {
                    FileUtils.TryToDelete(preparedFbxFilename);
                }
            }
        }
    }
}