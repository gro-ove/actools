using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace AcTools.Kn5File {
    public static class XmlWriterSafeExtend {
        private static readonly Regex InvalidXmlChars = new Regex(
            @"(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F\uFEFF\uFFFE\uFFFF]",
            RegexOptions.Compiled);

        public static string RemoveInvalidXmlChars(string text) {
            return string.IsNullOrEmpty(text) ? "" : InvalidXmlChars.Replace(text, "");
        }

        public static void WriteAttributeStringSafe(this XmlWriter xml, string key, string value){
            xml.WriteAttributeString(key, RemoveInvalidXmlChars(value));
        }

        public static void WriteElementStringSafe(this XmlWriter xml, string key, string value){
            xml.WriteElementString(key, RemoveInvalidXmlChars(value));
        }
    }

    public partial class Kn5 {
        private void ExportCollada(string filename) {
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
                xml.WriteStartElement("unit");
                xml.WriteAttributeString("name", "meter");
                xml.WriteAttributeString("meter", "1");
                xml.WriteEndElement();
                xml.WriteElementString("up_axis", "Y_UP");
                xml.WriteEndElement();

                xml.WriteStartElement("library_cameras");
                xml.WriteEndElement();

                xml.WriteStartElement("library_lights");
                xml.WriteEndElement();

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
                ExportCollada_Mesh(xml, RootNode);
                xml.WriteEndElement();

                xml.WriteStartElement("library_controllers");
                xml.WriteEndElement();

                xml.WriteStartElement("library_visual_scenes");
                xml.WriteStartElement("visual_scene");
                xml.WriteAttributeString("id", "Scene");
                xml.WriteAttributeString("name", "Scene");
                ExportCollada_Node(xml, RootNode);
                xml.WriteEndElement();
                xml.WriteEndElement();

                xml.WriteStartElement("scene");
                xml.WriteStartElement("instance_visual_scene");
                xml.WriteAttributeString("url", "#Scene");
                xml.WriteEndElement();
                xml.WriteEndElement();

                xml.WriteEndElement();
                xml.WriteEndDocument();
            }
        }

        private void ExportCollada_Texture(XmlWriter xml, Kn5Texture texture) {
            xml.WriteStartElement("image");
            xml.WriteAttributeStringSafe("id", texture.Name + "-image");
            xml.WriteAttributeStringSafe("name", texture.Name);
            
            xml.WriteStartElement("init_from");
            xml.WriteString("file://texture/" + texture.Name);
            xml.WriteEndElement();

            xml.WriteEndElement();
        }

        private void ExportCollada_MaterialEffect(XmlWriter xml, Kn5Material material) {
            xml.WriteStartElement("effect");
            xml.WriteAttributeStringSafe("id", material.Name + "-effect");
            xml.WriteStartElement("profile_COMMON");
            xml.WriteStartElement("technique");
            xml.WriteAttributeString("sid", "common");
            xml.WriteStartElement("phong");
            
            xml.WriteStartElement("emission");
            xml.WriteStartElement("color");
            xml.WriteAttributeString("sid", "emission");
            var ksEmissive = material.GetPropertyByName("ksEmissive");
            xml.WriteString(ksEmissive == null ? "0 0 0 1" : string.Format("{0} {1} {2} 1", ksEmissive.ValueC[0].ToString(CultureInfo.InvariantCulture), 
                ksEmissive.ValueC[1].ToString(CultureInfo.InvariantCulture), ksEmissive.ValueC[2].ToString(CultureInfo.InvariantCulture)));
            xml.WriteEndElement();
            xml.WriteEndElement();
            
            xml.WriteStartElement("ambient");
            xml.WriteStartElement("color");
            xml.WriteAttributeString("sid", "ambient");
            var ksAmbient = material.GetPropertyByName("ksAmbient");
            xml.WriteString(ksAmbient == null ? "0 0 0 1" : string.Format("{0} {0} {0} 1", ksAmbient.ValueA.ToString(CultureInfo.InvariantCulture)));
            xml.WriteEndElement();
            xml.WriteEndElement();
            
            xml.WriteStartElement("diffuse");
            var txDiffuse = material.GetMappingByName("txDiffuse");
            if (txDiffuse != null) {
                xml.WriteStartElement("texture");
                xml.WriteAttributeStringSafe("texture", txDiffuse.Texture + "-image");
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

            xml.WriteStartElement("color");
            xml.WriteAttributeString("sid", "diffuse");
            var ksDiffuse = material.GetPropertyByName("ksDiffuse");
            xml.WriteString(ksDiffuse == null ? "0.64 0.64 0.64 1" : string.Format("{0} {0} {0} 1", ksDiffuse.ValueA.ToString(CultureInfo.InvariantCulture)));
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
            var ksSpecularExp = material.GetPropertyByName("ksSpecularEXP");
            xml.WriteString(ksSpecularExp == null ? "50" : string.Format("{0}", ksSpecularExp.ValueA.ToString(CultureInfo.InvariantCulture)));
            xml.WriteEndElement();
            xml.WriteEndElement();
            
            xml.WriteStartElement("index_of_refraction");
            xml.WriteStartElement("float");
            xml.WriteAttributeString("sid", "index_of_refraction");
            xml.WriteString("1");
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        private void ExportCollada_Material(XmlWriter xml, Kn5Material material) {
            xml.WriteStartElement("material");
            xml.WriteAttributeStringSafe("id", material.Name + "-material");
            xml.WriteAttributeStringSafe("name", material.Name);
            
            xml.WriteStartElement("instance_effect");
            xml.WriteAttributeStringSafe("url", "#" + material.Name + "-effect");
            xml.WriteEndElement();
            
            xml.WriteEndElement();
        }

        private void ExportCollada_Mesh(XmlWriter xml, Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    foreach (var child in node.Children) {
                        ExportCollada_Mesh(xml, child);
                    }
                    break;

                case Kn5NodeClass.Mesh:
                case Kn5NodeClass.SkinnedMesh:

                    xml.WriteStartElement("geometry");
                    xml.WriteAttributeStringSafe("id", node.Name + "-mesh");
                    xml.WriteAttributeStringSafe("name", node.Name);

                    xml.WriteStartElement("mesh");

                    /* coordinates */
                    xml.WriteStartElement("source");
                    xml.WriteAttributeStringSafe("id", node.Name + "-mesh-positions");
                    xml.WriteStartElement("float_array");
                    xml.WriteAttributeStringSafe("id", node.Name + "-mesh-positions-array");
                    xml.WriteAttributeString("count", (node.Vertices.Length * 3).ToString(CultureInfo.InvariantCulture));

                    var builder = new StringBuilder();
                    for (var j = 0; j < node.Vertices.Length; j++) {
                        builder.Append(node.Vertices[j].Co[0].ToString(CultureInfo.InvariantCulture));
                        builder.Append(" ");
                        builder.Append(node.Vertices[j].Co[1].ToString(CultureInfo.InvariantCulture));
                        builder.Append(" ");
                        builder.Append(node.Vertices[j].Co[2].ToString(CultureInfo.InvariantCulture));
                        builder.Append(" ");
                    }
                    xml.WriteString(builder.ToString());
                    xml.WriteEndElement();

                    xml.WriteStartElement("technique_common");
                    xml.WriteStartElement("accessor");
                    xml.WriteAttributeStringSafe("source", "#" + node.Name + "-mesh-positions-array");
                    xml.WriteAttributeString("count", node.Vertices.Length.ToString(CultureInfo.InvariantCulture));
                    xml.WriteAttributeString("stride", "3");

                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "X");
                    xml.WriteAttributeString("type", "float");
                    xml.WriteEndElement();
                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "Y");
                    xml.WriteAttributeString("type", "float");
                    xml.WriteEndElement();
                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "Z");
                    xml.WriteAttributeString("type", "float");
                    xml.WriteEndElement();

                    xml.WriteEndElement();
                    xml.WriteEndElement();
                    xml.WriteEndElement();

                    /* normals */
                    xml.WriteStartElement("source");
                    xml.WriteAttributeStringSafe("id", node.Name + "-mesh-normals");
                    xml.WriteStartElement("float_array");
                    xml.WriteAttributeStringSafe("id", node.Name + "-mesh-normals-array");
                    xml.WriteAttributeString("count", (node.Vertices.Length * 3).ToString(CultureInfo.InvariantCulture));

                    builder = new StringBuilder();
                    for (var j = 0; j < node.Vertices.Length; j++) {
                        builder.Append(node.Vertices[j].Normal[0].ToString(CultureInfo.InvariantCulture));
                        builder.Append(" ");
                        builder.Append(node.Vertices[j].Normal[1].ToString(CultureInfo.InvariantCulture));
                        builder.Append(" ");
                        builder.Append(node.Vertices[j].Normal[2].ToString(CultureInfo.InvariantCulture));
                        builder.Append(" ");
                    }
                    xml.WriteString(builder.ToString());
                    xml.WriteEndElement();

                    xml.WriteStartElement("technique_common");
                    xml.WriteStartElement("accessor");
                    xml.WriteAttributeStringSafe("source", "#" + node.Name + "-mesh-normals-array");
                    xml.WriteAttributeString("count", node.Vertices.Length.ToString(CultureInfo.InvariantCulture));
                    xml.WriteAttributeString("stride", "3");

                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "X");
                    xml.WriteAttributeString("type", "float");
                    xml.WriteEndElement();
                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "Y");
                    xml.WriteAttributeString("type", "float");
                    xml.WriteEndElement();
                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "Z");
                    xml.WriteAttributeString("type", "float");
                    xml.WriteEndElement();

                    xml.WriteEndElement();
                    xml.WriteEndElement();
                    xml.WriteEndElement();

                    /* uv */
                    xml.WriteStartElement("source");
                    xml.WriteAttributeStringSafe("id", node.Name + "-mesh-map-0");
                    xml.WriteStartElement("float_array");
                    xml.WriteAttributeStringSafe("id", node.Name + "-mesh-map-0-array");
                    xml.WriteAttributeString("count", (node.Vertices.Length * 2).ToString(CultureInfo.InvariantCulture));

                    builder = new StringBuilder();
                    for (var j = 0; j < node.Vertices.Length; j++) {
                        builder.Append(node.Vertices[j].Uv[0].ToString(CultureInfo.InvariantCulture));
                        builder.Append(" ");
                        builder.Append((-node.Vertices[j].Uv[1]).ToString(CultureInfo.InvariantCulture));
                        builder.Append(" ");
                    }
                    xml.WriteString(builder.ToString());
                    xml.WriteEndElement();

                    xml.WriteStartElement("technique_common");
                    xml.WriteStartElement("accessor");
                    xml.WriteAttributeStringSafe("source", "#" + node.Name + "-mesh-map-0-array");
                    xml.WriteAttributeString("count", node.Vertices.Length.ToString(CultureInfo.InvariantCulture));
                    xml.WriteAttributeString("stride", "2");

                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "S");
                    xml.WriteAttributeString("type", "float");
                    xml.WriteEndElement();
                    xml.WriteStartElement("param");
                    xml.WriteAttributeString("name", "T");
                    xml.WriteAttributeString("type", "float");
                    xml.WriteEndElement();

                    xml.WriteEndElement();
                    xml.WriteEndElement();
                    xml.WriteEndElement();

                    /* vertices */
                    xml.WriteStartElement("vertices");
                    xml.WriteAttributeStringSafe("id", node.Name + "-mesh-vertices");
                    xml.WriteStartElement("input");
                    xml.WriteAttributeString("semantic", "POSITION");
                    xml.WriteAttributeStringSafe("source", "#" + node.Name + "-mesh-positions");
                    xml.WriteEndElement();
                    xml.WriteEndElement();

                    /* triangles */
                    xml.WriteStartElement("polylist");
                    xml.WriteAttributeStringSafe("material", Materials.Values.ElementAt((int)node.MaterialId).Name + "-material");
                    xml.WriteAttributeString("count", (node.Indices.Length / 3).ToString(CultureInfo.InvariantCulture));

                    xml.WriteStartElement("input");
                    xml.WriteAttributeString("semantic", "VERTEX");
                    xml.WriteAttributeStringSafe("source", "#" + node.Name + "-mesh-vertices");
                    xml.WriteAttributeString("offset", "0");
                    xml.WriteEndElement();
                    xml.WriteStartElement("input");
                    xml.WriteAttributeString("semantic", "NORMAL");
                    xml.WriteAttributeStringSafe("source", "#" + node.Name + "-mesh-normals");
                    xml.WriteAttributeString("offset", "1");
                    xml.WriteEndElement();
                    xml.WriteStartElement("input");
                    xml.WriteAttributeString("semantic", "TEXCOORD");
                    xml.WriteAttributeStringSafe("source", "#" + node.Name + "-mesh-map-0");
                    xml.WriteAttributeString("offset", "2");
                    xml.WriteAttributeString("set", "0");
                    xml.WriteEndElement();

                    builder = new StringBuilder();
                    for (var j = 0; j < node.Indices.Length; j += 3) {
                        builder.Append("3 ");
                    }
                    xml.WriteElementString("vcount", builder.ToString());

                    builder = new StringBuilder();
                    foreach (var s in node.Indices.Select(j => j.ToString(CultureInfo.InvariantCulture))) {
                        builder.Append(s);
                        builder.Append(" ");
                        builder.Append(s);
                        builder.Append(" ");
                        builder.Append(s);
                        builder.Append(" ");
                    }
                    xml.WriteElementString("p", builder.ToString());

                    xml.WriteEndElement();
                    xml.WriteEndElement();
                    xml.WriteEndElement();
                    break;

                //case Kn5NodeClass.SkinnedMesh:
                //    throw new NotImplementedException();
            }
        }

        private void ExportCollada_Node(XmlWriter xml, Kn5Node node) {
            foreach (var t in node.Children) {
                ExportCollada_NodeSub(xml, t, false);
            }
        }

        private void ExportCollada_NodeSub(XmlWriter xml, Kn5Node node, bool onlyChild) {
            if (node.NodeClass == Kn5NodeClass.Base || !onlyChild) {
                xml.WriteStartElement("node");
                xml.WriteAttributeStringSafe("id", node.Name);
                xml.WriteAttributeStringSafe("name", node.Name);
                xml.WriteAttributeString("type", "NODE");

                xml.WriteStartElement("matrix");
                xml.WriteAttributeString("sid", "transform");

                if (node.NodeClass == Kn5NodeClass.Base) {
                    var sb = new StringBuilder();
                    for (var i = 0; i < 4; i++) {
                        for (var j = 0; j < 4; j++) {
                            sb.Append(node.Transform[j * 4 + i].ToString(CultureInfo.InvariantCulture));
                            sb.Append(" ");
                        }
                    }
                    xml.WriteString(sb.ToString());
                } else {
                    xml.WriteString("1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 0");
                }

                xml.WriteEndElement();
            }

            if (node.NodeClass == Kn5NodeClass.Base) {
                foreach (var t in node.Children) {
                    var onlyOneChild = node.Children.Count == 1;
                    ExportCollada_NodeSub(xml, t, onlyOneChild);
                }
            } else {
                xml.WriteStartElement("instance_geometry");
                xml.WriteAttributeStringSafe("url", "#" + node.Name + "-mesh");

                xml.WriteStartElement("bind_material");
                xml.WriteStartElement("technique_common");
                xml.WriteStartElement("instance_material");

                var materialName = Materials.Values.ElementAt((int)node.MaterialId).Name;
                xml.WriteAttributeStringSafe("symbol", materialName + "-material");
                xml.WriteAttributeStringSafe("target", "#" + materialName + "-material");

                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndElement();

                xml.WriteEndElement();
            }

            if (node.NodeClass == Kn5NodeClass.Base || !onlyChild) {
                xml.WriteEndElement();
            }
        }
    }
}
