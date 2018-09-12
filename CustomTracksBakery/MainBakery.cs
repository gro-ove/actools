using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AcTools;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using CustomTracksBakery.Shaders;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using StringBasedFilter;
using StringBasedFilter.Parsing;
using StringBasedFilter.TestEntries;
using Device = SlimDX.Direct3D11.Device;
using Filter = StringBasedFilter.Filter;
using Half = SystemHalf.Half;

namespace CustomTracksBakery {
    public enum BakedMode {
        Tangent = 0,
        TangentLength = 1
    }

    public enum PatchEntryType {
        Tangent = 0,
        TangentLength = 1,
        Normal = 2,
        NormalLength = 3
    }

    public class BakedObjectFilters {
        [CanBeNull]
        public IFilter<BakedObject> Tree;

        [CanBeNull]
        public IFilter<BakedObject> Grass;

        [CanBeNull]
        public IFilter<BakedObject> RegularObjects;
    }

    public class BakedObject {
        public readonly Kn5 ObjectKn5;
        public readonly Kn5RenderableObject Object;
        public readonly BakedMode Mode;
        public readonly bool IsTree, IsGrass;

        public BakedObject(Kn5RenderableObject n, Kn5 kn5,
                [NotNull] BakedObjectFilters filters) {
            Object = n;
            ObjectKn5 = kn5;

            if (GetMaterial()?.GetMappingByName("txNormal") != null
                    || GetMaterial()?.GetMappingByName("txNormalDetail") != null
                    || GetMaterial()?.GetMappingByName("txDetailNM") != null) {
                Mode = BakedMode.TangentLength;
            } else {
                Mode = BakedMode.Tangent;
            }

            var isRegular = filters.RegularObjects?.Test(this) != true;
            IsTree = isRegular && filters.Tree?.Test(this) == true;
            IsGrass = isRegular && filters.Grass?.Test(this) == true;

            // GetShaderName() == "ksGrass";
        }

        [CanBeNull]
        public Kn5Material GetMaterial() {
            return ObjectKn5.GetMaterial(Object.OriginalNode.MaterialId);
        }

        [CanBeNull]
        public string GetShaderName() {
            return GetMaterial()?.ShaderName;
        }

        [CanBeNull]
        public string GetMaterialName() {
            return GetMaterial()?.Name;
        }
    }

    public class Kn5RenderableObjectTester : ITester<BakedObject> {
        public static readonly Kn5RenderableObjectTester Instance = new Kn5RenderableObjectTester();

        public string ParameterFromKey(string key) {
            return null;
        }

        public bool Test(BakedObject obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(obj.Object.OriginalNode.Name);
                case "shader":
                    return value.Test(obj.GetShaderName());
                case "material":
                    return value.Test(obj.GetMaterialName());
                default:
                    return false;
            }
        }
    }

    public static class Extensions {
        private static string PluralizeExt(int v, string s) {
            return v.ToInvariantString() + " " + (v == 1 ? s : s + "s");
        }

        public static string ToReadableTime(this TimeSpan span, bool considerMilliseconds = false) {
            var result = new List<string>();

            var days = (int)span.TotalDays;
            var months = days / 30;
            if (months > 30) {
                result.Add(PluralizeExt(months, "month"));
                days = days % 30;
            }

            if (days > 0) {
                result.Add(days % 7 == 0 ? PluralizeExt(days / 7, "week") : PluralizeExt(days, "day"));
            }

            if (span.Hours > 0 && months == 0) {
                result.Add(PluralizeExt(span.Hours, "hour"));
            }

            if (span.Minutes > 0 && months == 0) {
                result.Add(PluralizeExt(span.Minutes, "minute"));
            }

            if (span.Seconds > 0 && span.Hours == 0 && months == 0 && days == 0) {
                result.Add(PluralizeExt(span.Seconds, "second"));
            }

            if (considerMilliseconds && span.Milliseconds > 0 && result.Count == 0) {
                result.Add($@"{span.Milliseconds} ms");
            }

            return result.Count > 0 ? string.Join(@" ", result.Take(2)) : PluralizeExt(0, "second");
        }
    }

    public class MainBakery : UtilsRendererBase {
        private readonly string _filter;
        private readonly string _ignoreFilter;

        private readonly Kn5 _mainKn5;
        private readonly List<Kn5> _includeKn5 = new List<Kn5>();
        private readonly List<Kn5> _occludersKn5 = new List<Kn5>();

        private Kn5RenderableFile _mainNode;
        private List<Kn5RenderableFile> _includeNodeFiles = new List<Kn5RenderableFile>();
        private List<Kn5RenderableFile> _occluderNodeFiles = new List<Kn5RenderableFile>();

        private BakedObject[] _nodesToBake;
        private BakedObject[] _flattenNodes;
        private BakedObject[] _filteredNodes;
        private BakedObject[] _occluderNodes;

        protected override void DisposeOverride() {
            _mainNode.Dispose();
            _includeNodeFiles.DisposeEverything();
            _occluderNodeFiles.DisposeEverything();
            _state.Dispose();
            _color.Dispose();
            _depth.Dispose();
            _pool.GetList().DisposeEverything();
            base.DisposeOverride();
        }

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public MainBakery(string mainKn5Filename, string filter, string ignoreFilter) : this(Kn5.FromFile(mainKn5Filename)) {
            _filter = filter;
            _ignoreFilter = ignoreFilter;
        }

        private MainBakery(Kn5 kn5) {
            _mainKn5 = kn5;
        }

        protected override void ResizeInner() { }

        private BakeryMaterialsFactory _materialsFactory;
        private MultiKn5TexturesProvider _texturesProvider;

        protected override void InitializeInner() {
            _materialsFactory = new BakeryMaterialsFactory(_mainKn5);
            _texturesProvider = new MultiKn5TexturesProvider(new[] { _mainKn5 }, false);

            foreach (var kn5 in _includeKn5) {
                _texturesProvider.Kn5.Add(kn5);
            }

            foreach (var kn5 in _occludersKn5) {
                _texturesProvider.Kn5.Add(kn5);
            }

            DeviceContextHolder.Set<IMaterialsFactory>(_materialsFactory);
            DeviceContextHolder.Set<ITexturesProvider>(_texturesProvider);
        }

        private BakedMode _setBakedMode;

        private void Render() {
            var h = DeviceContextHolder;
            for (var i = _filteredNodes.Length - 1; i >= 0; i--) {
                var n = _filteredNodes[i];
                var m = n.Object.GetBaseMaterial();
                DeviceContext.Rasterizer.State = _state;
                n.Object.SetBuffers(h);

                if ((Kn5MaterialToBake.SecondPass || Kn5MaterialToBake.GrassPass) && (n.Mode != _setBakedMode)) {
                    _effect.FxSecondPassMode.Set((float)n.Mode);
                    _setBakedMode = n.Mode;
                }

                m.Draw(h, n.Object.IndicesCount, SpecialRenderMode.Simple);
            }
        }

        private TargetResourceDepthTexture _depth;
        private TargetResourceTexture _color;
        private RasterizerState _state;

        private class ExtractTexture : IDisposable {
            public static Device CurrentDevice;
            public static Format CurrentFormat;
            public static int Width, Height;
            public readonly Texture2D Texture;
            public int VertexId;

            public ExtractTexture() {
                Texture = new Texture2D(CurrentDevice, new Texture2DDescription {
                    SampleDescription = new SampleDescription(1, 0),
                    Width = Width,
                    Height = Height,
                    ArraySize = 1,
                    MipLevels = 1,
                    Format = CurrentFormat,
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CpuAccessFlags = CpuAccessFlags.Read
                });
            }

            public void Dispose() {
                Texture?.Dispose();
            }
        }

        private readonly List<ExtractTexture> _extractQueue = new List<ExtractTexture>();
        private readonly Pool<ExtractTexture> _pool = new Pool<ExtractTexture>();

        protected override void DrawOverride() {
            PrepareForFinalPass();
            DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _color.View, RenderTargetView);
        }

        protected override void OnTickOverride(float dt) { }

        public MainBakery LoadExtraKn5(IEnumerable<string> includes, IEnumerable<string> occluders) {
            _includeKn5.Clear();
            _occludersKn5.Clear();

            foreach (var file in includes) {
                _includeKn5.Add(Kn5.FromFile(file));
            }

            foreach (var file in occluders) {
                _occludersKn5.Add(Kn5.FromFile(file));
            }

            return this;
        }

        private long _progressBaked;
        private long _progressTotal;
        private Stopwatch _progressStopwatch;

        private class WeirdMesh {
            public Kn5Node Node;
            public Kn5 Kn5;
            public float MinValue, MaxValue;
        }

        public void Work(string saveTo) {
            Width = SampleResolution;
            Height = SampleResolution;

            if (!Initialized) {
                Initialize();
            }

            _filters = new BakedObjectFilters {
                Tree = TreeFilter == null ? null
                        : Filter.Create(Kn5RenderableObjectTester.Instance, TreeFilter,
                                new FilterParams { StringMatchMode = StringMatchMode.CompleteMatch }),
                Grass = GrassFilter == null ? null
                        : Filter.Create(Kn5RenderableObjectTester.Instance, GrassFilter,
                                new FilterParams { StringMatchMode = StringMatchMode.CompleteMatch }),
                RegularObjects = RegularObjectsFilter == null ? null
                        : Filter.Create(Kn5RenderableObjectTester.Instance, RegularObjectsFilter,
                                new FilterParams { StringMatchMode = StringMatchMode.CompleteMatch }),
            };

            _effect = DeviceContextHolder.GetEffect<EffectBakeryShaders>();
            _depth = TargetResourceDepthTexture.Create();
            _depth.Resize(DeviceContextHolder, Width, Height, null);
            _color = TargetResourceTexture.Create(SampleFormat);
            _color.Resize(DeviceContextHolder, Width, Height, null);

            _state = RasterizerState.FromDescription(Device, new RasterizerStateDescription {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsAntialiasedLineEnabled = false,
                IsFrontCounterclockwise = false,
                IsDepthClipEnabled = true
            });

            var camera = new FpsCamera(CameraFov.ToRadians()) {
                NearZ = CameraNear,
                FarZ = CameraFar,
                DisableFrustum = true
            };
            camera.SetLens(1.0f);

            InitializeNodes(camera);

            _progressStopwatch = Stopwatch.StartNew();
            _progressTotal = _nodesToBake.Sum(x => x.Object.Vertices.Length) * (ExtraPass ? 2 : 1);
            _progressBaked = 0;

            Trace.WriteLine("Total meshes to bake: " + _nodesToBake.Length);
            Trace.WriteLine("Total vertices to bake: " + _progressTotal);

            var minLength = float.MaxValue;
            var maxLength = float.MinValue;
            var verticesChecked = 0L;
            var weirdMeshes = new List<WeirdMesh>();
            foreach (var file in new[] { _mainKn5 }.Concat(_includeKn5).Concat(_occludersKn5)) {
                EnumerateNodes(file.RootNode);

                void EnumerateNodes(Kn5Node node) {
                    if (node.NodeClass == Kn5NodeClass.Base) {
                        foreach (var child in node.Children) {
                            EnumerateNodes(child);
                        }
                    } else {
                        var weird = 0;
                        var nodeMinLength = float.MaxValue;
                        var nodeMaxLength = float.MinValue;
                        foreach (var vertex in node.Vertices) {
                            var length = vertex.TangentU.ToVector3().Length();
                            if (length < nodeMinLength) nodeMinLength = length;
                            else if (length > nodeMaxLength) nodeMaxLength = length;
                            if (length < 0.99f || length > 1.01f) weird++;
                            verticesChecked++;
                        }

                        if (nodeMinLength < minLength) minLength = nodeMinLength;
                        else if (nodeMaxLength > maxLength) maxLength = nodeMaxLength;

                        if (weird > 0) {
                            weirdMeshes.Add(new WeirdMesh {
                                Kn5 = file,
                                Node = node,
                                MinValue = nodeMinLength,
                                MaxValue = nodeMaxLength
                            });
                        }
                    }
                }
            }
            Trace.WriteLine($"Tangents lengths of {verticesChecked} vertices checked: min={minLength}, max={maxLength}");
            if (minLength < 0.99f || maxLength > 1.01f) {
                Trace.WriteLine("That’s a weird track you got there! Please, report it to the developer of this tool. List of strange meshes:");

                foreach (var mesh in weirdMeshes) {
                    Trace.WriteLine($"KN5: {Path.GetFileName(mesh.Kn5.OriginalFilename)}, mesh: {mesh.Node.Name}, "
                            + $"shader: {mesh.Kn5.GetMaterial(mesh.Node.MaterialId)?.ShaderName ?? "?"}, min.: {mesh.MinValue}, max.: {mesh.MaxValue}");
                }

                Trace.WriteLine("For now, press any key to continue.");
                Console.ReadLine();
            }

            ExtractTexture.CurrentDevice = Device;
            ExtractTexture.CurrentFormat = SampleFormat;
            ExtractTexture.Width = Width;
            ExtractTexture.Height = Height;

            DeviceContext.OutputMerger.SetTargets(_depth.DepthView, _color.TargetView);
            DeviceContext.Rasterizer.SetViewports(_color.Viewport);
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.OutputMerger.DepthStencilState = null;

            _stopwatchSamples = new Stopwatch();
            _stopwatchSmoothing = new Stopwatch();

            foreach (var n in _nodesToBake) {
                BakeNode(n, camera, false);
            }

            if (ExtraPass) {
                Trace.WriteLine("Second pass");
                InitializeNodes(camera);

                Kn5MaterialToBake.SecondPass = true;
                Kn5MaterialToBake.SecondPassBrightnessGain = ExtraPassBrightnessGain;
                foreach (var n in _nodesToBake) {
                    BakeNode(n, camera, false);
                }
            }

            Trace.WriteLine($"Taken {_baked} samples: {_stopwatchSamples.Elapsed.ToReadableTime()} "
                    + $"({_baked / _stopwatchSamples.Elapsed.TotalMinutes / 1000:F0}k samples per minute)");
            Trace.WriteLine(
                    $"Smoothing: {_stopwatchSmoothing.Elapsed.TotalSeconds:F3} s (max. marged at once: {_mergedMaximum}, avg.: {(float)_mergedCount / _mergedVertices:F1})");

            if (SpecialGrassAmbient) {
                Trace.WriteLine("Grass syncronization…");
                InitializeNodes(camera);
                SyncGrassGpu();
            }

            if (CreatePatch) {
                using (var data = new MemoryStream())
                using (var writer = new ExtendedBinaryWriter(data)) {
                    foreach (var n in _nodesToBake) {
                        if (GrassNormalsSyncing && SpecialGrassAmbient && n.IsGrass) {
                            WriteNode(writer, n, true);
                        }
                        WriteNode(writer, n, false);
                    }

                    string checksum;
                    using (var stream = File.Open(_mainKn5.OriginalFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var sha1 = SHA1.Create()) {
                        checksum = sha1.ComputeHash(stream).ToHexString().ToLowerInvariant();
                    }

                    var destination = saveTo ?? _mainKn5.OriginalFilename;
                    var patchFilename = Path.Combine(Path.GetDirectoryName(destination) ?? ".", Path.GetFileNameWithoutExtension(destination) + ".vao-patch");
                    using (var stream = File.Create(patchFilename))
                    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                        zip.AddString("Manifest.json", JsonConvert.SerializeObject(new {
                            name = Path.GetFileName(saveTo ?? _mainKn5.OriginalFilename),
                            checksum
                        }));
                        zip.AddString("Config.ini", new IniFile() {
                            ["LIGHTING"] = {
                                ["OPACITY"] = ((double)AoOpacity).Round(0.0001),
                                ["BRIGHTNESS"] = ((double)AoMultiplier).Round(0.0001)
                            }
                        }.Stringify());
                        zip.AddBytes("Patch.data", data.ToArray());

                        var paramsFilename = Path.Combine(Path.GetDirectoryName(_mainKn5.OriginalFilename) ?? ".", "Baked Shadows Params.txt");
                        if (File.Exists(paramsFilename)) {
                            zip.AddBytes("Baked Shadows Params.txt", File.ReadAllBytes(paramsFilename));
                        }
                    }

                    Trace.WriteLine("Saved: " + patchFilename);
                }
            } else {
                _mainKn5.Save(saveTo ?? _mainKn5.OriginalFilename);
                Trace.WriteLine("Saved: " + (saveTo ?? _mainKn5.OriginalFilename));
            }
        }

        private void WriteNode(ExtendedBinaryWriter writer, BakedObject node, bool storeNormals) {
            if (storeNormals) {
                if (!WriteNode_Header(writer, node, PatchEntryType.Normal)) return;
                foreach (var vertex in node.Object.OriginalNode.Vertices) {
                    writer.WriteHalf(vertex.Normal[0]);
                    writer.WriteHalf(vertex.Normal[1]);
                    writer.WriteHalf(vertex.Normal[2]);
                }
                return;
            }

            switch (node.Mode) {
                case BakedMode.TangentLength: {
                    if (!WriteNode_Header(writer, node, PatchEntryType.TangentLength)) return;
                    foreach (var vertex in node.Object.OriginalNode.Vertices) {
                        writer.WriteHalf(vertex.TangentU.ToVector3().Length() * 2.0f - 1.0f);
                    }
                    break;
                }
                case BakedMode.Tangent: {
                    if (!WriteNode_Header(writer, node, PatchEntryType.Tangent)) return;
                    foreach (var vertex in node.Object.OriginalNode.Vertices) {
                        writer.WriteHalf(vertex.TangentU[0] / 1e5f + 1.0f);
                        writer.WriteHalf(vertex.TangentU[1] / 1e5f + 1.0f);
                        writer.WriteHalf(vertex.TangentU[2] / 1e5f + 1.0f);

                        if (node.IsGrass) {
                            // Console.WriteLine("Saved: " + vertex.TangentU[0]);
                        }
                    }
                    break;
                }
            }
        }

        private static bool WriteNode_Header(ExtendedBinaryWriter writer, BakedObject node, PatchEntryType mode) {
            var vertices = node.Object.OriginalNode.Vertices;
            if (vertices.Length == 0 || node.Object.OriginalNode.Name == null) return false;

            writer.Write(node.Object.OriginalNode.Name);

            writer.Write((int)mode);
            writer.Write(vertices[0].Position[0]);
            writer.Write(vertices[0].Position[1]);
            writer.Write(vertices[0].Position[2]);

            writer.Write(vertices.Length);
            return true;
        }

        private class SurfaceTriangle {
            public Vector3 v0, v1, v2;
            public Vector3 t0, t1, t2;
            public float w0m0, w0m1, w1m0, w1m1;

            public SurfaceTriangle(InputLayouts.VerticePNTG[] vertices, ushort[] indices, int index) {
                v0 = vertices[indices[index]].Position;
                v1 = vertices[indices[index + 1]].Position;
                v2 = vertices[indices[index + 2]].Position;
                // if (!Ray.Intersects(ray, v0, v1, v2, out distance) || distance > maxDistance) continue;

                t0 = vertices[indices[index]].Tangent;
                t1 = vertices[indices[index + 1]].Tangent;
                t2 = vertices[indices[index + 2]].Tangent;

                var mult = 1.0f / ((v1.Z - v2.Z) * (v0.X - v2.X) + (v2.X - v1.X) * (v0.Z - v2.Z));
                w0m0 = (v1.Z - v2.Z) * mult;
                w0m1 = (v2.X - v1.X) * mult;
                w1m0 = (v2.Z - v0.Z) * mult;
                w1m1 = (v0.X - v2.X) * mult;
            }

            public bool GetW(float x, float y, out float w0, out float w1, out float w2) {
                w0 = w0m0 * (x - v2.X) + w0m1 * (y - v2.Z);
                w1 = w1m0 * (x - v2.X) + w1m1 * (y - v2.Z);
                w2 = 1.0f - w0 - w1;
                return 0 <= w0 && w0 <= 1 && 0 <= w1 && w1 <= 1 && 0 <= w2 && w2 <= 1;
            }
        }

        private class Surface {
            public BakedObject Baked;
            public BoundingBox Box;
            public SurfaceTriangle[] Triangles;

            public static Surface Create(BakedObject o) {
                var result = new Surface { Baked = o };

                var obj = o.Object;
                var indices = obj.Indices;
                var vertices = obj.Vertices;

                var triangles = new List<SurfaceTriangle>();
                var faulty = 0;
                for (int i = 0, n = indices.Length / 3; i < n; i++) {
                    var n0 = vertices[indices[i * 3]].Normal;
                    var n1 = vertices[indices[i * 3 + 1]].Normal;
                    var n2 = vertices[indices[i * 3 + 2]].Normal;
                    if ((n0.Y < 0.0f || n1.Y <= 0.0f || n2.Y < 0.0f) && ++faulty > 10) {
                        Trace.WriteLine("Miss: " + n0 + "; " + n1 + "; " + n2);
                        return null;
                    }

                    triangles.Add(new SurfaceTriangle(vertices, indices, i * 3));

                    var v0 = vertices[indices[i * 3]].Position;
                    var v1 = vertices[indices[i * 3 + 1]].Position;
                    var v2 = vertices[indices[i * 3 + 2]].Position;
                    SlimDxExtension.Extend(ref result.Box, ref v0);
                    SlimDxExtension.Extend(ref result.Box, ref v1);
                    SlimDxExtension.Extend(ref result.Box, ref v2);
                }

                result.Triangles = triangles.ToArray();
                return result;
            }

            private Surface() { }
        }

        private static Vector3? CheckIntersection(Surface s, Ray ray, float maxDistance) {
            // Trace.WriteLine("Surface: " + s.Baked.Object.OriginalNode.Name + ", BB: " + s.Box + ", pos: " + ray.Position);
            for (int i = 0, n = s.Triangles.Length; i < n; i++) {
                if (s.Triangles[i].GetW(ray.Position.X, ray.Position.Z, out var w0, out var w1, out var w2)) {
                    Trace.WriteLine($"Found surface: {w0:F3}, {w1:F3}, {w2:F3}");

                    var t = s.Triangles[i];
                    if (s.Baked.Mode == BakedMode.TangentLength) {
                        var a0 = t.t0.Length() * 2.0f - 1.0f;
                        var a1 = t.t1.Length() * 2.0f - 1.0f;
                        var a2 = t.t2.Length() * 2.0f - 1.0f;
                        return new Vector3(a0 * w0 + a1 * w1 + a2 * w2);
                    } else {
                        var a0 = t.t0 / 1e5f + new Vector3(1.0f);
                        var a1 = t.t1 / 1e5f + new Vector3(1.0f);
                        var a2 = t.t2 / 1e5f + new Vector3(1.0f);
                        return a0 * w0 + a1 * w1 + a2 * w2;
                    }
                }
            }

            return null;
        }

        private BakedObjectFilters _filters;

        private void SyncGrassGpu() {
            var camera = new FpsCamera(1.0f.ToRadians()) {
                NearZ = CameraNear,
                FarZ = 5.0f,
                DisableFrustum = true
            };
            camera.SetLens(1.0f);

            Width = Height = 1;
            ExtractTexture.Width = Width;
            ExtractTexture.Height = Height;
            _color?.Dispose();
            _color = TargetResourceTexture.Create(Format.R32G32B32A32_UInt);
            ExtractTexture.CurrentFormat = Format.R32G32B32A32_UInt;
            _color.Resize(DeviceContextHolder, Width, Height, null);
            _depth.Resize(DeviceContextHolder, Width, Height, null);
            DeviceContext.OutputMerger.SetTargets(_depth.DepthView, _color.TargetView);
            DeviceContext.Rasterizer.SetViewports(_color.Viewport);
            _pool.GetList().DisposeEverything();
            _pool.Clear();

            var grass = _nodesToBake.Where(x => x.IsGrass).ToList();
            Trace.WriteLine($"Syncing grass with underlying surfaces: {grass.Count} {(grass.Count == 1 ? "mesh" : "meshes")}");

            var s = Stopwatch.StartNew();
            var surfaces = new[] { _mainNode }.Concat(_includeNodeFiles).Concat(_occluderNodeFiles)
                                              .SelectMany(file => FlattenFile(_filters, file))
                                              .Where(x => !x.IsGrass && !x.IsTree && x.GetMaterial()?.AlphaTested == false).NonNull().ToArray();
            _occluderNodes = surfaces;

            // Trace.WriteLine(surfaces.Select(x => x.Baked.GetMaterialName()).Distinct().JoinToString("; "));
            Trace.WriteLine($"Surfaces preparation: {s.Elapsed.TotalMilliseconds:F1} ms");
            s.Restart();

            Kn5MaterialToBake.GrassPass = true;
            for (var grassIndex = 0; grassIndex < grass.Count; grassIndex++) {
                var o = grass[grassIndex];
                BakeNode(o, camera, true);
            }

            Trace.WriteLine($"Grass meshes synced: {s.Elapsed.ToReadableTime()}");
        }

        private void SyncGrassCpu() {
            var grass = _nodesToBake.Where(x => x.IsGrass).Where(x => x.Object.OriginalNode.Name.Contains("_HI_")).ToList();
            // Trace.WriteLine(grass.Select(x => x.Object.OriginalNode.Name).JoinToString("; "));
            Trace.WriteLine($"Syncing grass with underlying surfaces: {grass.Count} {(grass.Count == 1 ? "mesh" : "meshes")}");

            var s = Stopwatch.StartNew();
            var surfaces = new[] { _mainNode }.Concat(_includeNodeFiles).Concat(_occluderNodeFiles)
                                              .SelectMany(file => FlattenFile(_filters, file))
                                              .Where(x => !x.IsGrass && !x.IsTree && x.GetMaterial()?.AlphaTested == false)
                                              .Where(x => x.GetMaterialName() == "grass")
                                              .Select(x => Surface.Create(x)).NonNull().ToArray();
            var bb = surfaces.Select(x => x.Box).ToArray();
            // Trace.WriteLine(surfaces.Select(x => x.Baked.GetMaterialName()).Distinct().JoinToString("; "));
            Trace.WriteLine($"Surfaces preparation: {s.Elapsed.TotalMilliseconds:F1} ms");
            s.Restart();

            var up = Vector3.UnitY * 0.2f;
            var down = -Vector3.UnitY;

            for (var grassIndex = 0; grassIndex < grass.Count; grassIndex++) {
                var o = grass[grassIndex];
                for (var vertexIndex = 0; vertexIndex < o.Object.Vertices.Length; vertexIndex++) {
                    var vertex = o.Object.Vertices[vertexIndex];
                    var pos = vertex.Position;
                    var ray = new Ray(pos + up, down);
                    Vector3 found = -Vector3.UnitY;
                    for (var i = 0; i < bb.Length; i++) {
                        var b = bb[i];
                        if (b.Minimum.X < pos.X && b.Maximum.X > pos.X
                                && b.Minimum.Z < pos.Z && b.Maximum.Z > pos.Z
                                && b.Minimum.Y < pos.Y && b.Maximum.Y + 0.5f > pos.Y) {
                            var n = surfaces[i];
                            var inter = CheckIntersection(n, ray, 0.5f);
                            if (inter.HasValue) {
                                found = inter.Value;
                                SetVector(o.Object.OriginalNode.Vertices[vertexIndex].TangentU, inter.Value);
                                // Trace.WriteLine("Hit: " + n.Baked.Object.OriginalNode.Name + ", BB: " + n.Box + ", pos: " + ray.Position + ", mat.: " + n.Baked.GetMaterialName());
                            } else {
                                // Trace.WriteLine("Miss: " + n.Baked.Object.OriginalNode.Name + ", BB: " + n.Box + ", pos: " + ray.Position + ", mat.: " + n.Baked.GetMaterialName());
                            }
                        }
                    }

                    if (found != -Vector3.UnitY) { } else {
                        Trace.WriteLine("Nothing found");
                        // Environment.Exit(1);
                    }
                }

                Trace.WriteLine($"Grass mesh synced: {o.Object.OriginalNode.Name} ({grassIndex}/{grass.Count})");
            }

            Trace.WriteLine($"Grass meshes synced: {s.Elapsed.ToReadableTime()}");
        }

        private static void SetVector(float[] destination, Vector3 value) {
            destination[0] = value.X;
            destination[1] = value.Y;
            destination[2] = value.Z;
        }

        private static void SetVector(float[] destination, float x, float y, float z) {
            destination[0] = x;
            destination[1] = y;
            destination[2] = z;
        }

        private void InitializeNodes(FpsCamera camera) {
            _mainNode?.Dispose();

            _mainNode = new Kn5RenderableFile(_mainKn5, Matrix.Identity, false);
            _mainNode.Draw(DeviceContextHolder, camera, SpecialRenderMode.InitializeOnly);

            _includeNodeFiles.DisposeEverything();
            foreach (var kn5 in _includeKn5) {
                var node = new Kn5RenderableFile(kn5, Matrix.Identity, false);
                node.Draw(DeviceContextHolder, camera, SpecialRenderMode.InitializeOnly);
                _includeNodeFiles.Add(node);
            }

            if (_occluderNodeFiles.Count == 0) {
                foreach (var kn5 in _occludersKn5) {
                    var node = new Kn5RenderableFile(kn5, Matrix.Identity, false);
                    node.Draw(DeviceContextHolder, camera, SpecialRenderMode.InitializeOnly);
                    _occluderNodeFiles.Add(node);
                }
            }

            RefreshFlatten();

            foreach (var n in _flattenNodes) {
                n.Object.UpdateBoundingBox();
                n.Object.Draw(DeviceContextHolder, camera, SpecialRenderMode.InitializeOnly);
            }
        }

        private void RefreshFlatten() {
            var filter = Filter.Create(Kn5RenderableObjectTester.Instance, _filter,
                    new FilterParams { StringMatchMode = StringMatchMode.CompleteMatch });
            var ignoreFilter = _ignoreFilter == null ? null
                    : Filter.Create(Kn5RenderableObjectTester.Instance, _ignoreFilter,
                            new FilterParams { StringMatchMode = StringMatchMode.CompleteMatch });
            var skipOccludersFilter = SkipOccludersFilter == null ? null
                    : Filter.Create(Kn5RenderableObjectTester.Instance, SkipOccludersFilter,
                            new FilterParams { StringMatchMode = StringMatchMode.CompleteMatch });

            bool IsMeshToBake(BakedObject n) {
                return filter.Test(n) && ignoreFilter?.Test(n) != true;
            }

            bool IsOccludingMesh(BakedObject n) {
                if (n.GetShaderName() == "ksGrass") return false;
                if (n.GetMaterial()?.BlendMode != Kn5MaterialBlendMode.Opaque) return false;
                return skipOccludersFilter?.Test(n) != true;
            }

            _flattenNodes = new[] { _mainNode }.Concat(_includeNodeFiles).Concat(_occluderNodeFiles).SelectMany(file => FlattenFile(_filters, file)).ToArray();
            _nodesToBake = new[] { _mainNode }.Concat(_includeNodeFiles).SelectMany(file => FlattenFile(_filters, file).Where(IsMeshToBake)).ToArray();
            _occluderNodes = _flattenNodes.Where(IsOccludingMesh).ToArray();
        }

        private static IEnumerable<BakedObject> FlattenFile([NotNull] BakedObjectFilters filters, Kn5RenderableFile file) {
            return Flatten(file, o => {
                var kn5Node = (o as Kn5RenderableObject)?.OriginalNode;
                if (kn5Node == null) return true;
                return kn5Node.IsRenderable
                        && !Regex.IsMatch(kn5Node.Name, @"^(?:AC_)", RegexOptions.IgnoreCase);
            }).OfType<Kn5RenderableObject>().Select(x => new BakedObject(x, file.OriginalFile, filters));
        }

        public bool ExtraPass;
        public bool CreatePatch;
        public bool SpecialGrassAmbient;
        public bool HdrSamples = false;
        public bool GrassNormalsSyncing = false;
        private Format SampleFormat => HdrSamples ? Format.R32G32B32A32_Float : Format.R8G8B8A8_UNorm;

        public string TreeFilter;
        public string GrassFilter;
        public string RegularObjectsFilter;
        public string SkipOccludersFilter;

        public float AoMultiplier = 1.0f;
        public float AoOpacity = 0.92f;
        public float SaturationGain = 3.0f;
        public float SaturationInputMultiplier = 2.0f;
        public float CameraFov = 120.0f;
        public float CameraNear = 0.15f;
        public float CameraFar = 50.0f;
        public float CameraNormalOffsetUp = 0.2f;
        public float CameraOffsetAway = 0.16f;
        public float CameraOffsetUp = 0.08f;
        public float OccludersDistanceThreshold = 30.0f;
        public float ExtraPassBrightnessGain;
        public int MergeVertices = 30;
        public float MergeThreshold = 0.1f;
        public int QueueSize = 50;
        public int SampleResolution = 32;

        private Stopwatch _stopwatchSamples, _stopwatchSmoothing;
        private long _baked;

        private class VertexGroup {
            public Vector3 Position;
            public Vector3 Normal;
            public readonly List<int> Indices = new List<int>();
        }

        private int _mergedMaximum;
        private int _mergedCount;
        private int _mergedVertices;
        private EffectBakeryShaders _effect;

        private void BakeNode(BakedObject prepared, FpsCamera camera, bool grassMode) {
            if (!grassMode && SpecialGrassAmbient && prepared.IsGrass) {
                Trace.WriteLine("Skipping grass: " + prepared.Object.OriginalNode.Name);
                return;
            }

            var obj = prepared.Object;
            var effect = _effect;
            var boundingBox = obj.BoundingBox ?? new BoundingBox();

            _filteredNodes = _occluderNodes.Where(x => {
                if (x.Object.BoundingBox == null) return false;

                var sameAsBaked = ReferenceEquals(x.Object, prepared.Object);
                if (sameAsBaked ? prepared.IsTree : x.Object.OriginalNode.LodIn > 0.0f) {
                    return false;
                }

                var xb = x.Object.BoundingBox.Value;
                return (xb.GetCenter() - boundingBox.GetCenter()).Length() - xb.GetSize().Length() / 2 - boundingBox.GetSize().Length() / 2
                        < OccludersDistanceThreshold;
            }).ToArray();

            _stopwatchSmoothing.Start();
            var size = obj.Vertices.Length;
            var groups = new List<VertexGroup>(size);

            var length = obj.Vertices.Length;
            var sorted = new List<int>(length);
            var threshold = MergeThreshold;

            for (var i = 0; i < length; i++) {
                sorted.Add(i);
            }

            sorted.Sort((left, right) => {
                var pa = obj.Vertices[left].Position.X;
                var pb = obj.Vertices[right].Position.X;
                if (float.IsNaN(pa)) return float.IsNaN(pb) ? 0 : 1;
                if (float.IsNaN(pb)) return -1;
                if (Math.Abs(pa - pb) < 0.001f) return 0;
                return pa > pb ? 1 : -1;
            });

            for (var iIndex = 0; iIndex < length; iIndex++) {
                var i = sorted[iIndex];
                if (i == -1) continue;

                var p = obj.Vertices[i].Position;
                var m = obj.Vertices[i].Normal;
                var g = new VertexGroup();

                g.Indices.Add(i);
                g.Position += p;
                g.Normal += m;

                for (int jIndex = iIndex + 1, x = Math.Min(iIndex + MergeVertices, length); jIndex < x; jIndex++) {
                    var j = sorted[jIndex];
                    if (j == -1) continue;

                    var jp = obj.Vertices[j].Position;
                    var jm = obj.Vertices[j].Normal;
                    if (jp.X < p.X - threshold || jp.X > p.X + threshold
                            || jp.Y < p.Y - threshold || jp.Y > p.Y + threshold
                            || jp.Z < p.Z - threshold || jp.Z > p.Z + threshold
                            || Vector3.Dot(m, jm) < 0.5f) continue;

                    g.Indices.Add(j);
                    g.Position += jp;
                    g.Normal += jm;
                    sorted[jIndex] = -1;
                }

                g.Position /= g.Indices.Count;
                g.Normal /= g.Indices.Count;
                groups.Add(g);

                if (g.Indices.Count > _mergedMaximum) _mergedMaximum = g.Indices.Count;
                _mergedCount += g.Indices.Count;
                _mergedVertices++;
            }
            _stopwatchSmoothing.Stop();

            var s = Stopwatch.StartNew();
            _stopwatchSamples.Start();

            for (var i = 0; i < groups.Count; i += 1) {
                var vertex = groups[i];
                var world = vertex.Position;
                var normal = grassMode ? -Vector3.UnitY : prepared.IsTree ? Vector3.UnitY
                        : Vector3.Normalize(vertex.Normal + Vector3.UnitY * CameraNormalOffsetUp);

                camera.Position = grassMode ? world + Vector3.UnitY : world + vertex.Normal * CameraOffsetAway + Vector3.UnitY * CameraOffsetUp;
                camera.Look = normal;

                camera.Right = Vector3.Normalize(Vector3.Cross(
                        Math.Abs(Vector3.Dot(camera.Look, Vector3.UnitY)) > 0.8f ? Vector3.UnitZ : Vector3.UnitY, camera.Look));
                camera.Up = Vector3.Normalize(Vector3.Cross(camera.Look, camera.Right));
                camera.UpdateViewMatrix();
                effect.FxWorldViewProj.SetMatrix(camera.ViewProj);

                // drawing:
                DeviceContext.ClearDepthStencilView(_depth.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
                DeviceContext.ClearRenderTargetView(_color.TargetView, grassMode ? new Color4(1, 0, 0, 1) : new Color4());
                DeviceContext.InputAssembler.InputLayout = effect.LayoutPNTG;

                Render();

                var copy = _pool.Get();
                copy.VertexId = i;
                Device.ImmediateContext.CopyResource(_color.Texture, copy.Texture);
                _extractQueue.Add(copy);

                if (_extractQueue.Count > QueueSize * 2) {
                    for (var j = 0; j < QueueSize; j++) {
                        Finalize(_extractQueue[j]);
                    }
                    _extractQueue.RemoveRange(0, QueueSize);
                }

                _baked++;
            }

            for (var j = 0; j < _extractQueue.Count; j++) {
                Finalize(_extractQueue[j]);
            }

            _extractQueue.Clear();

            _stopwatchSamples.Stop();
            _progressBaked += obj.OriginalNode.TotalVerticesCount;

            var speed = _progressBaked / _progressStopwatch.Elapsed.TotalSeconds;
            var leftSeconds = TimeSpan.FromSeconds(1.2f * (_progressTotal - _progressBaked) / speed);

            Trace.WriteLine($"{obj.OriginalNode.Name}: taken {groups.Count} samples: {s.Elapsed.TotalMilliseconds:F1} ms "
                    + $"({groups.Count / s.Elapsed.TotalMinutes / 1000:F0}k samples per minute; "
                    + $"progress: {100d * _progressBaked / _progressTotal:F1}%; ETA: {leftSeconds.ToReadableTime()})");

            void Finalize(ExtractTexture t) {
                // var yVal = obj.Vertices[groups[t.VertexId].Indices[0]].Position.Y;
                // Resource.SaveTextureToFile(Device.ImmediateContext, t.Texture, ImageFileFormat.Dds, $@"D:\Temporary\Big Piles of Crap\{yVal:F4}_{t.VertexId}.dds");
                var rect = Device.ImmediateContext.MapSubresource(t.Texture, 0, MapMode.Read, SlimDX.Direct3D11.MapFlags.None);

                var satGain = SaturationGain;
                var satInputMult = SaturationInputMultiplier;
                var satInputMultInv = 0.5f / satInputMult;
                var avgAoOnly = ExtraPass && !Kn5MaterialToBake.SecondPass;

                try {
                    var a = Vector3.Zero;
                    var n = Vector3.Zero;

                    using (var bb = new ReadAheadBinaryReader(rect.Data)) {
                        for (var y = 0; y < Height; y++) {
                            bb.Seek(rect.RowPitch * y, SeekOrigin.Begin);
                            for (var x = 0; x < Width; x++) {
                                float r, g, b, w;
                                if (grassMode) {
                                    bb.ReadUInt32_4D(out var vx, out var vy, out var vz, out var vw);
                                    r = Half.ToHalf((ushort)(vx >> 16));
                                    g = Half.ToHalf((ushort)(vx & 0xffff));
                                    b = Half.ToHalf((ushort)(vy >> 16));
                                    w = Half.ToHalf((ushort)(vy & 0xffff));
                                    n.X += Half.ToHalf((ushort)(vz >> 16));
                                    n.Y += Half.ToHalf((ushort)(vz & 0xffff));
                                    n.Z += Half.ToHalf((ushort)(vw >> 16));

                                    var nw = 1.0f - w;
                                    a.X = r * w + nw;
                                    a.Y = g * w + nw;
                                    a.Z = b * w + nw;
                                } else {
                                    if (HdrSamples) {
                                        bb.ReadSingle4D(out r, out g, out b, out w);
                                    } else {
                                        bb.ReadByte4D(out var byteR, out var byteG, out var byteB, out var byteW);
                                        r = byteR / 255.0f;
                                        g = byteG / 255.0f;
                                        b = byteB / 255.0f;
                                        w = byteW / 255.0f;
                                    }

                                    if (avgAoOnly) {
                                        var nw = 1.0f - w;
                                        var av = w * (r + g + b) / 3.0f + nw;
                                        a.X += av;
                                        a.Y += av;
                                        a.Z += av;
                                    } else {
                                        var sw = w * (1.0f + satGain * (GetSaturation(r, g, b) * satInputMult - satInputMultInv).Saturate());
                                        var nw = 1.0f - w;
                                        a.X += sw * r + nw;
                                        a.Y += sw * g + nw;
                                        a.Z += sw * b + nw;
                                    }
                                }
                            }
                        }
                    }

                    a /= Width * Height;
                    var ind = groups[t.VertexId].Indices;

                    if (grassMode) {
                        if (GrassNormalsSyncing) {
                            n /= Width * Height;
                            n.Normalize();
                            for (var i = ind.Count - 1; i >= 0; i--) {
                                SetVector(obj.OriginalNode.Vertices[groups[t.VertexId].Indices[i]].Normal, n);
                            }
                        }
                    } else if (CreatePatch && (!ExtraPass || Kn5MaterialToBake.SecondPass)) {
                        a.X = a.X.Saturate();
                        a.Y = a.Y.Saturate();
                        a.Z = a.Z.Saturate();
                    } else {
                        a = new Vector3(1.0f - AoOpacity) + a * AoOpacity;
                        a.X = a.X.Saturate();
                        a.Y = a.Y.Saturate();
                        a.Z = a.Z.Saturate();
                        a *= AoMultiplier;
                    }

                    switch (prepared.Mode) {
                        case BakedMode.TangentLength:
                            for (var i = ind.Count - 1; i >= 0; i--) {
                                var avg = (a.X + a.Y + a.Z) / 3.0f;
                                var g = ind[i];
                                var v = Vector3.Normalize(obj.Vertices[g].Tangent) * (0.5f + avg * 0.5f);
                                SetVector(obj.OriginalNode.Vertices[g].TangentU, v);
                            }
                            break;
                        default:
                            for (var i = ind.Count - 1; i >= 0; i--) {
                                var g = ind[i];
                                SetVector(obj.OriginalNode.Vertices[g].TangentU,
                                        a.X * 1e5f - 1e5f, a.Y * 1e5f - 1e5f, a.Z * 1e5f - 1e5f);
                            }
                            break;
                    }
                } finally {
                    Device.ImmediateContext.UnmapSubresource(t.Texture, 0);
                }
                _pool.Add(t);
            }
        }

        private static float GetSaturation(float r, float g, float b) {
            if (r == g && g == b) return 0;

            var max = r;
            var min = r;

            if (g > max) max = g;
            else if (g < min) min = g;

            if (b > max) max = b;
            else if (b < min) min = b;

            var sum = max + min;
            return (max - min) / (sum > 1.0f ? 2.0f - sum : sum + 0.00001f);
        }
    }
}