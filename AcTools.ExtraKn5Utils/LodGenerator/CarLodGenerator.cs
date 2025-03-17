using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        [CanBeNull]
        private readonly byte[] _originalKn5Uv2Data;

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

            if (Stages.Any(x => x.ConvertUv2?.Length > 0)
                    && File.Exists(FileUtils.ReplaceExtension(modelOriginal, ".uv2"))) {
                _originalKn5Uv2Data = File.ReadAllBytes(Regex.Replace(modelOriginal, @"\.\w+$", ".uv2"));
            }
        }

        public Task RunAsync(ICarLodGeneratorToolParams toolParams, [CanBeNull] CarLodGeneratorExceptionCallback exceptionCallback, [CanBeNull] CarLodGeneratorResultCallback resultCallback,
                IProgress<CarLodGeneratorProgressUpdate> progress = null, CancellationToken cancellationToken = default) {
            return Stages.Select(async (x, i) => {
                try {
                    var generated = await GenerateLodStageAsync(i, toolParams, x, CreateProgress(x.Id), cancellationToken).ConfigureAwait(false);
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

            // GCHelper.CleanUp();
            var mesh = children[0].Item1;
            var priority = children[0].Item2;
            var considerDetails = mesh.MaterialId != uint.MaxValue;
            var builder = new Kn5MeshBuilder(considerDetails, considerDetails);
            var useUv2 = children.Any(x => x.Item1.Uv2 != null && mergeRules.UseUv2(x.Item1));
            
#if DEBUG_
            AcToolsLogging.Write($"Merging together (UV2: {useUv2}): {children.Select(x => $"{x.Item1.Name} [{x.Item2}]").JoinToString(", ")}");
#endif

            var extraCounter = 0;
            foreach (var child in children) {
                var transform = child.Item3 * Mat4x4.CreateScale(new Vec3((float)priority)) * Mat4x4.CreateTranslation(MoveAsideDistance(priority, stage));
                var offset = mergeRules.GetOffsetAlongNormal(child.Item1);
                for (var i = 0; i < child.Item1.Indices.Length; ++i) {
                    builder.AddVertex(child.Item1.Vertices[child.Item1.Indices[i]].Transform(transform, offset), 
                            useUv2 ? child.Item1.Uv2?[child.Item1.Indices[i]] : null);
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

        private static void PrepareForGeneration(Kn5NodeFilterContext filterContext, CarLodGeneratorMergeRules mergeRules, 
                IKn5 kn5, CarLodGeneratorStageParams stage, bool printNodes) {
            var toMerge = new Dictionary<Kn5Node, List<Tuple<Kn5Node, double, Mat4x4>>>();
            var nodeIndices = new Dictionary<Kn5Node, int>();

            MergeNode(kn5.RootNode, kn5.RootNode, 1d);
            foreach (var pair in toMerge) {
                var mergeData = pair.Value.GroupBy(x => mergeRules.MergeGroup(x.Item1, x.Item2))
                        .OrderBy(v => mergeRules.GroupOrder(kn5, v, nodeIndices)).Select(v => v.ToList()).ToList();
                foreach (var group in mergeData) {
                    MergeMeshes(pair.Key, group, mergeRules, stage);
                }
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

        private struct VertexRef {
            public int I;
            public int VertexI;
            public int PolygonI;
            public int PolygonVertexI;
        }

        private enum MappingInformationType {
            ByIndex,
            ByVertex,
            ByPolygon,
            ByPolygonVertex,
            AllSame,
        }

        private class GenDataAccessor {
            private float[] _data;
            private int[] _indices;
            private readonly MappingInformationType _mappingType;

            private FbxNode _layer;
            
            public GenDataAccessor(FbxNode geometry, string layerKey, string typeKey, string indexKey) {
                var layer = geometry.GetRelative(layerKey);
                if (layer == null) {
                    return;
                }
                var mappingType = layer.GetRelative("MappingInformationType").Value.GetAsString();
                var referenceType = layer.GetRelative("ReferenceInformationType").Value.GetAsString();
                _data = layer.GetRelative(typeKey).Value.GetAsFloatArray();
                _indices = referenceType == "IndexToDirect" ? layer.GetRelative(indexKey).Value.GetAsIntArray() : null;
                _mappingType = mappingType == "ByVertex" || mappingType == "ByVertice" ? MappingInformationType.ByVertex 
                        : mappingType == "ByPolygonVertex" ? MappingInformationType.ByPolygonVertex 
                        : mappingType == "ByPolygon" ? MappingInformationType.ByPolygon 
                        : mappingType == "AllSame" ? MappingInformationType.AllSame 
                        : MappingInformationType.ByIndex;
                // AcToolsLogging.Write($"MappingType:{mappingType}, ReferenceType:{referenceType}");
                _layer = layer;
            }

            private int GetDataIndex(VertexRef vertexRef) {
                if (_data == null) return -1;
                var i = vertexRef.I;
                switch (_mappingType) {
                    case MappingInformationType.ByVertex:
                        i = vertexRef.VertexI;
                        break;
                    case MappingInformationType.ByPolygonVertex:
                        i = vertexRef.PolygonVertexI;
                        break;
                    case MappingInformationType.ByPolygon:
                        i = vertexRef.PolygonI;
                        break;
                    case MappingInformationType.AllSame:
                        i = 0;
                        break;
                }
                if (_indices != null) {
                    if (i >= _indices.Length) return -1;
                    i = _indices[i];
                }
                return i;
            }

            public Vec2 GetUv(VertexRef vertexRef) {
                var i = GetDataIndex(vertexRef);
                if (i < 0) return Vec2.Zero;
                return new Vec2(_data[i * 2], 1f - _data[i * 2 + 1]);
            }

            public Vec3 GetVec3(VertexRef vertexRef) {
                var i = GetDataIndex(vertexRef);
                if (i < 0) return new Vec3(0f, 1f, 0f);
                return new Vec3(_data[i * 3], _data[i * 3 + 1], _data[i * 3 + 2]);
            }

            public Vec4 GetVec4(VertexRef vertexRef) {
                var i = GetDataIndex(vertexRef);
                if (i < 0) return Vec4.Zero;
                return new Vec4(_data[i * 4], _data[i * 4 + 1], _data[i * 4 + 2], _data[i * 4 + 3]);
            }

            public int GetDataLength() {
                return _data?.Length ?? -1;
            }
        }

        private class GeometryInfo {
            public long Id;
            public FbxDocument Document;
        }

        private static void LodFbxToKn5(IKn5 preparedKn5, IEnumerable<Tuple<string, string>> fbxFilenames, CarLodGeneratorStageParams stage) {
            Dictionary<string, List<GeometryInfo>> geometries = new Dictionary<string, List<GeometryInfo>>();
            foreach (var fbxFilename in fbxFilenames) {
                var fbx = FbxIO.Read(fbxFilename.Item1);
                foreach (var i in fbx.GetGeometryIds()
                        .Select(id => new { id, name = fbxFilename.Item2 ?? fbx.GetNode("Model", fbx.GetConnection(id)).GetName("Model") })
                        .GroupBy(v => v.name)) {
                    geometries.GetValueOrSet(i.Key, () => new List<GeometryInfo>()).AddRange(i.Select(x => new GeometryInfo{Id = x.id, Document = fbx}));
                }
            }
            MergeNode(preparedKn5.RootNode);
            foreach (var geometry in geometries.Where(g => g.Value.Count > 0)) {
                var key = geometry.Key;
                var parent = preparedKn5.FirstByName(key);
                if (parent == null) {
                    AcToolsLogging.Write($"Error: parent {key} is missing");
                    continue;
                }
                if (parent.Children.Count == 0) {
                    AcToolsLogging.Write($"Error: parent {key} ({parent.Name}) is empty");
                    continue;
                }

                var mesh = Kn5MeshUtils.Create($"{key}__mesh_", parent.Children[0].MaterialId);
                if (parent.Children.Count != 1) throw new Exception("Unexpected arrangement");
                MergeMeshWith(parent, mesh, geometry.Value.Select(x => Tuple.Create(x.Document.GetGeometry(x.Id), 1d)));
                parent.Children.Add(mesh);
            }
            RemoveEmpty(preparedKn5.RootNode);

            void MergeMeshWith(Kn5Node parent, Kn5Node mesh, IEnumerable<Tuple<FbxNode, double>> geometriesList) {
                var builder = new Kn5MeshBuilder();
                var subCounter = 1;
                foreach (var geometry in geometriesList) {
                    var fbxIndices = geometry?.Item1?.GetRelative("PolygonVertexIndex")?.Value?.GetAsIntArray();
                    if (fbxIndices == null) {
                        continue;
                    }

                    var fbxVertices = geometry.Item1.GetRelative("Vertices").Value.GetAsFloatArray();
                    var fbxNormals = new GenDataAccessor(geometry.Item1, 
                            "LayerElementNormal", "Normals", "NormalsIndex");
                    var fbxUv0 = new GenDataAccessor(geometry.Item1, 
                            "LayerElementUV", "UV", "UVIndex");
                    
                    GenDataAccessor fbxUv1;
                    try {
                        fbxUv1 = mesh.Uv2 != null ? new GenDataAccessor(geometry.Item1,
                                "LayerElementColor", "Colors", "ColorIndex") : null;
                    } catch {
                        fbxUv1 = null;
                    }

                    var offset = MoveAsideDistance(geometry.Item2, stage);
                    var scale = (float)(1d / geometry.Item2);
                    void AddVertex(VertexRef r) {
                        var uv2 = fbxUv1?.GetVec4(r);
                        builder.AddVertex(new Kn5Node.Vertex {
                            Position = new Vec3(
                                    (fbxVertices[r.VertexI * 3] - offset.X) * scale,
                                    (fbxVertices[r.VertexI * 3 + 1] - offset.Y) * scale,
                                    (fbxVertices[r.VertexI * 3 + 2] - offset.Z) * scale),
                            Normal = fbxNormals.GetVec3(r),
                            Tex = fbxUv0.GetUv(r)
                        }, uv2.HasValue ? new Vec2(uv2.Value.X, uv2.Value.Y) : (Vec2?)null);
                    }
                    
                    var collected = new List<VertexRef>();
                    var polygonIndex = 0;
                    for (var i = 0; i < fbxIndices.Length; ++i) {
                        var v = fbxIndices[i];
                        collected.Add(new VertexRef {
                            I = i, 
                            VertexI = v < 0 ? -v - 1 : v,
                            PolygonVertexI = i,
                            PolygonI = polygonIndex
                        });
                        if (v < 0) {
                            if (builder.IsCloseToLimit) {
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

                            for (var j = 1; j < collected.Count - 1; j++) {
                                AddVertex(collected[0]);
                                AddVertex(collected[j]);
                                AddVertex(collected[j + 1]); 
                            }
                            collected.Clear();
                            ++polygonIndex;
                        }
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
                    return Tuple.Create(geometryId.Document.GetGeometry(geometryId.Id), node.Tag is double priority ? priority : 1d);
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

        private async Task<IKn5> GenerateLodAsync(string temporaryFilenamePrefix, CarLodGeneratorMergeRules mergeRules, IKn5 kn5, 
                ICarLodGeneratorToolParams toolParams, CarLodGeneratorStageParams stage, IProgress<double?> progress, CancellationToken cancellationToken) {
            GCHelper.CleanUp();
            string temporaryOutputFilename;
            if (toolParams.SplitPriorities) {
                var grouped = kn5.RootNode.AllChildren().Where(x => x.NodeClass == Kn5NodeClass.Mesh)
                        .GroupBy(mergeRules.CalculateReductionPriority)
                        .Select(x => {
                            var items = x.ToList();
                            return new { Priority = x.Key, Triangles = items.Sum(y => y.TotalTrianglesCount), Items = items };
                        }).ToList();
                if (grouped.Count == 1) {
                    // TODO: Switch to a single priority route
                }

                var rootDump = kn5.RootNode.AllChildren().Where(x => x.NodeClass == Kn5NodeClass.Base)
                        .ToDictionary(x => x, x => x.Children.ToList());
                var outputs = new List<Tuple<string, string>>();
                var adjustedCount = grouped.Sum(x => x.Triangles * x.Priority);

                for (var groupIndex = 0; groupIndex < grouped.Count; groupIndex++) {
                    var group = grouped[groupIndex];
                    var subProgress = progress.SubrangeDouble(0.99 * groupIndex / grouped.Count, 0.99 * (groupIndex + 1d) / grouped.Count);
                    AcToolsLogging.Write(
                            $"Processing priority: {group.Priority}, meshes: {group.Items.Count} ({group.Items.Select(x => x.Name).JoinToString("; ")})");
                    var preparedOriginFilename = FileUtils.EnsureUnique($"{temporaryFilenamePrefix}_in_{group.Priority}.{(toolParams.UseFbx ? "fbx" : "dae")}");
                    try {
                        foreach (var node in kn5.RootNode.AllChildren().Where(x => x.NodeClass == Kn5NodeClass.Base)) {
                            node.Children = rootDump[node].Where(x => x.NodeClass == Kn5NodeClass.Base || group.Items.Contains(x)).ToList();
                        }
                        kn5.Refresh();
                        
                        await Task.Run(() => {
                            if (toolParams.UseFbx) {
                                kn5.ExportFbx(preparedOriginFilename);
                            } else {
                                kn5.ExportCollada(preparedOriginFilename);
                            }
                        });
                        subProgress.Report(0.1);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (File.Exists(preparedOriginFilename)) {
                            var checksum = await CalculateChecksumAsync(kn5);
                            subProgress.Report(0.2);
                            cancellationToken.ThrowIfCancellationRequested();

                            temporaryOutputFilename = await _service.GenerateLodAsync(stage.Id, preparedOriginFilename,
                                    (int)(adjustedCount / group.Priority), checksum,
                                    kn5.Nodes.Any(x => x.Uv2 != null),
                                    subProgress.SubrangeDouble(0.3, 0.9), cancellationToken).ConfigureAwait(false);
                            cancellationToken.ThrowIfCancellationRequested();
                            subProgress.Report(0.95);

                            if (temporaryOutputFilename != string.Empty) {
                                outputs.Add(Tuple.Create(temporaryOutputFilename, group.Items.Count == 1 ? group.Items[0].Name : null));
                            }
                        }
                    } finally {
                        if (!stage.KeepTemporaryFiles) {
                            FileUtils.TryToDelete(preparedOriginFilename);
                        }
                    }
                }

                foreach (var node in kn5.RootNode.AllChildren().Where(x => x.NodeClass == Kn5NodeClass.Base)) {
                    node.Children = rootDump[node];
                }
                kn5.Refresh();
                
                await Task.Run(() => LodFbxToKn5(kn5, outputs, stage)).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                
            } else {
                var preparedOriginFilename = FileUtils.EnsureUnique($"{temporaryFilenamePrefix}_in.{(toolParams.UseFbx ? "fbx" : "dae")}");
                try {
                    await Task.Run(() => {
                        if (toolParams.UseFbx) {
                            kn5.ExportFbx(preparedOriginFilename);
                        } else {
                            kn5.ExportCollada(preparedOriginFilename);
                        }
                    });
                    progress.Report(0.1);
                    cancellationToken.ThrowIfCancellationRequested();

                    var checksum = await CalculateChecksumAsync(kn5);
                    progress.Report(0.2);
                    cancellationToken.ThrowIfCancellationRequested();

                    temporaryOutputFilename = await _service.GenerateLodAsync(stage.Id, preparedOriginFilename, kn5.RootNode.TotalTrianglesCount, checksum,
                            kn5.Nodes.Any(x => x.Uv2 != null),
                            progress.SubrangeDouble(0.3, 0.98), cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.Empty == temporaryOutputFilename) {
                        throw new Exception("Generated file is empty");
                    }
                    await Task.Run(() => LodFbxToKn5(kn5, new[] { Tuple.Create(temporaryOutputFilename, (string)null) }, stage)).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    progress.Report(0.99);
                } finally {
                    if (!stage.KeepTemporaryFiles) {
                        FileUtils.TryToDelete(preparedOriginFilename);
                    }
                }
            }
            
            return kn5;
        }

        private static string CalculateChecksum(IKn5 kn5) {
            // GCHelper.CleanUp();
            string ret;
            using (var sha1 = SHA1.Create()) {
                ret = sha1.ComputeHash(kn5.ToArray()).ToHexString();
            }
            var uv2 = kn5.Nodes.Where(x => x.Uv2 != null).ToArray();
            if (uv2.Length > 0) {
                using (var sha1 = SHA1.Create()) {
                    ret += sha1.ComputeHash(kn5.ExportUv2Data()).ToHexString();
                }
                using (var sha1 = SHA1.Create()) {
                    ret = sha1.ComputeHash(Encoding.ASCII.GetBytes(ret)).ToHexString();
                }
            }
            return ret;
        }

        private static Task<string> CalculateChecksumAsync(IKn5 kn5) {
            return Task.Run(() => CalculateChecksum(kn5));
        }

        private Task<IKn5> LoadModelAsync() {
            return Task.Run(() => {
                var ret = Kn5.FromBytes(_originalKn5Data, SkippingTextureLoader.Instance);
                if (_originalKn5Uv2Data != null) ret.LoadUv2(_originalKn5Uv2Data);
                return ret;
            });
        }

        public async Task SaveInputModelAsync(CarLodGeneratorStageParams stage, string filename) {
            var kn5 = await LoadModelAsync();
            var filterContext = new Kn5NodeFilterContext(stage.DefinitionsData, stage.UserDefined, _carDirectory, _carData, kn5);
            var mergeRules = new CarLodGeneratorMergeRules(filterContext, stage);
            await Task.Run(() => PrepareForGeneration(filterContext, mergeRules, kn5, stage, true));

            if (stage.InlineGeneration?.Source != null) {
                var inlineHr = kn5.Nodes.FirstOrDefault(filterContext.CreateFilter(stage.InlineGeneration.Source).Test);
                if (inlineHr == null) throw new Exception($"{stage.InlineGeneration.Source} node is missing");
                kn5.RootNode.Children = new List<Kn5Node> { inlineHr };
            }

            kn5.Save(filename);
        }

        [ItemCanBeNull]
        private async Task<Tuple<string, string>> GenerateLodStageAsync(int index, ICarLodGeneratorToolParams toolParams, CarLodGeneratorStageParams stage,
                IProgress<double?> progress, CancellationToken cancellationToken) {
            var kn5 = await LoadModelAsync();
            progress.Report(0.01);
            
            var filterContext = new Kn5NodeFilterContext(stage.DefinitionsData, stage.UserDefined, _carDirectory, _carData, kn5);
            var mergeRules = new CarLodGeneratorMergeRules(filterContext, stage);
            // AcToolsLogging.Write("UV2 nodes 0: " + kn5.Nodes.Where(x => x.Uv2 != null).Select(x => x.Name).JoinToString("; "));
            await Task.Run(() => PrepareForGeneration(filterContext, mergeRules, kn5, stage, false));
            // AcToolsLogging.Write("UV2 nodes 1: " + kn5.Nodes.Where(x => x.Uv2 != null).Select(x => x.Name).JoinToString("; "));

            progress.Report(0.02);
            cancellationToken.ThrowIfCancellationRequested();

            if (stage.InlineGeneration?.Source != null) {
                var inlineHr = kn5.Nodes.FirstOrDefault(filterContext.CreateFilter(stage.InlineGeneration.Source).Test);
                if (inlineHr == null) throw new Exception($"{stage.InlineGeneration.Source} node is missing");
                kn5.RootNode.Children = new List<Kn5Node> { inlineHr };
            }

            var temporaryFilenamePrefix = Path.Combine(_temporaryDirectory, $"{Path.GetFileName(_carDirectory)}_{index}");
            var generated = await GenerateLodAsync(temporaryFilenamePrefix, mergeRules, kn5, toolParams, stage,
                    progress.SubrangeDouble(0, 0.98), cancellationToken);
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
                FileUtils.TryToDelete(FileUtils.ReplaceExtension(resultFilename, ".uv2"));
                generated.SaveUv2(FileUtils.ReplaceExtension(resultFilename, ".uv2"), true);
                generated.RemoveUnusedMaterials();
                progress.Report(1d);
                return Tuple.Create(resultFilename, CalculateChecksum(generated));
            });
        }
    }
}