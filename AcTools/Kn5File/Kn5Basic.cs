using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AcTools.DataFile;
using AcTools.Numerics;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public class Kn5Basic : IKn5 {
        public string OriginalFilename { get; }

        private Kn5Basic() {
            OriginalFilename = string.Empty;
            Header = new Kn5Header { Version = CommonAcConsts.Kn5ActualVersion };
            Textures = new Dictionary<string, Kn5Texture>();
            TexturesData = new Dictionary<string, byte[]>();
            Materials = new Dictionary<string, Kn5Material>();
        }

        private Kn5Basic(string filename) {
            OriginalFilename = filename;
        }

        private Kn5Header Header;

        public Dictionary<string, Kn5Texture> Textures { get; set; }

        public Dictionary<string, byte[]> TexturesData { get; set; }

        public Dictionary<string, Kn5Material> Materials { get; set; }

        public Kn5Node RootNode { get; set; }

        public bool IsEditable { get; private set; } = true;

        Kn5Material IKn5.GetMaterial(uint id) {
            return Materials.Values.ElementAtOrDefault((int)id);
        }

        public Kn5Node GetNode(string path) {
            var pieces = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var node = RootNode;

            for (var i = 0; i < pieces.Length; i++) {
                node = node?.GetByName(pieces[i]);
                if (node == null) return null;
            }

            return node;
        }

        [CanBeNull]
        private string GetObjectPath([NotNull] Kn5Node parent, [NotNull] Kn5Node node) {
            if (parent.Children.IndexOf(node) != -1) return node.Name;

            for (var i = 0; i < parent.Children.Count; i++) {
                var child = parent.Children[i];
                var v = GetObjectPath(child, node);
                if (v != null) {
                    return child.Name + '\\' + v;
                }
            }

            return null;
        }

        [CanBeNull]
        private string GetParentPath([NotNull] Kn5Node parent, [NotNull] Kn5Node child) {
            if (!TraverseDown(parent, "", out var ret)) {
                throw new Exception("Failed to traverse down");
            }
            return ret;

            bool TraverseDown(Kn5Node node, string s, out string r) {
                foreach (var c in node.Children) {
                    if (c == child) {
                        r = s;
                        return true;
                    }
                    if (c.NodeClass == Kn5NodeClass.Base && TraverseDown(c, $"{s}\\{c.Name}", out r)) {
                        return true;
                    }
                }
                r = null;
                return false;
            }
        }

        public string GetObjectPath(Kn5Node node) {
            return GetObjectPath(RootNode, node);
        }

        public string GetParentPath(Kn5Node node) {
            return GetParentPath(RootNode, node);
        }

        public IEnumerable<Kn5Node> Nodes {
            get {
                var queue = new Queue<Kn5Node>();
                if (RootNode != null) {
                    queue.Enqueue(RootNode);
                }

                while (queue.Count > 0) {
                    var next = queue.Dequeue();

                    if (next.NodeClass == Kn5NodeClass.Base) {
                        foreach (var child in next.Children) {
                            queue.Enqueue(child);
                        }
                    }

                    yield return next;
                }
            }
        }

        public Kn5Node FirstByName(string name) {
            return Nodes.FirstOrDefault(node => node.Name == name);
        }

        public int RemoveAllByName(Kn5Node node, string name) {
            var result = 0;
            for (var i = 0; i < node.Children.Count; i++) {
                var child = node.Children[i];
                if (child.Name == name) {
                    node.Children.Remove(child);
                    result++;
                } else if (child.NodeClass == Kn5NodeClass.Base) {
                    result += RemoveAllByName(child, name);
                }
            }

            return result;
        }

        public int RemoveAllByName(string name) {
            return RemoveAllByName(RootNode, name);
        }

        public Kn5Node FirstFiltered(Func<Kn5Node, bool> filter) {
            return Nodes.FirstOrDefault(filter);
        }

        public void SetTexture([Localizable(false)] string textureName, string filename) {
            var bytes = File.ReadAllBytes(filename);
            Textures[textureName] = new Kn5Texture {
                Active = true,
                Name = textureName,
                Length = bytes.Length
            };
            TexturesData[textureName] = bytes;
        }

        public void ExportTextures(string textureDir) {
            foreach (var texture in Textures.Values) {
                File.WriteAllBytes(Path.Combine(textureDir, texture.Name), TexturesData[texture.Name]);
            }
        }

        public async Task ExportTexturesAsync(string textureDir, IProgress<string> progress, CancellationToken cancellation) {
            foreach (var texture in Textures.Values) {
                if (cancellation.IsCancellationRequested) return;
                progress?.Report(texture.Name);
                await FileUtils.WriteAllBytesAsync(Path.Combine(textureDir, texture.Name), TexturesData[texture.Name], cancellation);
            }
        }

        private static string GetFbxConverterLocation() {
            if (Kn5.FbxConverterLocation != null) return Kn5.FbxConverterLocation;
            var location = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? "");
            var fbxConverter = Path.Combine(location ?? "", "FbxConverter.exe");
            return fbxConverter;
        }

        public void ExportFbx(string filename) {
            var colladaFilename = filename + ".dae";
            ExportCollada(colladaFilename);
            ConvertColladaToFbx(colladaFilename, filename);
        }

        public void ConvertColladaToFbx(string colladaFilename, string fbxFilename) {
            var process = new Process();
            var outputStringBuilder = new StringBuilder();

            try {
                var arguments = "\"" + Path.GetFileName(colladaFilename) + "\" \"" + Path.GetFileName(fbxFilename) + "\" /sffCOLLADA /dffFBX /f201300";
                var colladaLocation = Path.GetDirectoryName(fbxFilename);
                process.StartInfo.FileName = GetFbxConverterLocation();
                process.StartInfo.WorkingDirectory = colladaLocation ?? "";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.EnableRaisingEvents = false;
                process.OutputDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
                process.ErrorDataReceived += (sender, eventArgs) => outputStringBuilder.AppendLine(eventArgs.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var processExited = process.WaitForExit(60000);

                if (processExited == false) {
                    process.Kill();
                    throw new Exception("FbxConverter took too long to finish");
                }

                // Console.WriteLine("\nAutodesk FBX Converter:\n    Arguments: {0}\n    Exit code: {1}\n{2}",
                //        arguments, process.ExitCode, outputStringBuilder.ToString().Trim().Replace("\n", "\n    "));

                if (process.ExitCode == 0 && colladaFilename != null) {
                    File.Delete(colladaFilename);
                }
            } finally {
                process.Close();
            }
        }

        private bool _uniqueNamesSet;

        private void EnsureUniqueNamesSet() {
            if (_uniqueNamesSet) return;
            _uniqueNamesSet = true;

            var names = new List<string>();
            foreach (var node in Nodes) {
                if (node.NodeClass == Kn5NodeClass.Mesh || node.NodeClass == Kn5NodeClass.SkinnedMesh) {
                    var name = node.Name;
                    for (var i = 1; names.Contains(name) && i < 1000; i++) {
                        name = $"{node.Name}-{i}";
                    }

                    node.UniqueName = name;
                    names.Add(name);
                } else {
                    node.UniqueName = node.Name;
                }
            }
        }

        public void ExportCollada(string filename) {
            EnsureUniqueNamesSet();

            using (var xml = XmlWriter.Create(filename, new XmlWriterSettings {
                Indent = true,
                Encoding = Encoding.UTF8
            })) {
                xml.WriteStartDocument();
                xml.WriteStartElement("COLLADA", "http://www.collada.org/2005/11/COLLADASchema");
                xml.WriteAttributeString("version", "1.4.1");

                xml.WriteStartElement("asset");
                xml.WriteStartElement("contributor");
                xml.WriteElementStringSafe("author", Environment.UserName);
                xml.WriteElementStringSafe("authoring_tool", AcToolsInformation.Name);
                xml.WriteEndElement();
                if (!string.IsNullOrEmpty(OriginalFilename)) {
                    xml.WriteElementStringSafe("created", new FileInfo(OriginalFilename).CreationTime.ToString(CultureInfo.InvariantCulture));
                    xml.WriteElementStringSafe("modified", new FileInfo(OriginalFilename).LastWriteTime.ToString(CultureInfo.InvariantCulture));
                }
                xml.WriteElement("unit", "name", "meter", "meter", 1);
                xml.WriteElementString("up_axis", "Y_UP");
                xml.WriteEndElement();

                xml.WriteElement("library_cameras");
                xml.WriteElement("library_lights");

                xml.WriteStartElement("library_images");
                foreach (var texture in Textures.Values) {
                    ExportCollada_Texture(xml, texture);
                }
                xml.WriteEndElement();

                xml.WriteStartElement("library_effects");
                foreach (var material in Materials.Values) {
                    ExportCollada_MaterialEffect(xml, material);
                }
                xml.WriteEndElement();

                xml.WriteStartElement("library_materials");
                foreach (var material in Materials.Values) {
                    ExportCollada_Material(xml, material);
                }
                xml.WriteEndElement();

                xml.WriteStartElement("library_geometries");
                ExportCollada_MeshWrapper(xml, RootNode);
                xml.WriteEndElement();

                xml.WriteStartElement("library_controllers");
                ExportCollada_Skinned(xml, RootNode);
                xml.WriteEndElement();

                xml.WriteStartElement("library_visual_scenes");
                ExportCollada_Scene(xml);
                xml.WriteEndElement();

                xml.WriteStartElement("scene");
                xml.WriteElement("instance_visual_scene", "url", "#Scene");
                xml.WriteEndElement();

                xml.WriteEndElement();
                xml.WriteEndDocument();
            }
        }

        private void ExportCollada_Texture(XmlWriter xml, Kn5Texture texture) {
            xml.WriteStartElement("image");
            xml.WriteAttributeStringSafe("id", $"{texture.Name}-image");
            xml.WriteAttributeStringSafe("name", texture.Name);

            xml.WriteStartElement("init_from");
            xml.WriteString("file://texture/" + Uri.EscapeUriString(texture.Name));
            xml.WriteEndElement();

            xml.WriteEndElement();
        }

        private void ExportCollada_MaterialEffectTexture(XmlWriter xml, Kn5Material.TextureMapping tex) {
            xml.WriteStartElement("texture");
            xml.WriteAttributeStringSafe("texture", $"{tex.Texture}-image");
            xml.WriteAttributeString("texcoord", "CHANNEL0");

            xml.WriteStartElement("extra");
            xml.WriteStartElement("technique");
            xml.WriteAttributeString("profile", "MAYA");

            xml.WriteStartElement("wrapV");
            xml.WriteAttributeString("sid", "wrapV0");
            xml.WriteString("TRUE");
            xml.WriteEndElement();

            xml.WriteStartElement("wrapU");
            xml.WriteAttributeString("sid", "wrapU0");
            xml.WriteString("TRUE");
            xml.WriteEndElement();

            xml.WriteStartElement("blend_mode");
            xml.WriteString("ADD");
            xml.WriteEndElement();

            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteEndElement();
        }

        private void ExportCollada_MaterialEffect(XmlWriter xml, Kn5Material material) {
            xml.WriteStartElement("effect");
            xml.WriteAttributeStringSafe("id", $"{material.Name}-effect");
            xml.WriteStartElement("profile_COMMON");
            xml.WriteStartElement("technique");
            xml.WriteAttributeString("sid", "common");
            xml.WriteStartElement("phong");

            xml.WriteStartElement("emission");
            xml.WriteStartElement("color");
            xml.WriteAttributeString("sid", "emission");
            var ksEmissive = material.GetPropertyByName("ksEmissive");
            xml.WriteString(ksEmissive == null ? "0 0 0 1" :
                    $"{ksEmissive.ValueC.X.ToString(CultureInfo.InvariantCulture)} {ksEmissive.ValueC.Y.ToString(CultureInfo.InvariantCulture)} {ksEmissive.ValueC.Z.ToString(CultureInfo.InvariantCulture)} 1");
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteStartElement("ambient");
            xml.WriteStartElement("color");
            xml.WriteAttributeString("sid", "ambient");
            var ksAmbient = material.GetPropertyByName("ksAmbient");
            xml.WriteString(ksAmbient == null ? "0.4 0.4 0.4 1" : string.Format("{0} {0} {0} 1", ksAmbient.ValueA.ToString(CultureInfo.InvariantCulture)));
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteStartElement("diffuse");
            var txDiffuse = material.GetMappingByName("txDiffuse");
            if (txDiffuse != null) {
                ExportCollada_MaterialEffectTexture(xml, txDiffuse);
            }

            xml.WriteStartElement("color");
            xml.WriteAttributeString("sid", "diffuse");
            var ksDiffuse = material.GetPropertyByName("ksDiffuse");
            xml.WriteString(ksDiffuse == null ? "0.6 0.6 0.6 1" : string.Format("{0} {0} {0} 1", ksDiffuse.ValueA.ToString(CultureInfo.InvariantCulture)));
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteStartElement("specular");
            xml.WriteStartElement("color");
            xml.WriteAttributeString("sid", "specular");
            var ksSpecular = material.GetPropertyByName("ksSpecular");
            xml.WriteString(ksSpecular == null ? "0.5 0.5 0.5 1" : string.Format("{0} {0} {0} 1", ksSpecular.ValueA.ToString(CultureInfo.InvariantCulture)));
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteStartElement("shininess");
            xml.WriteStartElement("float");
            xml.WriteAttributeString("sid", "shininess");
            xml.WriteString(material.GetPropertyByName("ksSpecularEXP")?.ValueA ?? 50);
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteStartElement("index_of_refraction");
            xml.WriteStartElement("float");
            xml.WriteAttributeString("sid", "index_of_refraction");
            xml.WriteString(1f);
            xml.WriteEndElement();
            xml.WriteEndElement();

            if (material.BlendMode == Kn5MaterialBlendMode.AlphaBlend) {
                xml.WriteStartElement("transparent");
                xml.WriteAttributeString("opaque", "RGB_ZERO");

                if (material.ShaderName == "ksPerPixelNM" || material.ShaderName == "ksPerPixelNM_UV2" ||
                        material.ShaderName == "ksPerPixelMultiMap_AT" || material.ShaderName == "ksPerPixelMultiMap_AT_NMDetail") {
                    var txNormal = material.GetMappingByName("txNormal");
                    if (txNormal != null) {
                        ExportCollada_MaterialEffectTexture(xml, txNormal);
                    }
                } else if (txDiffuse != null) {
                    ExportCollada_MaterialEffectTexture(xml, txDiffuse);
                }

                xml.WriteEndElement(); // transparent
            }

            var alpha = 1.0f;
            switch (material.ShaderName) {
                case "ksBrokenGlass":
                    alpha = 0f;
                    break;
                case "ksPerPixelAlpha":
                    alpha = material.GetPropertyByName("alpha")?.ValueA ?? 1f;
                    break;
            }

            if (!Equals(alpha, 1f)) {
                xml.WriteStartElement("transparency");
                xml.WriteStartElement("float");
                xml.WriteAttributeString("sid", "transparency");
                xml.WriteString(alpha);
                xml.WriteEndElement(); // float
                xml.WriteEndElement(); // transparency
            }

            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        private void ExportCollada_Material(XmlWriter xml, Kn5Material material) {
            xml.WriteStartElement("material");
            xml.WriteAttributeStringSafe("id", $"{material.Name}-material");
            xml.WriteAttributeStringSafe("name", material.Name);

            xml.WriteStartElement("instance_effect");
            xml.WriteAttributeStringSafe("url", $"#{material.Name}-effect");
            xml.WriteEndElement();

            xml.WriteEndElement();
        }

        private void ExportCollada_Skinned(XmlWriter xml, Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    foreach (var child in node.Children) {
                        ExportCollada_Skinned(xml, child);
                    }
                    break;

                case Kn5NodeClass.Mesh:
                    return;

                case Kn5NodeClass.SkinnedMesh:
                    xml.WriteStartElement("controller");
                    xml.WriteAttributeStringSafe("id", $"{node.UniqueName}Controller");

                    xml.WriteStartElement("skin");
                    xml.WriteAttributeStringSafe("source", $"#{node.UniqueName}-mesh");

                    // xml.WriteElementString("bind_shape_matrix", "1 0 0 0 0 0 1 0 0 -1 0 0 0 0 0 1");
                    xml.WriteElementString("bind_shape_matrix", "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1");

                    // Joints
                    xml.WriteStartElement("source");
                    xml.WriteAttributeStringSafe("id", $"{node.UniqueName}Controller-Joints");

                    xml.WriteStartElement("Name_array");
                    xml.WriteAttributeStringSafe("id", $"{node.UniqueName}Controller-Joints-array");
                    xml.WriteAttributeString("count", node.Bones.Length);
                    xml.WriteString(node.Bones.Select(x => x.Name).JoinToString(" "));
                    xml.WriteEndElement(); // Name_array

                    xml.WriteStartElement("technique_common");
                    xml.WriteStartElement("accessor");
                    xml.WriteAttributeStringSafe("source", $"#{node.UniqueName}Controller-Joints-array");
                    xml.WriteAttributeString("count", node.Bones.Length);
                    xml.WriteAttributeString("stride", 1);
                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "JOINT");
                    xml.WriteAttributeString("type", "name");
                    xml.WriteEndElement(); // param
                    xml.WriteEndElement(); // accessor
                    xml.WriteEndElement(); // technique_common
                    xml.WriteEndElement(); // source (Joints)

                    // Matrices
                    xml.WriteStartElement("source");
                    xml.WriteAttributeStringSafe("id", $"{node.UniqueName}Controller-Matrices");

                    xml.WriteStartElement("float_array");
                    xml.WriteAttributeStringSafe("id", $"{node.UniqueName}Controller-Matrices-array");
                    xml.WriteAttributeString("count", node.Bones.Length * 16);
                    xml.WriteString(node.Bones.Select(x => XmlWriterExtension.MatrixToCollada(x.Transform)).JoinToString(" "));
                    xml.WriteEndElement(); // float_array

                    xml.WriteStartElement("technique_common");
                    xml.WriteStartElement("accessor");
                    xml.WriteAttributeStringSafe("source", $"#{node.UniqueName}Controller-Matrices-array");
                    xml.WriteAttributeString("count", node.Bones.Length);
                    xml.WriteAttributeString("stride", 16);
                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "TRANSFORM");
                    xml.WriteAttributeString("type", "float4x4");
                    xml.WriteEndElement(); // param
                    xml.WriteEndElement(); // accessor
                    xml.WriteEndElement(); // technique_common
                    xml.WriteEndElement(); // source (Matrices)

                    // Weights
                    xml.WriteStartElement("source");
                    xml.WriteAttributeStringSafe("id", $"{node.UniqueName}Controller-Weights");

                    xml.WriteStartElement("float_array");
                    xml.WriteAttributeStringSafe("id", $"{node.UniqueName}Controller-Weights-array");

                    var weights = new StringBuilder();
                    var weightsVCount = new StringBuilder();
                    var weightsV = new StringBuilder();

                    var weightsCount = 0;
                    foreach (var w in node.VerticeWeights) {
                        AddWeight(w.Indices.X, w.Weights.X);
                        AddWeight(w.Indices.Y, w.Weights.Y);
                        AddWeight(w.Indices.Z, w.Weights.Z);
                        AddWeight(w.Indices.W, w.Weights.W);

                        var vcount = 0;
                        if (w.Weights.X != 0f) {
                            ++vcount;
                        }
                        if (w.Weights.Y != 0f) {
                            ++vcount;
                        }
                        if (w.Weights.Z != 0f) {
                            ++vcount;
                        }
                        if (w.Weights.W != 0f) {
                            ++vcount;
                        }
                        weightsVCount.Append(vcount);
                        weightsVCount.Append(' ');
                    }

                    void AddWeight(float index, float weight) {
                        if (index != -1f) {
                            weights.Append(weight);
                            weights.Append(' ');
                            weightsV.Append(index);
                            weightsV.Append(' ');
                            weightsV.Append(weightsCount);
                            weightsV.Append(' ');
                            ++weightsCount;
                        }
                    }

                    xml.WriteAttributeString("count", weightsCount);
                    xml.WriteString(weights.ToString(0, weights.Length > 0 ? weights.Length - 1 : 0));
                    xml.WriteEndElement(); // float_array

                    xml.WriteStartElement("technique_common");
                    xml.WriteStartElement("accessor");
                    xml.WriteAttributeStringSafe("source", $"#{node.UniqueName}Controller-Weights-array");
                    xml.WriteAttributeString("count", weightsCount);
                    xml.WriteAttributeString("stride", 1);
                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "WEIGHT");
                    xml.WriteAttributeString("type", "float");
                    xml.WriteEndElement(); // param
                    xml.WriteEndElement(); // accessor
                    xml.WriteEndElement(); // technique_common
                    xml.WriteEndElement(); // source (Matrices)

                    xml.WriteStartElement("joints");
                    xml.WriteStartElement("input");
                    xml.WriteAttributeString("semantic", "JOINT");
                    xml.WriteAttributeStringSafe("source", $"#{node.UniqueName}Controller-Joints");
                    xml.WriteEndElement(); // input
                    xml.WriteStartElement("input");
                    xml.WriteAttributeString("semantic", "INV_BIND_MATRIX");
                    xml.WriteAttributeStringSafe("source", $"#{node.UniqueName}Controller-Matrices");
                    xml.WriteEndElement(); // input
                    xml.WriteEndElement(); // joints

                    xml.WriteStartElement("vertex_weights");
                    xml.WriteAttributeString("count", node.Vertices.Length);

                    xml.WriteStartElement("input");
                    xml.WriteAttributeString("semantic", "JOINT");
                    xml.WriteAttributeString("offset", 0);
                    xml.WriteAttributeStringSafe("source", $"#{node.UniqueName}Controller-Joints");
                    xml.WriteEndElement(); // input
                    xml.WriteStartElement("input");
                    xml.WriteAttributeString("semantic", "WEIGHT");
                    xml.WriteAttributeString("offset", 1);
                    xml.WriteAttributeStringSafe("source", $"#{node.UniqueName}Controller-Weights");
                    xml.WriteEndElement(); // input

                    xml.WriteElementString("vcount", weightsVCount.ToString(0, weightsVCount.Length == 0 ? 0 : weightsVCount.Length - 1));
                    xml.WriteElementString("v", weightsV.ToString(0, weightsV.Length == 0 ? 0 : weightsV.Length - 1));

                    xml.WriteEndElement(); // vertex_weights

                    xml.WriteEndElement(); // skin
                    xml.WriteEndElement(); // controller
                    break;
            }
        }

        private void ExportCollada_Mesh(XmlWriter xml, string name, IReadOnlyList<Kn5Node> unsorted) {
            // AcToolsLogging.Write($"{name}: {unsorted.Sum(x => x.Vertices.Length)} vertices, {unsorted.Sum(x => x.Indices.Length / 3d)} triangles");

            string Vec2ToString(IEnumerable<Vec2> vecs) {
                var s = new StringBuilder();
                foreach (var vec in vecs) {
                    if (s.Length > 0) {
                        s.Append(' ');
                    }
                    s.Append(vec.X);
                    s.Append(' ');
                    s.Append(vec.Y);
                }
                return s.ToString();
            }

            string Vec2ToString4(IEnumerable<Vec2> vecs) {
                var s = new StringBuilder();
                foreach (var vec in vecs) {
                    if (s.Length > 0) {
                        s.Append(' ');
                    }
                    s.Append(vec.X);
                    s.Append(' ');
                    s.Append(vec.Y);
                    s.Append(" 0 0");
                }
                return s.ToString();
            }

            string Vec3ToString(IEnumerable<Vec3> vecs) {
                var s = new StringBuilder();
                foreach (var vec in vecs) {
                    if (s.Length > 0) {
                        s.Append(' ');
                    }
                    s.Append(vec.X);
                    s.Append(' ');
                    s.Append(vec.Y);
                    s.Append(' ');
                    s.Append(vec.Z);
                }
                return s.ToString();
            }

            xml.WriteStartElement("geometry");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh");
            xml.WriteAttributeStringSafe("name", name);

            xml.WriteStartElement("mesh");

            var nodes = unsorted.Count == 1 ? unsorted :
                    unsorted.OrderBy(x => int.Parse(x.UniqueName.Split(new[] { "_SUB" }, StringSplitOptions.None).Last())).ToList();

            /* coordinates */
            var vertexCount = nodes.Sum(x => x.Vertices.Length);
            xml.WriteStartElement("source");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-positions");
            xml.WriteStartElement("float_array");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-positions-array");
            xml.WriteAttributeString("count", vertexCount * 3);
            xml.WriteString(Vec3ToString(nodes.SelectMany(x => x.Vertices).Select(x => x.Position)));
            xml.WriteEndElement(); // float_array

            xml.WriteStartElement("technique_common");
            xml.WriteStartElement("accessor");
            xml.WriteAttributeStringSafe("source", $"#{name}-mesh-positions-array");
            xml.WriteAttributeString("count", vertexCount);
            xml.WriteAttributeString("stride", 3);

            xml.WriteElement("param", "name", "X", "type", "float");
            xml.WriteElement("param", "name", "Y", "type", "float");
            xml.WriteElement("param", "name", "Z", "type", "float");

            xml.WriteEndElement(); // accessor
            xml.WriteEndElement(); // technique_common
            xml.WriteEndElement(); // source

            /* normals */
            xml.WriteStartElement("source");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-normals");
            xml.WriteStartElement("float_array");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-normals-array");
            xml.WriteAttributeString("count", vertexCount * 3);
            xml.WriteString(Vec3ToString(nodes.SelectMany(x => x.Vertices).Select(x => x.Normal)));
            xml.WriteEndElement(); // float_array

            xml.WriteStartElement("technique_common");
            xml.WriteStartElement("accessor");
            xml.WriteAttributeStringSafe("source", $"#{name}-mesh-normals-array");
            xml.WriteAttributeString("count", vertexCount);
            xml.WriteAttributeString("stride", "3");

            xml.WriteElement("param", "name", "X", "type", "float");
            xml.WriteElement("param", "name", "Y", "type", "float");
            xml.WriteElement("param", "name", "Z", "type", "float");

            xml.WriteEndElement(); // accessor
            xml.WriteEndElement(); // technique_common
            xml.WriteEndElement(); // source

            /* uv */
            xml.WriteStartElement("source");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-map-0");
            xml.WriteStartElement("float_array");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-map-0-array");
            xml.WriteAttributeString("count", vertexCount * 2);
            xml.WriteString(Vec2ToString(nodes.SelectMany(x => x.Vertices).Select(x => new Vec2(x.Tex.X, -x.Tex.Y))));
            xml.WriteEndElement(); // float_array

            xml.WriteStartElement("technique_common");
            xml.WriteStartElement("accessor");
            xml.WriteAttributeStringSafe("source", $"#{name}-mesh-map-0-array");
            xml.WriteAttributeString("count", vertexCount);
            xml.WriteAttributeString("stride", 2);

            xml.WriteElement("param", "name", "S", "type", "float");
            xml.WriteElement("param", "name", "T", "type", "float");

            xml.WriteEndElement(); // accessor
            xml.WriteEndElement(); // technique_common
            xml.WriteEndElement(); // source

            var hasUv2 = nodes.Any(n => n.Uv2 != null);
            if (hasUv2) {
                AcToolsLogging.Write("UV2: " + nodes.Select(x => x.Name).JoinToString("; "));
                if (nodes.Any(n => n.Uv2 == null)) {
                    throw new Exception($"Can’t export KN5 into COLLADA with UV2 only applied to some of meshes in a group {name}");
                }

                /* uv 2 */
                xml.WriteStartElement("source");
                xml.WriteAttributeStringSafe("id", $"{name}-colors-Col");
                xml.WriteStartElement("float_array");
                xml.WriteAttributeStringSafe("id", $"{name}-colors-Col-array");
                xml.WriteAttributeString("count", vertexCount * 2);
                xml.WriteString(Vec2ToString4(nodes.SelectMany(x => x.Uv2)));
                xml.WriteEndElement(); // float_array

                xml.WriteStartElement("technique_common");
                xml.WriteStartElement("accessor");
                xml.WriteAttributeStringSafe("source", $"#{name}-colors-Col-array");
                xml.WriteAttributeString("count", vertexCount);
                xml.WriteAttributeString("stride", 4);

                xml.WriteElement("param", "name", "R", "type", "float");
                xml.WriteElement("param", "name", "G", "type", "float");
                xml.WriteElement("param", "name", "B", "type", "float");
                xml.WriteElement("param", "name", "A", "type", "float");

                xml.WriteEndElement(); // accessor
                xml.WriteEndElement(); // technique_common
                xml.WriteEndElement(); // source
            }

            /* vertices */
            xml.WriteStartElement("vertices");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-vertices");
            xml.WriteElement("input", "semantic", "POSITION", "source", $"#{name}-mesh-positions");
            xml.WriteEndElement();

            /* triangles */
            var offset = 0;
            foreach (var node in nodes) {
                xml.WriteStartElement("triangles");
                xml.WriteAttributeStringSafe("original_node", $"{node.UniqueName}");
                xml.WriteAttributeStringSafe("material", $"{RequireMaterial(node.MaterialId).Name}-material");
                xml.WriteAttributeString("count", node.Indices.Length / 3);

                xml.WriteElement("input", "semantic", "VERTEX", "source", $"#{name}-mesh-vertices", "offset", 0);
                xml.WriteElement("input", "semantic", "NORMAL", "source", $"#{name}-mesh-normals", "offset", 1);
                xml.WriteElement("input", "semantic", "TEXCOORD", "source", $"#{name}-mesh-map-0", "offset", 2, "set", 0);
                if (hasUv2) {
                    xml.WriteElement("input", "semantic", "COLOR", "source", $"#{name}-colors-Col", "offset", 3, "set", 0);
                }

                var inner = offset;
                xml.WriteElementString("p", hasUv2
                        ? node.Indices.SelectMany(x => new[] { x + inner, x + inner, x + inner, x + inner }).JoinToString(" ")
                        : node.Indices.SelectMany(x => new[] { x + inner, x + inner, x + inner }).JoinToString(" "));
                xml.WriteEndElement(); // triangles

                offset += node.Vertices.Length;
            }

            xml.WriteEndElement(); // mesh
            xml.WriteEndElement(); // geometry
        }

        [NotNull]
        private Kn5Material RequireMaterial(uint index) {
            return Materials.Values.ElementAtOrDefault((int)index) ?? throw new Exception($"Material is missing: {index}");
        }

        private static bool IsMultiMaterial(Kn5Node node) {
            if (!Kn5.OptionJoinToMultiMaterial || node.NodeClass != Kn5NodeClass.Base || !node.Children.Any()) return false;
            var regex = new Regex($@"^{Regex.Escape(node.Name)}_SUB\d+$", RegexOptions.Compiled);
            return node.Children.All(x => x.NodeClass == Kn5NodeClass.Mesh && regex.IsMatch(x.Name));
        }

        private void ExportCollada_MeshWrapper(XmlWriter xml, Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    if (IsMultiMaterial(node)) {
                        ExportCollada_Mesh(xml, node.UniqueName, node.Children);
                    } else {
                        foreach (var child in node.Children) {
                            ExportCollada_MeshWrapper(xml, child);
                        }
                    }
                    break;

                case Kn5NodeClass.Mesh:
                case Kn5NodeClass.SkinnedMesh:
                    ExportCollada_Mesh(xml, node.UniqueName, new[] { node });
                    break;
            }
        }

        private void ExportCollada_Scene(XmlWriter xml) {
            xml.WriteStartElement("visual_scene");
            xml.WriteAttributeString("id", "Scene");
            xml.WriteAttributeString("name", "Scene");
            ExportCollada_Node(xml, RootNode);

            xml.WriteStartElement("evaluate_scene");
            xml.WriteStartElement("render");
            xml.WriteElementString("layer", "Visible");
            xml.WriteEndElement(); // render
            xml.WriteEndElement(); // evaluate_scene

            xml.WriteEndElement(); // visual_scene
        }

        private void ExportCollada_Node(XmlWriter xml, Kn5Node node) {
            var boneNames = node.Children.SelectManyRecursive(x => x.Children)
                    .Where(x => x.NodeClass == Kn5NodeClass.SkinnedMesh)
                    .SelectMany(x => x.Bones.Select(y => y.Name))
                    .ToList();
            foreach (var t in node.Children) {
                ExportCollada_NodeSub(xml, boneNames, t);
            }
        }

        private void ExportCollada_NodeSub_BindMaterial(XmlWriter xml, params uint[] materialId) {
            xml.WriteStartElement("bind_material");
            xml.WriteStartElement("technique_common");

            foreach (var materialName in materialId.Select(u => RequireMaterial(u).Name)) {
                xml.WriteElement("instance_material",
                        "symbol", $"{materialName}-material",
                        "target", $"#{materialName}-material");
            }

            xml.WriteEndElement(); // technique_common
            xml.WriteEndElement(); // bind_material
        }

        private void ExportCollada_NodeSub_Inner(XmlWriter xml, IReadOnlyList<string> boneNames, Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    if (node.Children.Count == 1 && node.Children[0].NodeClass != Kn5NodeClass.Base
                            && node.Children[0].Name == node.Name) {
                        ExportCollada_NodeSub_Inner(xml, boneNames, node.Children[0]);
                    } else {
                        foreach (var t in node.Children) {
                            ExportCollada_NodeSub(xml, boneNames, t);
                        }
                    }
                    break;

                case Kn5NodeClass.Mesh:
                    xml.WriteStartElement("instance_geometry");
                    xml.WriteAttributeStringSafe("url", $"#{node.UniqueName}-mesh");
                    ExportCollada_NodeSub_BindMaterial(xml, node.MaterialId);
                    xml.WriteEndElement();
                    break;

                case Kn5NodeClass.SkinnedMesh:
                    xml.WriteStartElement("instance_controller");
                    xml.WriteAttributeStringSafe("url", $"#{node.UniqueName}Controller");
                    ExportCollada_NodeSub_BindMaterial(xml, node.MaterialId);

                    foreach (var bone in node.Bones) {
                        xml.WriteElementString("skeleton", $"#{bone.Name}");
                    }

                    xml.WriteEndElement(); // instance_controller
                    break;
            }
        }

        private void ExportCollada_NodeSub(XmlWriter xml, IReadOnlyList<string> boneNames, Kn5Node node) {
            xml.WriteStartElement("node");
            xml.WriteAttributeStringSafe("id", node.UniqueName);
            xml.WriteAttributeStringSafe("sid", node.UniqueName);
            xml.WriteAttributeStringSafe("name", node.UniqueName);

            xml.WriteAttributeString("layer", node.Active ? "Visible" : "Hidden");
            xml.WriteAttributeString("type", node.NodeClass == Kn5NodeClass.Base && boneNames.Contains(node.UniqueName) ? "JOINT" : "NODE");

            if (node.Children?.FirstOrDefault()?.NodeClass != Kn5NodeClass.SkinnedMesh) {
                xml.WriteElement("matrix",
                        "sid", "transform",
                        node.NodeClass == Kn5NodeClass.Base ? XmlWriterExtension.MatrixToCollada(node.Transform) : "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 0");
            }

            if (IsMultiMaterial(node) && node.Children != null) {
                xml.WriteStartElement("instance_geometry");
                xml.WriteAttributeStringSafe("url", $"#{node.UniqueName}-mesh");
                ExportCollada_NodeSub_BindMaterial(xml, node.Children.Select(x => x.MaterialId).ToArray());
                xml.WriteEndElement();
            } else {
                ExportCollada_NodeSub_Inner(xml, boneNames, node);
            }

            xml.WriteEndElement(); // node
        }

        private void ExportIni(string filename, string fbxName = null) {
            var iniFile = new IniFile(filename);
            ExportIni_Header(iniFile);
            ExportIni_Materials(iniFile);

            if (fbxName != null) {
                ExportIni_Nodes(iniFile, fbxName);
            }

            iniFile.Save();
        }

        public void ExportFbxWithIni(string fbxFilename) {
            var colladaFilename = fbxFilename + ".dae";
            ExportCollada(colladaFilename);
            ConvertColladaToFbx(colladaFilename, fbxFilename);
            ExportIni(fbxFilename + ".ini", Path.GetFileName(fbxFilename));
        }

        public async Task ExportFbxWithIniAsync(string fbxFilename, IProgress<string> progress = null, CancellationToken cancellation = default) {
            var colladaFilename = fbxFilename + ".dae";

            progress?.Report("Exporting to Collada format…");
            await Task.Run(() => ExportCollada(colladaFilename), cancellation);
            if (cancellation.IsCancellationRequested) return;

            progress?.Report("Convert Collada to FBX…");
            await Task.Run(() => ConvertColladaToFbx(colladaFilename, fbxFilename), cancellation);
            if (cancellation.IsCancellationRequested) return;

            progress?.Report("Saving INI-file…");
            await Task.Run(() => ExportIni(fbxFilename + ".ini", Path.GetFileName(fbxFilename)), cancellation);
        }

        public void ExportIni_Header(IniFile iniFile) {
            iniFile["HEADER"].Set("VERSION", 3);
        }

        private void ExportIni_Materials(IniFile iniFile) {
            iniFile["MATERIAL_LIST"]["COUNT"] = Convert.ToString(Materials.Count);

            var materialId = 0;
            foreach (var material in Materials.Values) {
                var section = iniFile["MATERIAL_" + materialId++];
                section.Set("NAME", material.Name);
                section.Set("SHADER", material.ShaderName);
                section.Set("ALPHABLEND", (int)material.BlendMode);
                section.Set("ALPHATEST", material.AlphaTested);
                section.Set("DEPTHMODE", (int)material.DepthMode);

                section.Set("VARCOUNT", material.ShaderProperties.Length);
                for (var i = 0; i < material.ShaderProperties.Length; i++) {
                    section.Set("VAR_" + i + "_NAME", material.ShaderProperties[i].Name);
                    section.Set("VAR_" + i + "_FLOAT1", material.ShaderProperties[i].ValueA);
                    section.Set("VAR_" + i + "_FLOAT2", material.ShaderProperties[i].ValueB.ToString());
                    section.Set("VAR_" + i + "_FLOAT3", material.ShaderProperties[i].ValueC.ToString());
                    section.Set("VAR_" + i + "_FLOAT4", material.ShaderProperties[i].ValueD.ToString());
                }

                section.Set("RESCOUNT", material.TextureMappings.Length);
                for (var i = 0; i < material.TextureMappings.Length; i++) {
                    section.Set("RES_" + i + "_NAME", material.TextureMappings[i].Name);
                    section.Set("RES_" + i + "_SLOT", material.TextureMappings[i].Slot);
                    section.Set("RES_" + i + "_TEXTURE", material.TextureMappings[i].Texture);
                }
            }
        }

        private void ExportIni_Nodes(IniFile iniFile, string fbxName) {
            ExportIni_Node(iniFile, fbxName, RootNode);
        }

        private void ExportIni_Node(IniFile iniFile, string parentName, Kn5Node node, int priority = 0) {
            var name = node == RootNode ? parentName : parentName + "_" + node.Name;

            var section = iniFile["model_FBX: " + name];
            section.Set("ACTIVE", node.Active);
            section.Set("PRIORITY", priority);

            if (node.NodeClass == Kn5NodeClass.Base) {
                if (IsMultiMaterial(node)) {
                    var p = node.Children.Count;
                    foreach (var child in node.Children) {
                        ExportIni_Node(iniFile, name, child, --p);
                    }
                } else {
                    foreach (var child in node.Children) {
                        if (node == RootNode && child.NodeClass == Kn5NodeClass.Mesh) {
                            ExportIni_TrackNode(iniFile, name, child);
                        } else {
                            ExportIni_Node(iniFile, name, child);
                        }
                    }
                }
            } else {
                section.Set("VISIBLE", node.IsVisible);
                section.Set("TRANSPARENT", node.IsTransparent);
                section.Set("CAST_SHADOWS", node.CastShadows);
                section.Set("LOD_IN", node.LodIn);
                section.Set("LOD_OUT", node.LodOut);
                section.Set("RENDERABLE", node.IsRenderable);
            }
        }

        private void ExportIni_TrackNode(IniFile iniFile, string parentName, Kn5Node node) {
            var name = node == RootNode ? parentName : parentName + "_" + node.Name;

            var section = iniFile["model_FBX: " + name];
            section.Set("ACTIVE", node.Active);
            section.Set("PRIORITY", 0);

            section = iniFile["model_FBX: " + name + "_" + node.Name];
            section.Set("ACTIVE", true);
            section.Set("PRIORITY", 0);
            section.Set("VISIBLE", node.IsVisible);
            section.Set("TRANSPARENT", node.IsTransparent);
            section.Set("CAST_SHADOWS", node.CastShadows);
            section.Set("LOD_IN", node.LodIn);
            section.Set("LOD_OUT", node.LodOut);
            section.Set("RENDERABLE", node.IsRenderable);
        }

        private static void Save_Node(Kn5Writer writer, Kn5Node node) {
            writer.Write(node);
            foreach (var t in node.Children) {
                Save_Node(writer, t);
            }
        }

        private IKn5TextureLoader _textureLoader;

        private void SaveInner(Stream stream, IKn5TextureProvider textureProvider = null) {
            using (var writer = new Kn5Writer(stream, true)) {
                writer.Write(Header);

                if (_textureLoader == SkippingTextureLoader.Instance && textureProvider == null) {
                    writer.Write(new Dictionary<string, Kn5Texture>().Count);
                } else {
                    writer.Write(Textures.Count);
                    foreach (var texture in Textures.Values) {
                        if (TexturesData.TryGetValue(texture.Name, out var data) && data.Length > 0) {
                            texture.Length = data.Length;
                            writer.Write(texture);
                            writer.Write(data);
                        } else {
                            textureProvider?.GetTexture(texture.Name, size => {
                                texture.Length = size;
                                writer.Write(texture);
                                writer.Flush();
                                return writer.BaseStream;
                            });
                        }
                    }
                }

                writer.Write(Materials.Count);
                foreach (var material in Materials.Values) {
                    writer.Write(material);
                }

                Save_Node(writer, RootNode);
            }
        }

        private class ExistingKn5Textures : IKn5TextureProvider, IKn5TextureLoader {
            private readonly string _kn5;
            private string _textureName;
            private Func<int, Stream> _fn;

            public ExistingKn5Textures(string kn5) {
                _kn5 = kn5;
            }

            public void OnNewKn5(string kn5Filename) { }

            public void GetTexture(string textureName, Func<int, Stream> writer) {
                _textureName = textureName;
                _fn = writer;
                Kn5.FromFile(_kn5, this, SkippingMaterialLoader.Instance, SkippingNodeLoader.Instance);
                _textureName = null;
                _fn = null;
            }

            byte[] IKn5TextureLoader.LoadTexture(string textureName, ReadAheadBinaryReader reader, int textureSize) {
                if (textureName == _textureName) {
                    var s = _fn?.Invoke(textureSize);
                    if (s != null) {
                        reader.CopyTo(s, textureSize);
                        return null;
                    }
                }

                reader.Skip(textureSize);
                return null;
            }
        }

        public void Save(Stream stream) {
            if (_textureLoader == DefaultKn5TextureLoader.Instance || string.IsNullOrEmpty(OriginalFilename)) {
                SaveInner(stream);
            } else {
                AcToolsLogging.Write("Extra special mode for saving KN5s without textures loaded");
                SaveInner(stream, new ExistingKn5Textures(OriginalFilename));
            }
        }

        public void Save(string filename) {
            using (var stream = File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {
                Save(stream);
            }
        }

        public void SaveRecyclingOriginal(string filename) {
            using (var f = FileUtils.RecycleOriginal(filename)) {
                try {
                    Save(f.Filename);
                } catch {
                    FileUtils.TryToDelete(f.Filename);
                    throw;
                }
            }
        }

        public int NodesCount => Nodes.Count();

        public bool IsWithoutTextures() {
            return Textures.Count == 0;
        }

        public static IKn5Factory GetFactoryInstance() {
            return new Factory();
        }

        private class Factory : IKn5Factory {
            private sealed class Kn5Reader : ReadAheadBinaryReader, IKn5Reader {
                public Kn5Reader(string filename, bool withoutHeader = false) : base(filename) {
                    if (!withoutHeader && new string(ReadChars(6)) != "sc6969") {
                        throw new Exception("Not a valid KN5 file.");
                    }
                }

                public Kn5Reader(Stream filename, bool withoutHeader = false) : base(filename) {
                    if (!withoutHeader && new string(ReadChars(6)) != "sc6969") {
                        throw new Exception("Not a valid KN5 file.");
                    }
                }

                public Kn5MaterialBlendMode ReadBlendMode() {
                    var value = ReadByte();
                    if (value.IsValidBlendMode()) {
                        return (Kn5MaterialBlendMode)value;
                    }

                    AcToolsLogging.Write("Unknown blend mode: " + value);
                    return Kn5MaterialBlendMode.Opaque;
                }

                public Kn5MaterialDepthMode ReadDepthMode() {
                    var value = ReadInt32();
                    if (value.IsValidDepthMode()) {
                        return (Kn5MaterialDepthMode)value;
                    }

                    AcToolsLogging.Write("Unknown depth mode: " + value);
                    return Kn5MaterialDepthMode.DepthOff;
                }

                public Kn5NodeClass ReadNodeClass() {
                    var value = ReadInt32();
                    if (value.IsValidNodeClass()) {
                        return (Kn5NodeClass)value;
                    }

                    AcToolsLogging.Write("Unknown node class: " + value);
                    return Kn5NodeClass.Base;
                }

                public Kn5Header ReadHeader() {
                    var header = new Kn5Header {
                        Version = ReadInt32()
                    };

                    header.Extra = header.Version > 5 ? ReadInt32() : 0;
                    return header;
                }

                public Kn5Texture ReadTexture() {
                    var activeFlag = ReadInt32();
                    var name = ReadString();
                    var length = ReadUInt32();
                    return new Kn5Texture {
                        Active = activeFlag == 1,
                        Name = name,
                        Length = (int)length
                    };
                }

                public Kn5Material ReadMaterial() {
                    var material = new Kn5Material {
                        Name = ReadString(),
                        ShaderName = ReadString(),
                        BlendMode = ReadBlendMode(), // byte
                        AlphaTested = ReadBoolean(), // bool
                        DepthMode = ReadDepthMode(),
                        ShaderProperties = new Kn5Material.ShaderProperty[ReadInt32()]
                    };

                    for (var i = 0; i < material.ShaderProperties.Length; i++) {
                        material.ShaderProperties[i] = new Kn5Material.ShaderProperty {
                            Name = ReadString(),
                            ValueA = ReadSingle(),
                            ValueB = ReadVec2(),
                            ValueC = ReadVec3(),
                            ValueD = ReadVec4()
                        };
                    }

                    material.TextureMappings = new Kn5Material.TextureMapping[ReadInt32()];
                    for (var i = 0; i < material.TextureMappings.Length; i++) {
                        material.TextureMappings[i] = new Kn5Material.TextureMapping {
                            Name = ReadString(),
                            Slot = ReadInt32(),
                            Texture = ReadString()
                        };
                    }

                    return material;
                }

                public void SkipMaterial() {
                    SkipString(); // name
                    SkipString(); // shader name
                    Skip(6); // blend (byte) + alpha tested (byte) + depth mode

                    var properties = ReadInt32();
                    for (var i = 0; i < properties; i++) {
                        SkipString();
                        Skip(40);
                    }

                    var mappings = ReadInt32();
                    for (var i = 0; i < mappings; i++) {
                        SkipString();
                        Skip(4);
                        SkipString();
                    }
                }

                public Kn5Node ReadNode() {
                    var nodeClass = ReadNodeClass();
                    var nodeName = ReadString();
                    var nodeChildren = ReadInt32();
                    var nodeActive = ReadBoolean();

                    var node = new Kn5Node {
                        NodeClass = nodeClass,
                        Name = nodeName,
                        Children = new List<Kn5Node>(nodeChildren),
                        Active = nodeActive
                    };

                    switch (node.NodeClass) {
                        case Kn5NodeClass.Base:
                            node.Transform = ReadMatrix();
                            break;

                        case Kn5NodeClass.Mesh:
                            node.CastShadows = ReadBoolean();
                            node.IsVisible = ReadBoolean();
                            node.IsTransparent = ReadBoolean();

                            node.Vertices = new Kn5Node.Vertex[ReadUInt32()];
                            for (var i = 0; i < node.Vertices.Length; i++) {
                                // 44 bytes per vertex
                                node.Vertices[i] = new Kn5Node.Vertex {
                                    Position = ReadVec3(),
                                    Normal = ReadVec3(),
                                    Tex = ReadVec2(),
                                    Tangent = ReadVec3()
                                };
                            }

                            var indicesCount = ReadUInt32();
                            node.Indices = new ushort[indicesCount];
                            for (var i = 0; i < node.Indices.Length; i++) {
                                node.Indices[i] = ReadUInt16();
                            }

                            node.MaterialId = ReadUInt32();
                            node.Layer = ReadUInt32();

                            node.LodIn = ReadSingle();
                            node.LodOut = ReadSingle();

                            node.BoundingSphereCenter = ReadVec3();
                            node.BoundingSphereRadius = ReadSingle();

                            node.IsRenderable = ReadBoolean();
                            break;

                        case Kn5NodeClass.SkinnedMesh:
                            node.CastShadows = ReadBoolean();
                            node.IsVisible = ReadBoolean();
                            node.IsTransparent = ReadBoolean();

                            node.Bones = new Kn5Node.Bone[ReadUInt32()];
                            for (var i = 0; i < node.Bones.Length; i++) {
                                node.Bones[i] = new Kn5Node.Bone {
                                    Name = ReadString(),
                                    Transform = ReadMatrix()
                                };
                            }

                            node.Vertices = new Kn5Node.Vertex[ReadUInt32()];
                            node.VerticeWeights = new Kn5Node.VerticeWeight[node.Vertices.Length];
                            for (var i = 0; i < node.Vertices.Length; i++) {
                                // 76 bytes per vertex
                                node.Vertices[i] = new Kn5Node.Vertex {
                                    Position = ReadVec3(),
                                    Normal = ReadVec3(),
                                    Tex = ReadVec2(),
                                    Tangent = ReadVec3()
                                };

                                node.VerticeWeights[i] = new Kn5Node.VerticeWeight {
                                    Weights = ReadVec4(),

                                    // Yes! Those are floats!
                                    Indices = ReadVec4()
                                };
                            }

                            node.Indices = new ushort[ReadUInt32()];
                            for (var i = 0; i < node.Indices.Length; i++) {
                                node.Indices[i] = ReadUInt16();
                            }

                            node.MaterialId = ReadUInt32();
                            node.Layer = ReadUInt32();
                            node.LodIn = ReadSingle();
                            node.LodOut = ReadSingle();
                            node.IsRenderable = true;
                            break;
                    }

                    return node;
                }

                /// <summary>
                /// Only hierarchy, without meshes or bones.
                /// </summary>
                public Kn5Node ReadNodeHierarchy() {
                    var nodeClass = ReadNodeClass();
                    var nodeName = ReadString();
                    var nodeChildren = ReadInt32();
                    var nodeActive = ReadBoolean();

                    var node = new Kn5Node {
                        NodeClass = nodeClass,
                        Name = nodeName,
                        Children = new List<Kn5Node>(nodeChildren),
                        Active = nodeActive
                    };

                    switch (node.NodeClass) {
                        case Kn5NodeClass.Base:
                            node.Transform = ReadMatrix();
                            break;

                        case Kn5NodeClass.Mesh:
                            node.CastShadows = ReadBoolean();
                            node.IsVisible = ReadBoolean();
                            node.IsTransparent = ReadBoolean();

                            node.Vertices = new Kn5Node.Vertex[0];
                            node.Indices = new ushort[0];

                            Skip((int)(44 * ReadUInt32()));
                            Skip((int)(2 * ReadUInt32()));

                            node.MaterialId = ReadUInt32();
                            node.Layer = ReadUInt32();

                            node.LodIn = ReadSingle();
                            node.LodOut = ReadSingle();

                            node.BoundingSphereCenter = ReadVec3();
                            node.BoundingSphereRadius = ReadSingle();

                            node.IsRenderable = ReadBoolean();
                            break;

                        case Kn5NodeClass.SkinnedMesh:
                            node.CastShadows = ReadBoolean();
                            node.IsVisible = ReadBoolean();
                            node.IsTransparent = ReadBoolean();

                            node.Bones = new Kn5Node.Bone[0];
                            node.Vertices = new Kn5Node.Vertex[0];
                            node.VerticeWeights = new Kn5Node.VerticeWeight[0];
                            node.Indices = new ushort[0];

                            var bones = ReadUInt32();
                            for (var i = 0; i < bones; i++) {
                                SkipString();
                                Skip(64);
                            }

                            Skip((int)(76 * ReadUInt32()));
                            Skip((int)(2 * ReadUInt32()));

                            node.MaterialId = ReadUInt32();
                            node.Layer = ReadUInt32();

                            node.MisteryBytes = ReadBytes(8); // the only mystery left?
                            node.IsRenderable = true;
                            break;
                    }

                    return node;
                }

                public int SkipNode() {
                    var nodeClass = ReadNodeClass();
                    SkipString();

                    var children = ReadInt32();
                    switch (nodeClass) {
                        case Kn5NodeClass.Base:
                            Skip(65); // active flag (byte) + transform matrix
                            break;

                        case Kn5NodeClass.Mesh:
                            Skip(4); // active flag + cast shadow + is visible + transparent
                            Skip((int)(44 * ReadUInt32()));
                            Skip((int)(2 * ReadUInt32()) + 33);
                            break;

                        case Kn5NodeClass.SkinnedMesh:
                            Skip(4); // active flag + cast shadow + is visible + transparent
                            var bones = ReadUInt32();
                            for (var i = 0; i < bones; i++) {
                                SkipString();
                                Skip(64);
                            }
                            Skip((int)(76 * ReadUInt32()));
                            Skip((int)(2 * ReadUInt32()) + 16);
                            break;
                    }

                    return children;
                }
            }

            public IKn5 FromFile(string filename, IKn5TextureLoader textureLoader = null, IKn5MaterialLoader materialLoader = null,
                    IKn5NodeLoader nodeLoader = null) {
                if (!File.Exists(filename)) {
                    throw new FileNotFoundException(filename);
                }

                var kn5 = new Kn5Basic(filename);
                (textureLoader = textureLoader ?? DefaultKn5TextureLoader.Instance).OnNewKn5(filename);
                (materialLoader = materialLoader ?? DefaultKn5MaterialLoader.Instance).OnNewKn5(filename);
                (nodeLoader = nodeLoader ?? DefaultKn5NodeLoader.Instance).OnNewKn5(filename);

                using (var reader = new Kn5Reader(filename)) {
                    FromFile_Header(kn5, reader);
                    FromFile_Textures(kn5, reader, textureLoader);
                    if (nodeLoader != SkippingNodeLoader.Instance || materialLoader != SkippingMaterialLoader.Instance) {
                        FromFile_Materials(kn5, reader, materialLoader);
                        FromFile_Nodes(kn5, reader, nodeLoader);
                    }
                }

                kn5._textureLoader = textureLoader;
                kn5.IsEditable = materialLoader == DefaultKn5MaterialLoader.Instance && nodeLoader != DefaultKn5NodeLoader.Instance;
                return kn5;
            }

            public IKn5 FromStream(Stream entry, IKn5TextureLoader textureLoader = null, IKn5MaterialLoader materialLoader = null,
                    IKn5NodeLoader nodeLoader = null) {
                var kn5 = new Kn5Basic(string.Empty);
                (textureLoader = textureLoader ?? DefaultKn5TextureLoader.Instance).OnNewKn5(string.Empty);
                (materialLoader = materialLoader ?? DefaultKn5MaterialLoader.Instance).OnNewKn5(string.Empty);
                (nodeLoader = nodeLoader ?? DefaultKn5NodeLoader.Instance).OnNewKn5(string.Empty);

                using (var reader = new Kn5Reader(entry)) {
                    FromFile_Header(kn5, reader);
                    FromFile_Textures(kn5, reader, textureLoader);
                    if (nodeLoader != SkippingNodeLoader.Instance || materialLoader != SkippingMaterialLoader.Instance) {
                        FromFile_Materials(kn5, reader, materialLoader);
                        FromFile_Nodes(kn5, reader, nodeLoader);
                    }
                }

                kn5._textureLoader = textureLoader;
                kn5.IsEditable = materialLoader == DefaultKn5MaterialLoader.Instance && nodeLoader != DefaultKn5NodeLoader.Instance;
                return kn5;
            }

            public IKn5 FromBytes(byte[] data, IKn5TextureLoader textureLoader = null) {
                using (var memory = new MemoryStream(data)) {
                    return FromStream(memory, textureLoader);
                }
            }

            public IKn5 CreateEmpty() {
                return new Kn5Basic {
                    RootNode = Kn5Node.CreateBaseNode("Root"),
                    _textureLoader = DefaultKn5TextureLoader.Instance,
                    IsEditable = true
                };
            }

            private void FromFile_Header(Kn5Basic kn5, Kn5Reader reader) {
                kn5.Header = reader.ReadHeader();
            }

            public IKn5TextureLoader TextureLoader { get; private set; }

            private void FromFile_Textures(Kn5Basic kn5, Kn5Reader reader, [NotNull] IKn5TextureLoader textureLoader) {
                TextureLoader = textureLoader;

                try {
                    var count = reader.ReadInt32();

                    kn5.Textures = new Dictionary<string, Kn5Texture>(count);
                    kn5.TexturesData = new Dictionary<string, byte[]>(count);

                    for (var i = 0; i < count; i++) {
                        var texture = reader.ReadTexture();
                        if (texture.Length > 0) {
                            kn5.Textures[texture.Name] = texture;
                            kn5.TexturesData[texture.Name] = textureLoader.LoadTexture(texture.Name, reader, texture.Length) ?? new byte[0];
                        }
                    }
                } catch (NotImplementedException) {
                    kn5.Textures = null;
                    kn5.TexturesData = null;
                }
            }

            public IKn5MaterialLoader MaterialLoader { get; private set; }

            private void FromFile_Materials(Kn5Basic kn5, Kn5Reader reader, [NotNull] IKn5MaterialLoader materialLoader) {
                MaterialLoader = materialLoader;

                try {
                    var count = reader.ReadInt32();

                    kn5.Materials = new Dictionary<string, Kn5Material>(count);
                    for (var i = 0; i < count; i++) {
                        var material = materialLoader.LoadMaterial(reader);
                        if (material != null) {
                            kn5.Materials[material.Name] = material;
                        }
                    }
                } catch (NotImplementedException) {
                    kn5.Materials = null;
                }
            }

            public IKn5NodeLoader NodeLoader { get; private set; }

            private void FromFile_Nodes(Kn5Basic kn5, Kn5Reader reader, [NotNull] IKn5NodeLoader nodeLoader) {
                NodeLoader = nodeLoader;
                kn5.RootNode = nodeLoader.LoadNode(reader);
            }
        }
    }
}