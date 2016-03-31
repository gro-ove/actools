using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Kn5Render.Utils;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;
using Buffer = SlimDX.Direct3D11.Buffer;

namespace AcTools.Kn5Render.Kn5Render {
    public static class Kn5MaterialExtend {
        public static float GetPropertyValueAByName(this Kn5Material mat, string name, float defaultValue = 0.0f) {
            var property = mat.GetPropertyByName(name);
            return property == null ? defaultValue : property.ValueA;
        }
    }

    public partial class Render : System.IDisposable {
        string _filename;
        Kn5 _kn5;

        public Kn5 LoadedKn5 {
            get { return _kn5; }
        }

        VisualMode _mode;

        List<MeshObject> _objs;
        List<ShaderMaterial> _materials;
        Dictionary<string, ShaderResourceView> _textures;

        public int SelectedSkin;
        public List<string> Skins;
        List<string> _overrides;

        Vector3 _objectPos, _wheelLfPos, _wheelRfPos, _wheelLrPos, _wheelRrPos;
        BoundingBox _sceneBoundingBox = new BoundingBox(new Vector3(float.MaxValue), new Vector3(float.MinValue));

        DirectionalLight _dirLight;
        Color4 _backgroundColor, _wireframeBackgroundColor;

        ShaderResourceView _reflectionCubemap;

        class MeshObject : System.IDisposable {
            public bool Visible = true;

            public string Name;

            public VertexBufferBinding VertexBufferBinding;

            public Buffer Vb {
                get { return _vb; }
                set {
                    _vb = value;
                    VertexBufferBinding = new VertexBufferBinding(_vb, Stride, 0);
                }
            }

            protected virtual int Stride {
                get { return VerticePT.Stride; }
            }

            public Buffer Ib;
            public int IndCount;
            public Matrix Transform, InvTransform;
            public ShaderMaterial Mat;
            public string TexName, NormalTexName, DetailTexName, MapTexName;
            public ShaderResourceView Tex;
            public RasterizerState Rast;
            public BlendState Blen;

            private Buffer _vb;

            public void Dispose() {
                Vb.Dispose();
                Ib.Dispose();
                if (Tex != null) Tex.Dispose();
                if (Blen != null) Blen.Dispose();
                //if (Rast != null) Rast.Dispose();
            }
        }

        class MeshObjectShadow : MeshObject { }

        class MeshObjectKn5 : MeshObject {
            public Kn5Node OriginalNode;
            public BoundingBox MeshBox;

            protected override int Stride {
                get { return VerticePNT.Stride; }
            }
        }

        void LoadScene(string filename, int skinNumber, VisualMode mode) {
            _mode = mode;
            _filename = filename;

            _kn5 = Kn5.FromFile(filename, mode == VisualMode.BODY_SHADOW || mode == VisualMode.TRACK_MAP);
            if (_kn5.IsWithoutTextures()) {
                var mainFile = FileUtils.GetMainCarFilename(Path.GetDirectoryName(filename));
                if (mainFile != null) {
                    _kn5.LoadTexturesFrom(mainFile);
                }
            }

            _textures = new Dictionary<string, ShaderResourceView>(_kn5.Textures.Count);

            LoadMaterials();
            GetObjects();

            _wireframeBackgroundColor = new Color4(0x292826);

            switch (_mode) {
                case VisualMode.SIMPLE_PREVIEW_GT5: {
                        _camera = new CameraOrbit(0.08f * MathF.PI) { Alpha = 1.1f, Beta = -0.04f, Radius = 8.0f, Target = new Vector3(0, 0.78f, 0) };

                        _dirLight = new DirectionalLight {
                            Ambient = new Color4(0.65f, 0.66f, 0.64f),
                            Diffuse = new Color4(1.84f, 1.87f, 1.88f),
                            Specular = new Color4(0.95f, 0.96f, 1.13f),
                            Direction = new Vector3(-1.57735f, -2.57735f, 0.57735f)
                        };

                        _backgroundColor = new Color4(0.0f, 0.0f, 0.0f, 0.0f);
                        _reflectionCubemap = ShaderResourceView.FromMemory(CurrentDevice, Properties.Resources.TextureDarkRoomReflection);
                    }
                    break;

                case VisualMode.SIMPLE_PREVIEW_GT6: {
                        _camera = new CameraOrbit(0.011f * MathF.PI) { Alpha = 0.0f, Beta = 0.0f, NearZ = 1.0f, Radius = 90.0f, Target = new Vector3(0, 0.78f, 0) };

                        _dirLight = new DirectionalLight {
                            Ambient = new Color4(0.65f, 0.66f, 0.64f),
                            Diffuse = new Color4(1.84f, 1.87f, 1.88f),
                            Specular = new Color4(0.95f, 0.96f, 1.13f),
                            Direction = new Vector3(-1.57735f, -2.57735f, 0.57735f)
                        };

                        _backgroundColor = new Color4(0.0f, 0.0f, 0.0f, 0.0f);
                        _reflectionCubemap = ShaderResourceView.FromMemory(CurrentDevice, Properties.Resources.TextureDarkRoomReflection);
                    }
                    break;

                case VisualMode.LIVERY_VIEW: {
                        _dirLight = new DirectionalLight {
                            Ambient = new Color4(2.5f, 2.5f, 2.5f),
                            Diffuse = new Color4(0.0f, 0.0f, 0.0f),
                            Specular = new Color4(0.0f, 0.0f, 0.0f),
                            Direction = new Vector3(0.0f, -1.0f, 0.0f)
                        };

                        var pos = -_objectPos;
                        pos.Y = CarSize.Y + 1.0f;

                        _camera = new CameraOrtho() {
                            Position = pos,
                            FarZ = CarSize.Y + 10.0f,
                            Target = pos - Vector3.UnitY,
                            Width = CarSize.X,
                            Height = CarSize.Z
                        };

                        foreach (var obj in _objs) {
                            if (obj.Blen != null) {
                                obj.Blen.Dispose();

                                var transDesc = new BlendStateDescription {
                                    AlphaToCoverageEnable = false,
                                    IndependentBlendEnable = false
                                };

                                transDesc.RenderTargets[0].BlendEnable = true;
                                transDesc.RenderTargets[0].SourceBlend = BlendOption.Zero;
                                transDesc.RenderTargets[0].DestinationBlend = BlendOption.Zero;
                                transDesc.RenderTargets[0].BlendOperation = BlendOperation.Add;
                                transDesc.RenderTargets[0].SourceBlendAlpha = BlendOption.Zero;
                                transDesc.RenderTargets[0].DestinationBlendAlpha = BlendOption.Zero;
                                transDesc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
                                transDesc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;

                                obj.Blen = BlendState.FromDescription(CurrentDevice, transDesc);
                            }
                        }

                        _backgroundColor = new Color4(0.0f, 0.0f, 0.0f, 0.0f);
                    }
                    break;

                case VisualMode.SIMPLE_PREVIEW_SEAT_LEON_EUROCUP: {
                        _camera = new CameraOrbit(0.011f * MathF.PI) { Alpha = 0.0f, Beta = 0.0f, NearZ = 1.0f, Radius = 90.0f, Target = new Vector3(0, 0.78f, 0) };

                        _dirLight = new DirectionalLight {
                            Ambient = new Color4(0.65f, 0.66f, 0.64f),
                            Diffuse = new Color4(1.89f, 1.89f, 1.84f),
                            Specular = new Color4(2.95f, 2.96f, 2.13f),
                            Direction = new Vector3(-1.57735f, -0.57735f, 0.57735f)
                        };

                        _backgroundColor = new Color4(0.68f, 0.68f, 0.68f);
                        _reflectionCubemap = ShaderResourceView.FromMemory(CurrentDevice, Properties.Resources.TextureDarkRoomReflection);
                    }
                    break;

                case VisualMode.DARK_ROOM: {
                        _camera = new CameraOrbit(0.15f * MathF.PI) { Alpha = 1.1f, Beta = 0.021f, Radius = 5.4f, Target = new Vector3(0, 0.78f, 0) };

                        _dirLight = new DirectionalLight {
                            Ambient = new Color4(1.45f, 1.46f, 1.44f),
                            Diffuse = new Color4(2.24f, 2.23f, 2.20f),
                            Specular = new Color4(0.0f, 0.0f, 0.0f),
                            Direction = new Vector3(-1.57735f, -2.57735f, 0.57735f)
                        };

                        _backgroundColor = new Color4(0.0f, 0.0f, 0.0f);
                        _reflectionCubemap = ShaderResourceView.FromMemory(CurrentDevice, Properties.Resources.TextureDarkRoomReflection);

                        var size = CarSize;
                        var maxSize = System.Math.Max(size.X, size.Z) * 0.8f;
                        var pos = _objectPos;
                        pos.Y = 0;
                        CreateTexturedPlace(Matrix.Scaling(maxSize, 1.0f, maxSize) * Matrix.Translation(pos),
                            ShaderResourceView.FromMemory(CurrentDevice, Properties.Resources.TextureDarkRoomFloor));
                        LoadShadows();
                    }
                    break;

                case VisualMode.BRIGHT_ROOM: {
                        _camera = new CameraOrbit(0.15f * MathF.PI) { Alpha = 1.1f, Beta = 0.021f, Radius = 5.4f, Target = new Vector3(0, 0.78f, 0) };

                        _dirLight = new DirectionalLight {
                            Ambient = new Color4(2.36f, 2.36f, 2.36f),
                            Diffuse = new Color4(1.08f, 1.08f, 1.08f),
                            Specular = new Color4(0.0f, 0.0f, 0.0f),
                            Direction = new Vector3(1.57735f, -2.57735f, 0.57735f)
                        };

                        _backgroundColor = new Color4(0.9f, 0.92f, 0.97f);
                        _reflectionCubemap = ShaderResourceView.FromMemory(CurrentDevice, Properties.Resources.TextureDarkRoomReflection);
                    
                        var size = CarSize;
                        var maxSize = System.Math.Max(size.X, size.Z) * 0.8f;
                        var pos = _objectPos;
                        pos.Y = 0;
                        CreateTexturedPlace(Matrix.Scaling(maxSize, 1.0f, maxSize) * Matrix.Translation(pos),
                            ShaderResourceView.FromMemory(CurrentDevice, Properties.Resources.TextureDarkRoomFloor)).Visible = false;
                        LoadShadows();
                    }
                    break;

                case VisualMode.BODY_SHADOW: {
                        LoadShadowsSize();
                    }
                    return;

                case VisualMode.TRACK_MAP:
                    return;
            }

            LoadSkins();
            if (skinNumber >= 0 && skinNumber < Skins.Count) {
                LoadSkin(skinNumber);
            }

            LoadTextures();
        }

        void LoadMaterials() {
            _materials = new List<ShaderMaterial>(_kn5.Materials.Count);
            foreach (var mat in _kn5.Materials.Values) {
                var ksAmbient = mat.GetPropertyValueAByName("ksAmbient");
                var ksDiffuse = mat.GetPropertyValueAByName("ksDiffuse");
                var ksSpecular = mat.GetPropertyValueAByName("ksSpecular");
                var ksSpecularExp = mat.GetPropertyValueAByName("ksSpecularEXP");

                var fresnelC = mat.GetPropertyValueAByName("fresnelC");
                var fresnelExp = mat.GetPropertyValueAByName("fresnelEXP", 1.0f);
                var fresnelMaxLevel = mat.GetPropertyValueAByName("fresnelMaxLevel");
                var useDetail = mat.GetPropertyValueAByName("useDetail");
                var detailUvMultiplier = mat.GetPropertyValueAByName("detailUVMultiplier");

                var useMap = mat.GetMappingByName("txMaps") != null ? 1.0f : 0.0f;
                var useNormal = !mat.ShaderName.Contains("_damage") && mat.GetMappingByName("txNormal") != null ? 1.0f : 0.0f;

                _materials.Add(new ShaderMaterial {
                    Ambient = new Color4(1.0f, ksAmbient, ksAmbient, ksAmbient),
                    Diffuse = new Color4(1.0f, ksDiffuse, ksDiffuse, ksDiffuse),
                    Specular = new Color4(ksSpecularExp, ksSpecular, ksSpecular, ksSpecular),
                    Fresnel = new Color4(fresnelExp, fresnelC, fresnelC, fresnelC),
                    FresnelMax = fresnelMaxLevel,
                    MinAlpha = mat.BlendMode == Kn5MaterialBlendMode.AlphaBlend ? 0.03f : 1.0f,
                    UseDetail = useDetail,
                    UseNormal = useNormal,
                    UseMap = useMap,
                    DetailUVMultiplier = detailUvMultiplier
                });
            }
        }

        private void SortObjectsInRenderOrder() {
            _objs = _objs.OrderBy(x => x.Blen == null ? -1 : 1).ToList();
        }

        private void AlignObjectAtCenter() {
            _objectPos = new Vector3((_sceneBoundingBox.Minimum.X + _sceneBoundingBox.Maximum.X) / 2, _sceneBoundingBox.Minimum.Y,
                                  (_sceneBoundingBox.Minimum.Z + _sceneBoundingBox.Maximum.Z) / 2);
            var mat = Matrix.Translation(-_objectPos);
            foreach (var obj in _objs.OfType<MeshObjectKn5>()) {
                obj.Transform = obj.Transform * mat;
                obj.MeshBox.Minimum -= _objectPos;
                obj.MeshBox.Maximum -= _objectPos;
            }
        }

        private void GetObjects() {
#if !DEBUG
            try {
#endif
                _objs = new List<MeshObject>(200);
                GetObject(_kn5.RootNode, Matrix.Identity);
                SortObjectsInRenderOrder();

                if (_mode != VisualMode.TRACK_MAP) {
                    AlignObjectAtCenter();
                }
#if !DEBUG
            } catch (System.Exception ex) {
                MessageBox.Show(@"KN5 ERROR: " + ex);
            }
#endif
        }

        private Vector3 FixVector(Vector3 vec) {
            return new Vector3 { X = vec.X, Y = -vec.Z, Z = vec.Y };
        }

        private static Matrix ToMatrix(float[] mat4) {
            var matrix = new Matrix {
                M11 = mat4[0],
                M12 = mat4[1],
                M13 = mat4[2],
                M14 = mat4[3],
                M21 = mat4[4],
                M22 = mat4[5],
                M23 = mat4[6],
                M24 = mat4[7],
                M31 = mat4[8],
                M32 = mat4[9],
                M33 = mat4[10],
                M34 = mat4[11],
                M41 = mat4[12],
                M42 = mat4[13],
                M43 = mat4[14],
                M44 = mat4[15]
            };

            Vector3 translation, scale;
            Quaternion rotation;
            matrix.Decompose(out scale, out rotation, out translation);
            translation.X *= -1;
            var axis = rotation.Axis;
            axis.Y *= -1;
            axis.Z *= -1;
            rotation = Quaternion.RotationAxis(axis, rotation.Angle);
            return Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotation) * Matrix.Translation(translation);
        }

        private void GetObject(Kn5Node node, Matrix matrix) {
            if ((!node.Active || node.Name == "CINTURE_ON") && _mode != VisualMode.TRACK_MAP) {
                return;
            }

            if (node.NodeClass == Kn5NodeClass.Base) {
                matrix = ToMatrix(node.Transform) * matrix;
                foreach (var child in node.Children) {
                    GetObject(child, matrix);
                }

                switch (node.Name) {
                    case "WHEEL_LF":
                        _wheelLfPos = new Vector3(matrix.M41, matrix.M42, matrix.M43);
                        break;
                    case "WHEEL_RF":
                        _wheelRfPos = new Vector3(matrix.M41, matrix.M42, matrix.M43);
                        break;
                    case "WHEEL_LR":
                        _wheelLrPos = new Vector3(matrix.M41, matrix.M42, matrix.M43);
                        break;
                    case "WHEEL_RR":
                        _wheelRrPos = new Vector3(matrix.M41, matrix.M42, matrix.M43);
                        break;
                }
            } else if (_mode == VisualMode.TRACK_MAP || node.IsRenderable) {
                GetMeshObject(node, matrix);
            }
        }

        private void GetMeshObject(Kn5Node meshNode, Matrix matrix) {
            var obj = new MeshObjectKn5 {
                Name = meshNode.Name,
                OriginalNode = meshNode,
                Transform = matrix
            };

            obj.InvTransform = Matrix.Invert(Matrix.Transpose(obj.Transform));

            var bbMin = new Vector3(float.MaxValue);
            var bbMax = new Vector3(float.MinValue);

            var vertices = new VerticePNT[meshNode.Vertices.Length];
            for (var i = 0; i < vertices.Length; i++) {
                var vert = meshNode.Vertices[i];
                var pos = new Vector3(-vert.Co[0], vert.Co[1], vert.Co[2]);

                var posW = Matrix.Translation(pos) * matrix;
                if (posW.M41 < bbMin.X) bbMin.X = posW.M41;
                if (posW.M41 > bbMax.X) bbMax.X = posW.M41;
                if (posW.M42 < bbMin.Y) bbMin.Y = posW.M42;
                if (posW.M42 > bbMax.Y) bbMax.Y = posW.M42;
                if (posW.M43 < bbMin.Z) bbMin.Z = posW.M43;
                if (posW.M43 > bbMax.Z) bbMax.Z = posW.M43;

                vertices[i] = new VerticePNT {
                    Position = pos,
                    Normal = new Vector3(-vert.Normal[0], vert.Normal[1], vert.Normal[2]),
                    Tex = new Vector2(vert.Uv[0], vert.Uv[1])
                };
            }

            obj.MeshBox = new BoundingBox(bbMin, bbMax);

            if (_mode == VisualMode.BODY_SHADOW || vertices.Length > 10) {
                if (obj.MeshBox.Minimum.X < _sceneBoundingBox.Minimum.X) _sceneBoundingBox.Minimum.X = obj.MeshBox.Minimum.X;
                if (obj.MeshBox.Maximum.X > _sceneBoundingBox.Maximum.X) _sceneBoundingBox.Maximum.X = obj.MeshBox.Maximum.X;
                if (obj.MeshBox.Minimum.Y < _sceneBoundingBox.Minimum.Y) _sceneBoundingBox.Minimum.Y = obj.MeshBox.Minimum.Y;
                if (obj.MeshBox.Maximum.Y > _sceneBoundingBox.Maximum.Y) _sceneBoundingBox.Maximum.Y = obj.MeshBox.Maximum.Y;
                if (obj.MeshBox.Minimum.Z < _sceneBoundingBox.Minimum.Z) _sceneBoundingBox.Minimum.Z = obj.MeshBox.Minimum.Z;
                if (obj.MeshBox.Maximum.Z > _sceneBoundingBox.Maximum.Z) _sceneBoundingBox.Maximum.Z = obj.MeshBox.Maximum.Z;
            }

            var vbd = new BufferDescription(VerticePNT.Stride * vertices.Length, ResourceUsage.Immutable,
                BindFlags.VertexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            obj.Vb = new Buffer(CurrentDevice, new DataStream(vertices, false, false), vbd);

            var indices = new ushort[meshNode.Indices.Length];
            for (var i = 0; i < indices.Length; i += 3) {
                indices[i] = meshNode.Indices[i];
                indices[i + 1] = meshNode.Indices[i + 2];
                indices[i + 2] = meshNode.Indices[i + 1];
            }

            obj.IndCount = indices.Length;
            var ibd = new BufferDescription(sizeof(ushort) * obj.IndCount, ResourceUsage.Immutable,
                BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            obj.Ib = new Buffer(CurrentDevice, new DataStream(indices, false, false), ibd);

            var materialId = (int)meshNode.MaterialId;
            if (materialId < 0 || materialId >= _materials.Count) {
                materialId = 0;
            }

            obj.Mat = _materials[materialId];

            var mat = _kn5.Materials.Values.ElementAt(materialId);
            if (mat.BlendMode == Kn5MaterialBlendMode.AlphaBlend) {
                var transDesc = new BlendStateDescription {
                    AlphaToCoverageEnable = false,
                    IndependentBlendEnable = false
                };

                transDesc.RenderTargets[0].BlendEnable = true;
                transDesc.RenderTargets[0].SourceBlend = BlendOption.SourceAlpha;
                transDesc.RenderTargets[0].DestinationBlend = BlendOption.InverseSourceAlpha;
                transDesc.RenderTargets[0].BlendOperation = BlendOperation.Add;
                transDesc.RenderTargets[0].SourceBlendAlpha = BlendOption.One;
                transDesc.RenderTargets[0].DestinationBlendAlpha = BlendOption.One;
                transDesc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
                transDesc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;

                obj.Blen = BlendState.FromDescription(CurrentDevice, transDesc);
            }

            var diffuseMapping = mat.GetMappingByName("txDiffuse");
            obj.TexName = diffuseMapping == null ? null : diffuseMapping.Texture.ToLower();

            var detailMapping = mat.GetMappingByName("txDetail");
            obj.DetailTexName = detailMapping == null ? null : detailMapping.Texture.ToLower();

            var normalMapping = mat.GetMappingByName("txNormal");
            obj.NormalTexName = normalMapping == null ? null : normalMapping.Texture.ToLower();

            var mapMapping = mat.GetMappingByName("txMaps");
            obj.MapTexName = mapMapping == null ? null : mapMapping.Texture.ToLower();

            _objs.Add(obj);
        }

        MeshObject CreateTexturedPlace(Matrix transform, ShaderResourceView texture, bool shadowObj = false) {
            var obj = shadowObj ? new MeshObjectShadow() : new MeshObject();
            obj.Transform = transform;
            obj.InvTransform = Matrix.Invert(Matrix.Transpose(obj.Transform));

            var vertices = new VerticePT[4];
            for (var i = 0; i < vertices.Length; i++) {
                vertices[i] = new VerticePT(
                    new Vector3(i < 2 ? 1 : -1, 0, i % 2 == 0 ? -1 : 1),
                    new Vector2(i < 2 ? 1 : 0, i % 2)
                );
            }

            var vbd = new BufferDescription(VerticePT.Stride * vertices.Length, ResourceUsage.Immutable,
                BindFlags.VertexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            obj.Vb = new Buffer(CurrentDevice, new DataStream(vertices, false, false), vbd);

            var indices = new ushort[] { 0, 2, 1, 3, 1, 2 };

            obj.IndCount = indices.Length;
            var ibd = new BufferDescription(sizeof(ushort) * obj.IndCount, ResourceUsage.Immutable,
                BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            obj.Ib = new Buffer(CurrentDevice, new DataStream(indices, false, false), ibd);

            obj.Mat = new ShaderMaterial();
            obj.Rast = null;

            obj.Tex = texture;
            _objs.Add(obj);

            if (shadowObj) {
                var transDesc = new BlendStateDescription {
                    AlphaToCoverageEnable = false,
                    IndependentBlendEnable = false
                };

                transDesc.RenderTargets[0].BlendEnable = true;
                transDesc.RenderTargets[0].SourceBlend = BlendOption.SourceAlpha;
                transDesc.RenderTargets[0].DestinationBlend = BlendOption.InverseSourceAlpha;
                transDesc.RenderTargets[0].BlendOperation = BlendOperation.ReverseSubtract;
                transDesc.RenderTargets[0].SourceBlendAlpha = BlendOption.Zero;
                transDesc.RenderTargets[0].DestinationBlendAlpha = BlendOption.One;
                transDesc.RenderTargets[0].BlendOperationAlpha = BlendOperation.Add;
                transDesc.RenderTargets[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;

                obj.Blen = BlendState.FromDescription(CurrentDevice, transDesc);
            }

            return obj;
        }

        bool _ambientShadowSizeLoaded;
        public Vector3 AmbientBodyShadowSize, AmbientWheelShadowSize;

        void LoadShadowsSize() {
            try {
                var iniFile = new IniFile(Path.GetDirectoryName(_filename), "ambient_shadows.ini");
                AmbientBodyShadowSize = new Vector3(
                        (float)iniFile["SETTINGS"].GetDouble("WIDTH"), 1.0f,
                        (float)iniFile["SETTINGS"].GetDouble("LENGTH")
                        );
            } catch (System.Exception) {
                return;
            }

            AmbientWheelShadowSize = new Vector3(0.35f, 1.0f, 0.35f);
            _ambientShadowSizeLoaded = true;
        }

        void LoadShadows() {
            if (!_ambientShadowSizeLoaded) {
                LoadShadowsSize();
                if (!_ambientShadowSizeLoaded) return;
            }

            var shadowsDir = Path.GetDirectoryName(_filename) ?? "";

            var bodyShadow = Path.Combine(shadowsDir, "body_shadow.png");
            if (File.Exists(bodyShadow)) {
                var carPosZero = _objectPos;
                carPosZero.Y = 1e-3f;

                CreateTexturedPlace(Matrix.Scaling(AmbientBodyShadowSize) * Matrix.RotationY(MathF.PI) * Matrix.Translation(carPosZero),
                    ShaderResourceView.FromFile(CurrentDevice, bodyShadow), true);
            }

            for (var i = 0; i < 4; i++) {
                var tyreShadow = Path.Combine(shadowsDir, "tyre_" + i + "_shadow.png");
                if (!File.Exists(tyreShadow)) continue;

                var carPosZero = _objectPos;
                carPosZero.Y = 0;

                var wheelPos = i == 0 ? _wheelLfPos : i == 1 ? _wheelRfPos : i == 2 ? _wheelLrPos : _wheelRrPos;
                wheelPos.Y = 2e-3f;

                CreateTexturedPlace(Matrix.Scaling(AmbientWheelShadowSize) * Matrix.Translation(-carPosZero + wheelPos),
                                    ShaderResourceView.FromFile(CurrentDevice, tyreShadow), true);
            }
        }

        void LoadSkins() {
            SelectedSkin = -1;
            Skins = new List<string>();

            var skinsDir = Path.Combine(Path.GetDirectoryName(_filename), "skins");
            if (!Directory.Exists(skinsDir)) return;

            Skins = Directory.GetDirectories(skinsDir).ToList();
            Skins.Sort();
        }

        private SkinDirectoryWatcher _watcher;
        public void LoadSkin(int id, int attempt = 0) {
            if (Skins.Count == 0) return;

            try {
                var newSelectedSkin = (id + Skins.Count) % Skins.Count;

                if (newSelectedSkin != SelectedSkin && _watcher != null) {
                    _watcher.Update -= Watcher_Update;
                    _watcher.Dispose();
                    _watcher = null;
                }

                SelectedSkin = newSelectedSkin;
                var skinPath = Skins[SelectedSkin];

                Logging.Write("skin: " + skinPath);

                if (_overrides != null) {
                    foreach (var ov in _overrides) {
                        _textures[ov].Dispose();
                        _textures[ov] = null;
                    }

                    _overrides.Clear();
                } else {
                    _overrides = new List<string>();
                }

                if (!Directory.Exists(skinPath)) return;

                foreach (var file in Directory.GetFiles(skinPath)) {
                    if (File.GetAttributes(file).HasFlag(FileAttributes.Directory)) continue;

                    var key = Path.GetFileName(file).ToLower();
                    if (!ContainsTexture(key)) continue;

                    if (_textures.ContainsKey(key) && _textures[key] != null) {
                        _textures[key].Dispose();
                    }

                    _textures[key] = ShaderResourceView.FromFile(CurrentDevice, file);
                    _overrides.Add(key);
                }

                foreach (var key in _textures.Keys.ToList().Where(key => _textures[key] == null)) {
                    _textures[key] = ShaderResourceView.FromMemory(CurrentDevice, DefaultTexture(key));
                }

                if (_watcher != null) return;
                _watcher = new SkinDirectoryWatcher(skinPath);
                _watcher.Update += Watcher_Update;
            } catch (Exception) {
                if (attempt > 3) {
                    if (_form != null) {
                        Toast(@"Can't load skin: " + id);
                        Logging.Warning(@"Can't load skin: " + id);
                        if (_watcher != null) {
                            _watcher.Update -= Watcher_Update;
                            _watcher.Dispose();
                            _watcher = null;
                        }
                    }
                } else {
                    if (_attemptTimer == null) {
                        _attemptTimer = new System.Timers.Timer(300) { AutoReset = false };
                        _attemptTimer.Elapsed += (o, eventArgs) => {
                            LoadSkin(SelectedSkin);
                        };
                        _attemptTimer.Enabled = true;
                    }

                    _attemptTimer.Stop();
                    _attemptTimer.Start();
                }
            }
        }

        void ReloadTexture(string filename, byte[] data) {
            var key = Path.GetFileName(filename).ToLower();
            if (!ContainsTexture(key) && data != null) {
                if (!key.EndsWith(".psd") && !key.EndsWith(".xcf") && !key.EndsWith(".jpg") && !key.EndsWith(".jpeg") && 
                    !key.EndsWith(".png") && !key.EndsWith(".tiff") && !key.EndsWith(".tga") && !key.EndsWith(".bmp")) return;

                Toast("Updated: " + key);
                Logging.Write("reload texture using imagemagick: " + key);

                var nameOnly = key.Substring(0, key.Length - 4);
                var possibleDestinations = _kn5.Textures.Where(entry => entry.Key.ToLower() == nameOnly ||
                    Path.GetFileNameWithoutExtension(entry.Key).ToLower() == nameOnly).ToList();
                if (!possibleDestinations.Any()) {
                    Toast("Not appliable");
                    Logging.Warning("  texture isn't appliable");
                    return;
                }

                key = possibleDestinations[0].Key.ToLower();
                Logging.Write("  load in place of " + key);

                byte[] imageData;
                try {
                    imageData = MagickWrapper.LoadFromBytesAsSlimDxBuffer(data);
                } catch (Exception e) {
                    Toast("Can't read file");
                    Logging.Write("  can't read file: " + e);
                    return;
                }

                if (_textures.ContainsKey(key) && _textures[key] != null) {
                    if (_overrides.Contains(key)) {
                        _overrides.Remove(key);
                    }

                    _textures[key].Dispose();
                    _textures[key] = null;
                }
                
                Toast("Loaded: " + imageData.Length + " bytes");
                _textures[key] = ShaderResourceView.FromMemory(CurrentDevice, imageData);
                _overrides.Add(key);
                return;
            }
            
            Logging.Write("reload texture as usual: " + key);
            if (_textures.ContainsKey(key) && _textures[key] != null) {
                if (_overrides.Contains(key)) {
                    _overrides.Remove(key);
                }

                _textures[key].Dispose();
                _textures[key] = null;
            }

            if (data != null) {
                _textures[key] = ShaderResourceView.FromMemory(CurrentDevice, data);
                _overrides.Add(key);
            } else {
                _textures[key] = ShaderResourceView.FromMemory(CurrentDevice, DefaultTexture(key));
            }
        }

        private System.Timers.Timer _attemptTimer, _watcherTimer;
        private readonly Queue<string> _watcherQueue = new Queue<string>();

        void Watcher_Update(object sender, SkinUpdatedEventHandlerArgs args) {
            if (_watcherTimer == null) {
                _watcherTimer = new System.Timers.Timer(200) { AutoReset = true };
                _watcherTimer.Elapsed += (o, eventArgs) => {
                    if (_watcherQueue.Count == 0) return;

                    _watcherTimer.Enabled = false;

                    Action action = delegate {
                        var texture = _watcherQueue.Dequeue();

                        byte[] bytes;
                        try {
                            bytes = File.ReadAllBytes(texture);
                        } catch (Exception e) {
                            Toast("Can't read file: " + e.Message);
                            _watcherTimer.Enabled = true;
                            return;
                        }

                        ReloadTexture(texture, bytes);

                        _watcherTimer.Enabled = true;
                    };
                    _form.Invoke(action);
                };
                _watcherTimer.Enabled = true;
                _watcherTimer.Start();
            }

            var extension = Path.GetExtension(args.TextureFilename);
            if (extension == null || extension.ToLower() == ".tmp") return;

            if (!_watcherQueue.Contains(args.TextureFilename)) {
                _watcherQueue.Enqueue(args.TextureFilename);
            }
        }

        void LoadTextures() {
            foreach (var key in _kn5.Textures.Values.Select(x => x.Name.ToLower()).Where(key => !_textures.ContainsKey(key))) {
                try {
                    _textures[key] = ShaderResourceView.FromMemory(CurrentDevice, DefaultTexture(key));
                } catch (System.Exception) { }
            }
        }

        bool ContainsTexture(string key) {
            return _kn5.Textures.Any(entry => entry.Key.ToLower() == key);
        }

        byte[] DefaultTexture(string key) {
            return (from entry in _kn5.TexturesData where entry.Key.ToLower() == key select entry.Value).FirstOrDefault();
        }
    }
}
