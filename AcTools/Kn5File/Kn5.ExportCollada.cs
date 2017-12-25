using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    internal static class XmlWriterExtension {
        private static readonly Regex InvalidXmlChars = new Regex(
            @"(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F\uFEFF\uFFFE\uFFFF]",
            RegexOptions.Compiled);

        public static string RemoveInvalidXmlChars(string text) {
            return string.IsNullOrEmpty(text) ? "" : InvalidXmlChars.Replace(text, "");
        }

        public static void WriteAttributeStringSafe(this XmlWriter xml, string key, string value){
            xml.WriteAttributeString(key, RemoveInvalidXmlChars(value));
        }

        public static void WriteAttributeString(this XmlWriter xml, string key, int value){
            xml.WriteAttributeString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteAttributeString(this XmlWriter xml, string key, float value){
            xml.WriteAttributeString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteAttributeString(this XmlWriter xml, string key, double value){
            xml.WriteAttributeString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteString(this XmlWriter xml, int value) {
            xml.WriteString(value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteString(this XmlWriter xml, float value) {
            xml.WriteString(value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteString(this XmlWriter xml, double value) {
            xml.WriteString(value.ToString(CultureInfo.InvariantCulture));
        }

        public static string MatrixToCollada(float[] matrix) {
            var sb = new StringBuilder();
            for (var i = 0; i < 4; i++) {
                for (var j = 0; j < 4; j++) {
                    if (i > 0 || j > 0) {
                        sb.Append(" ");
                    }

                    sb.Append(matrix[j * 4 + i].ToString(CultureInfo.InvariantCulture));
                }
            }

            return sb.ToString();
        }

        public static void WriteMatrixAsString(this XmlWriter xml, float[] matrix){
            xml.WriteString(MatrixToCollada(matrix));
        }

        public static void WriteElementStringSafe(this XmlWriter xml, string key, string value){
            xml.WriteElementString(key, RemoveInvalidXmlChars(value));
        }

        public static void WriteElement(this XmlWriter xml, [NotNull] string localName, [NotNull] params object[] attributes) {
            xml.WriteStartElement(localName);
            int i;
            for (i = 0; i < attributes.Length - 1; i += 2) {
                xml.WriteAttributeStringSafe(attributes[i].ToInvariantString(), attributes[i + 1].ToInvariantString());
            }
            if (i < attributes.Length) {
                xml.WriteString(attributes[i].ToInvariantString());
            }
            xml.WriteEndElement();
        }
    }

    public partial class Kn5 {
        public static bool OptionJoinToMultiMaterial = true;

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
                xml.WriteElementStringSafe("created", new FileInfo(OriginalFilename).CreationTime.ToString(CultureInfo.InvariantCulture));
                xml.WriteElementStringSafe("modified", new FileInfo(OriginalFilename).LastWriteTime.ToString(CultureInfo.InvariantCulture));
                xml.WriteElement("unit",
                        "name", "meter",
                        "meter", 1);
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
                xml.WriteElement("instance_visual_scene",
                        "url", "#Scene");
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
            xml.WriteString("file://texture/" + texture.Name);
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
                    $"{ksEmissive.ValueC[0].ToString(CultureInfo.InvariantCulture)} {ksEmissive.ValueC[1].ToString(CultureInfo.InvariantCulture)} {ksEmissive.ValueC[2].ToString(CultureInfo.InvariantCulture)} 1");
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
                    xml.WriteString(node.Bones.Select(x => XmlWriterExtension.MatrixToCollada(x.Transform))
                                        .JoinToString(" "));
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

                    var weights = node.VerticeWeights.SelectMany(x => x.Weights.Where((y, i) => !Equals(x.Indices[i], -1f))).ToList();
                    xml.WriteAttributeString("count", weights.Count);
                    xml.WriteString(weights.JoinToString(" "));
                    xml.WriteEndElement(); // float_array

                    xml.WriteStartElement("technique_common");
                    xml.WriteStartElement("accessor");
                    xml.WriteAttributeStringSafe("source", $"#{node.UniqueName}Controller-Weights-array");
                    xml.WriteAttributeString("count", weights.Count);
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

                    xml.WriteElementString("vcount", node.VerticeWeights.Select(x => x.Weights.Count(y => !Equals(y, 0f))).JoinToString(" "));
                    var k = 0;
                    xml.WriteElementString("v", node.VerticeWeights.SelectMany(x =>
                            x.Indices.Where(y => !Equals(y, -1f)).SelectMany((y, i) => new[] { y, k++ })).JoinToString(" "));

                    xml.WriteEndElement(); // vertex_weights

                    xml.WriteEndElement(); // skin
                    xml.WriteEndElement(); // controller
                    break;
            }

        }

        private void ExportCollada_Mesh(XmlWriter xml, string name, IReadOnlyList<Kn5Node> unsorted) {
            AcToolsLogging.Write($"{name}: {unsorted.Sum(x => x.Vertices.Length)} vertices, {unsorted.Sum(x => x.Indices.Length / 3d)} triangles");

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
            xml.WriteString(nodes.SelectMany(x => x.Vertices).SelectMany(x => x.Co).JoinToString(" "));
            xml.WriteEndElement(); // float_array

            xml.WriteStartElement("technique_common");
            xml.WriteStartElement("accessor");
            xml.WriteAttributeStringSafe("source", $"#{name}-mesh-positions-array");
            xml.WriteAttributeString("count", vertexCount);
            xml.WriteAttributeString("stride", 3);

            xml.WriteElement("param",
                    "name", "X",
                    "type", "float");
            xml.WriteElement("param",
                    "name", "Y",
                    "type", "float");
            xml.WriteElement("param",
                    "name", "Z",
                    "type", "float");

            xml.WriteEndElement(); // accessor
            xml.WriteEndElement(); // technique_common
            xml.WriteEndElement(); // source

            /* normals */
            xml.WriteStartElement("source");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-normals");
            xml.WriteStartElement("float_array");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-normals-array");
            xml.WriteAttributeString("count", vertexCount * 3);
            xml.WriteString(nodes.SelectMany(x => x.Vertices).SelectMany(x => x.Normal).JoinToString(" "));
            xml.WriteEndElement(); // float_array

            xml.WriteStartElement("technique_common");
            xml.WriteStartElement("accessor");
            xml.WriteAttributeStringSafe("source", $"#{name}-mesh-normals-array");
            xml.WriteAttributeString("count", vertexCount);
            xml.WriteAttributeString("stride", "3");

            xml.WriteElement("param",
                    "name", "X",
                    "type", "float");
            xml.WriteElement("param",
                    "name", "Y",
                    "type", "float");
            xml.WriteElement("param",
                    "name", "Z",
                    "type", "float");

            xml.WriteEndElement(); // accessor
            xml.WriteEndElement(); // technique_common
            xml.WriteEndElement(); // source

            /* uv */
            xml.WriteStartElement("source");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-map-0");
            xml.WriteStartElement("float_array");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-map-0-array");
            xml.WriteAttributeString("count", vertexCount * 2);
            xml.WriteString(nodes.SelectMany(x => x.Vertices).SelectMany(x => new[] { x.Uv[0], -x.Uv[1] }).JoinToString(" "));
            xml.WriteEndElement(); // float_array

            xml.WriteStartElement("technique_common");
            xml.WriteStartElement("accessor");
            xml.WriteAttributeStringSafe("source", $"#{name}-mesh-map-0-array");
            xml.WriteAttributeString("count", vertexCount);
            xml.WriteAttributeString("stride", 2);

            xml.WriteElement("param",
                    "name", "S",
                    "type", "float");
            xml.WriteElement("param",
                    "name", "T",
                    "type", "float");

            xml.WriteEndElement(); // accessor
            xml.WriteEndElement(); // technique_common
            xml.WriteEndElement(); // source

            /* vertices */
            xml.WriteStartElement("vertices");
            xml.WriteAttributeStringSafe("id", $"{name}-mesh-vertices");
            xml.WriteElement("input",
                    "semantic", "POSITION",
                    "source", $"#{name}-mesh-positions");
            xml.WriteEndElement();

            /* triangles */
            var offset = 0;
            foreach (var node in nodes) {
                xml.WriteStartElement("triangles");
                xml.WriteAttributeStringSafe("original_node", $"{node.UniqueName}");
                xml.WriteAttributeStringSafe("material", $"{Materials.Values.ElementAt((int)node.MaterialId).Name}-material");
                xml.WriteAttributeString("count", node.Indices.Length / 3);

                xml.WriteElement("input",
                        "semantic", "VERTEX",
                        "source", $"#{name}-mesh-vertices",
                        "offset", 0);
                xml.WriteElement("input",
                        "semantic", "NORMAL",
                        "source", $"#{name}-mesh-normals",
                        "offset", 1);
                xml.WriteElement("input",
                        "semantic", "TEXCOORD",
                        "source", $"#{name}-mesh-map-0",
                        "offset", 2,
                        "set", 0);

                var inner = offset;
                xml.WriteElementString("p", node.Indices.SelectMany(x => new[] { x + inner, x + inner, x + inner }).JoinToString(" "));
                xml.WriteEndElement(); // triangles

                offset += node.Vertices.Length;
            }

            xml.WriteEndElement(); // mesh
            xml.WriteEndElement(); // geometry
        }

        private static bool IsMultiMaterial(Kn5Node node) {
            if (!OptionJoinToMultiMaterial || node.NodeClass != Kn5NodeClass.Base || !node.Children.Any()) return false;
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
                    ExportCollada_Mesh(xml, node.UniqueName, new [] { node });
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

            foreach (var materialName in materialId.Select(u => Materials.Values.ElementAt((int)u).Name)) {
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
                    if (node.Children.Count == 1 && node.Children[0].NodeClass != Kn5NodeClass.Base) {
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
    }
}
