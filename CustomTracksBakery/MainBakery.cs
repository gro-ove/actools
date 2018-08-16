using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AcTools;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
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

namespace CustomTracksBakery {
    public class Kn5RenderableObjectTester : ITester<Kn5RenderableObject> {
        public static readonly Kn5RenderableObjectTester Instance = new Kn5RenderableObjectTester();

        public static Kn5 CurrentKn5;

        public string ParameterFromKey(string key) {
            return null;
        }

        public bool Test(Kn5RenderableObject obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(obj.OriginalNode.Name);
                case "shader":
                    return value.Test(CurrentKn5.GetMaterial(obj.OriginalNode.MaterialId)?.ShaderName);
                case "material":
                    return value.Test(CurrentKn5.GetMaterial(obj.OriginalNode.MaterialId)?.Name);
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
        private readonly List<Kn5> _extraKn5 = new List<Kn5>();
        private Kn5RenderableFile _mainNode;
        private List<Kn5RenderableFile> _extraNodes = new List<Kn5RenderableFile>();

        private struct PreparedObject {
            public readonly Kn5RenderableObject Object;
            public readonly bool AreTangentsUsed;
            public readonly bool IsTree;

            public PreparedObject(Kn5RenderableObject n, Kn5 kn5,
                    [CanBeNull] IFilter<Kn5RenderableObject> treeFilter) {
                Object = n;
                AreTangentsUsed = kn5.GetMaterial(n.OriginalNode.MaterialId)?.GetMappingByName("txNormal") != null
                        || kn5.GetMaterial(n.OriginalNode.MaterialId)?.GetMappingByName("txNormalDetail") != null
                        || kn5.GetMaterial(n.OriginalNode.MaterialId)?.GetMappingByName("txDetailNM") != null;

                Kn5RenderableObjectTester.CurrentKn5 = kn5;
                IsTree = treeFilter?.Test(n) == true;
            }
        }

        private PreparedObject[] _nodesToBake;
        private PreparedObject[] _flattenNodes;
        private PreparedObject[] _filteredNodes;
        private PreparedObject[] _occluderNodes;

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public MainBakery(string mainKn5Filename, string filter, string ignoreFilter) : this(Kn5.FromFile(mainKn5Filename)) {
            _filter = filter;
            _ignoreFilter = ignoreFilter;
        }

        private MainBakery(Kn5 kn5) {
            _mainKn5 = kn5;
            Kn5RenderableObjectTester.CurrentKn5 = kn5;
        }

        protected override void ResizeInner() { }

        private BakeryMaterialsFactory _materialsFactory;
        private MultiKn5TexturesProvider _texturesProvider;

        protected override void InitializeInner() {
            _materialsFactory = new BakeryMaterialsFactory(_mainKn5);
            _texturesProvider = new MultiKn5TexturesProvider(new[] { _mainKn5 }, false);

            foreach (var kn5 in _extraKn5) {
                _texturesProvider.Kn5.Add(kn5);
            }

            DeviceContextHolder.Set<IMaterialsFactory>(_materialsFactory);
            DeviceContextHolder.Set<ITexturesProvider>(_texturesProvider);
        }

        private bool _setAreTangentsUsed;

        private void Render() {
            var h = DeviceContextHolder;
            for (var i = _filteredNodes.Length - 1; i >= 0; i--) {
                var n = _filteredNodes[i];
                var m = n.Object.GetBaseMaterial();
                DeviceContext.Rasterizer.State = _state;
                n.Object.SetBuffers(h);

                if (Kn5MaterialToBake.SecondPass && (n.AreTangentsUsed != _setAreTangentsUsed)) {
                    _effect.FxSecondPassMode.Set(n.AreTangentsUsed ? 1.0f : 0.0f);
                    _setAreTangentsUsed = n.AreTangentsUsed;
                }

                m.Draw(h, n.Object.IndicesCount, SpecialRenderMode.Simple);
            }
        }

        private TargetResourceDepthTexture _depth;
        private TargetResourceTexture _color;
        private RasterizerState _state;

        private class ExtractTexture {
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
        }

        private readonly List<ExtractTexture> _extractQueue = new List<ExtractTexture>();
        private readonly Pool<ExtractTexture> _pool = new Pool<ExtractTexture>();

        protected override void DrawOverride() {
            PrepareForFinalPass();
            DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, _color.View, RenderTargetView);
        }

        protected override void OnTickOverride(float dt) { }

        public MainBakery LoadExtraOccluders(string optionsCommonKn5Filter) {
            _extraKn5.Clear();
            var directory = Path.GetDirectoryName(_mainKn5.OriginalFilename);
            if (!string.IsNullOrWhiteSpace(optionsCommonKn5Filter) && directory != null) {
                var filter = Filter.Create(StringTester.Instance, optionsCommonKn5Filter, new FilterParams { StringMatchMode = StringMatchMode.CompleteMatch });
                foreach (var file in Directory.GetFiles(directory, "*.kn5").Where(x =>
                        filter.Test(Path.GetFileName(x)) && !FileUtils.ArePathsEqual(x, _mainKn5.OriginalFilename))) {
                    _extraKn5.Add(Kn5.FromFile(file));
                }
            }
            return this;
        }

        private long _progressBaked;
        private long _progressTotal;
        private Stopwatch _progressStopwatch;

        public void Work(string saveTo) {
            Width = SampleResolution;
            Height = SampleResolution;

            if (!Initialized) {
                Initialize();
            }

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

            Console.WriteLine("Total meshes to bake: " + _nodesToBake.Length);
            Console.WriteLine("Total vertices to bake: " + _progressTotal);

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
                BakeNode(n, camera);
            }

            if (ExtraPass) {
                Console.WriteLine("Second pass");
                InitializeNodes(camera);

                Kn5MaterialToBake.SecondPass = true;
                Kn5MaterialToBake.SecondPassBrightnessGain = ExtraPassBrightnessGain;
                foreach (var n in _nodesToBake) {
                    BakeNode(n, camera);
                }
            }

            Console.WriteLine($"Taken {_baked} samples: {_stopwatchSamples.Elapsed.ToReadableTime()} ms "
                    + $"({_baked / _stopwatchSamples.Elapsed.TotalMinutes / 1000:F0}k samples per minute)");
            Console.WriteLine(
                    $"Smoothing: {_stopwatchSmoothing.Elapsed.TotalSeconds:F3} s (max. marged at once: {_mergedMaximum}, avg.: {(float)_mergedCount / _mergedVertices:F1})");

            if (CreatePatch) {
                using (var data = new MemoryStream())
                using (var writer = new ExtendedBinaryWriter(data)) {
                    foreach (var n in _nodesToBake) {
                        var vertices = n.Object.OriginalNode.Vertices;
                        if (vertices.Length == 0 || n.Object.OriginalNode.Name == null) continue;

                        writer.Write(n.Object.OriginalNode.Name);

                        writer.Write(n.AreTangentsUsed ? 1 : 0);
                        writer.Write(vertices[0].Position[0]);
                        writer.Write(vertices[0].Position[1]);
                        writer.Write(vertices[0].Position[2]);

                        writer.Write(vertices.Length);
                        if (n.AreTangentsUsed) {
                            foreach (var vertex in vertices) {
                                writer.WriteHalf(1.0f - vertex.TangentU.ToVector3().Length() / 1e7f);
                            }
                        } else {
                            foreach (var vertex in vertices) {
                                writer.WriteHalf(vertex.TangentU[0] / 1e7f + 1.0f);
                                writer.WriteHalf(vertex.TangentU[1] / 1e7f + 1.0f);
                                writer.WriteHalf(vertex.TangentU[2] / 1e7f + 1.0f);
                            }
                        }
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
                        zip.AddBytes("Patch.data", data.ToArray());

                        var paramsFilename = Path.Combine(Path.GetDirectoryName(_mainKn5.OriginalFilename) ?? ".", "Baked Shadows Params.txt");
                        if (File.Exists(paramsFilename)) {
                            zip.AddBytes("Baked Shadows Params.txt", File.ReadAllBytes(paramsFilename));
                        }
                    }

                    Console.WriteLine("Saved: " + patchFilename);
                }
            } else {
                _mainKn5.Save(saveTo ?? _mainKn5.OriginalFilename);
                Console.WriteLine("Saved: " + (saveTo ?? _mainKn5.OriginalFilename));
            }
        }

        private void InitializeNodes(FpsCamera camera) {
            _mainNode?.Dispose();

            _mainNode = new Kn5RenderableFile(_mainKn5, Matrix.Identity, false);
            _mainNode.Draw(DeviceContextHolder, camera, SpecialRenderMode.InitializeOnly);

            if (_extraNodes.Count == 0) {
                foreach (var kn5 in _extraKn5) {
                    var node = new Kn5RenderableFile(kn5, Matrix.Identity, false);
                    node.Draw(DeviceContextHolder, camera, SpecialRenderMode.InitializeOnly);
                    _extraNodes.Add(node);
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
            var treeFilter = TreeFilter == null ? null
                    : Filter.Create(Kn5RenderableObjectTester.Instance, TreeFilter,
                            new FilterParams { StringMatchMode = StringMatchMode.CompleteMatch });
            var skipOccludersFilter = SkipOccludersFilter == null ? null
                    : Filter.Create(Kn5RenderableObjectTester.Instance, SkipOccludersFilter,
                            new FilterParams { StringMatchMode = StringMatchMode.CompleteMatch });

            _flattenNodes = new[] { _mainNode }.Concat(_extraNodes).SelectMany(file => FlattenFile(treeFilter, file)).ToArray();
            _nodesToBake = FlattenFile(treeFilter, _mainNode).Where(n => filter.Test(n.Object) && ignoreFilter?.Test(n.Object) != true).ToArray();
            _occluderNodes = _flattenNodes.Where(x => skipOccludersFilter?.Test(x.Object) != true).ToArray();
        }

        private static IEnumerable<PreparedObject> FlattenFile([CanBeNull] IFilter<Kn5RenderableObject> treeFilter, Kn5RenderableFile file) {
            return Flatten(file, o => {
                if (Regex.IsMatch((o as Kn5RenderableObject)?.OriginalNode.Name ?? "_", @"^(?:AC_)")) {
                    return false;
                }
                return true;
            }).OfType<Kn5RenderableObject>().Select(x => new PreparedObject(x, file.OriginalFile, treeFilter));
        }

        public bool ExtraPass;
        public bool CreatePatch;
        public bool HdrSamples = false;
        private Format SampleFormat => HdrSamples ? Format.R32G32B32A32_Float : Format.R8G8B8A8_UNorm;

        public string TreeFilter;
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

        private void BakeNode(PreparedObject prepared, FpsCamera camera) {
            var tangentsUsed = prepared.AreTangentsUsed;
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
            sorted.Sort((_a, _b) => {
                var pa = obj.Vertices[_a].Position.X;
                var pb = obj.Vertices[_b].Position.X;
                return pa > pb ? 1 : pa == pb ? 0 : -1;
            });

            for (var _i = 0; _i < length; _i++) {
                var i = sorted[_i];
                if (i == -1) continue;

                var p = obj.Vertices[i].Position;
                var m = obj.Vertices[i].Normal;
                var g = new VertexGroup();

                g.Indices.Add(i);
                g.Position += p;
                g.Normal += m;

                for (int _j = _i + 1, x = Math.Min(_i + MergeVertices, length); _j < x; _j++) {
                    var j = sorted[_j];
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
                    sorted[_j] = -1;
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
                var normal = prepared.IsTree ? Vector3.UnitY : Vector3.Normalize(vertex.Normal + Vector3.UnitY * CameraNormalOffsetUp);

                camera.Position = world + vertex.Normal * CameraOffsetAway + Vector3.UnitY * CameraOffsetUp;
                camera.Look = normal;

                camera.Right = Vector3.Normalize(Vector3.Cross(
                        Math.Abs(Vector3.Dot(camera.Look, Vector3.UnitY)) > 0.5f ? Vector3.UnitZ : Vector3.UnitY, camera.Look));
                camera.Up = Vector3.Normalize(Vector3.Cross(camera.Look, camera.Right));
                camera.UpdateViewMatrix();
                effect.FxWorldViewProj.SetMatrix(camera.ViewProj);

                // drawing:
                DeviceContext.ClearDepthStencilView(_depth.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
                DeviceContext.ClearRenderTargetView(_color.TargetView, new Color4());
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

            Console.WriteLine($"{obj.OriginalNode.Name}: taken {groups.Count} samples: {s.Elapsed.TotalMilliseconds:F1} ms "
                    + $"({groups.Count / s.Elapsed.TotalMinutes / 1000:F0}k samples per minute; "
                    + $"progress: {100d * _progressBaked / _progressTotal:F1}%; ETA: {leftSeconds.ToReadableTime()})");

            void Finalize(ExtractTexture t) {
                var rect = Device.ImmediateContext.MapSubresource(t.Texture, 0, MapMode.Read, SlimDX.Direct3D11.MapFlags.None);

                var satGain = SaturationGain;
                var satInputMult = SaturationInputMultiplier;
                var satInputMultInv = 0.5f / satInputMult;
                var avgAoOnly = ExtraPass && !Kn5MaterialToBake.SecondPass;

                try {
                    var a = Vector3.Zero;
                    using (var bb = new ReadAheadBinaryReader(rect.Data)) {
                        for (var y = 0; y < Height; y++) {
                            bb.Seek(rect.RowPitch * y, SeekOrigin.Begin);
                            for (var x = 0; x < Width; x++) {
                                float r, g, b, w;
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

                    a /= Width * Height;
                    a *= AoMultiplier;
                    a = new Vector3(1.0f - AoOpacity) + a * AoOpacity;
                    a.X = a.X.Saturate();
                    a.Y = a.Y.Saturate();
                    a.Z = a.Z.Saturate();

                    if (tangentsUsed) {
                        foreach (var g in groups[t.VertexId].Indices) {
                            var avg = (a.X + a.Y + a.Z) / 3.0f;
                            var v = Vector3.Normalize(obj.Vertices[g].Tangent) * (1.0f + (1e7f - 1.0f) * (1.0f - avg));
                            obj.OriginalNode.Vertices[g].TangentU[0] = v.X;
                            obj.OriginalNode.Vertices[g].TangentU[1] = v.Y;
                            obj.OriginalNode.Vertices[g].TangentU[2] = v.Z;
                        }
                    } else {
                        foreach (var g in groups[t.VertexId].Indices) {
                            obj.OriginalNode.Vertices[g].TangentU[0] = a.X * 1e7f - 1e7f;
                            obj.OriginalNode.Vertices[g].TangentU[1] = a.Y * 1e7f - 1e7f;
                            obj.OriginalNode.Vertices[g].TangentU[2] = a.Z * 1e7f - 1e7f;
                        }
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