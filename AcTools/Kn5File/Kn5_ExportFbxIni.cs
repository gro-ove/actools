using AcTools.Utils;
using System;
using System.Globalization;
using System.Linq;
using AcTools.DataFile;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        private void ExportIni(string filename, string fbxName = null) {
            var iniFile = new IniFile(filename);
            ExportIni_Header(iniFile);
            ExportIni_Materials(iniFile);

            if (fbxName != null) {
                ExportIni_Nodes(iniFile, fbxName);
            }

            iniFile.Save();
        }

        private void ExportIni_Header(IniFile iniFile) {
            iniFile["HEADER"]["VERSION"] = "3";
        }

        private void ExportIni_Materials(IniFile iniFile) {
            iniFile["MATERIAL_LIST"]["COUNT"] = Convert.ToString(Materials.Count);

            var materialId = 0;
            foreach (var material in Materials.Values) {
                var section = iniFile["MATERIAL_" + materialId++];
                section["NAME"] = material.Name;
                section["SHADER"] = material.ShaderName;
                section["ALPHABLEND"] = Convert.ToString((int)material.BlendMode);
                section["ALPHATEST"] = material.AlphaTested ? "1" : "0";
                section["DEPTHMODE"] = Convert.ToString((int)material.DepthMode);
                section["VARCOUNT"] = Convert.ToString(material.ShaderProperties.Length);

                for (var i = 0; i < material.ShaderProperties.Length; i++) {
                    section["VAR_" + i + "_NAME"] = material.ShaderProperties[i].Name;
                    section["VAR_" + i + "_FLOAT1"] = material.ShaderProperties[i].ValueA.ToString(CultureInfo.InvariantCulture);
                    section["VAR_" + i + "_FLOAT2"] = string.Join(",", material.ShaderProperties[i].ValueB.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                    section["VAR_" + i + "_FLOAT3"] = string.Join(",", material.ShaderProperties[i].ValueC.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                    section["VAR_" + i + "_FLOAT4"] = string.Join(",", material.ShaderProperties[i].ValueD.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                }
                
                section["RESCOUNT"] = Convert.ToString(material.TextureMappings.Length);

                for (var i = 0; i < material.TextureMappings.Length; i++) {
                    section["RES_" + i + "_NAME"] = material.TextureMappings[i].Name;
                    section["RES_" + i + "_SLOT"] = Convert.ToString(material.TextureMappings[i].Slot);
                    section["RES_" + i + "_TEXTURE"] = material.TextureMappings[i].Texture;
                }
            }
        }

        private void ExportIni_Nodes(IniFile iniFile, string fbxName) {
            ExportIni_Node(iniFile, fbxName, RootNode);
        }

        private void ExportIni_Node(IniFile iniFile, string parentName, Kn5Node node) {
            var name = node == RootNode ? parentName : parentName + "_" + node.Name;

            var section = iniFile["model_FBX: " + name];
            section["ACTIVE"] = node.Active ? "1" : "0";
            section["PRIORITY"] = "0";

            if (node.NodeClass == Kn5NodeClass.Base) {
                foreach (var child in node.Children) {
                    if (node == RootNode && child.NodeClass == Kn5NodeClass.Mesh) { 
                        ExportIni_TrackNode(iniFile, name, child);
                    } else { 
                        ExportIni_Node(iniFile, name, child);
                    }
                }
            } else {
                section["VISIBLE"] = node.IsVisible ? "1" : "0";
                section["TRANSPARENT"] = node.IsTransparent ? "1" : "0";
                section["CAST_SHADOWS"] = node.CastShadows ? "1" : "0";
                section["LOD_IN"] = node.LodIn.ToString(CultureInfo.InvariantCulture);
                section["LOD_OUT"] = node.LodOut.ToString(CultureInfo.InvariantCulture);
                section["RENDERABLE"] = node.IsRenderable ? "1" : "0";
            }
        }

        private void ExportIni_TrackNode(IniFile iniFile, string parentName, Kn5Node node) {
            var name = node == RootNode ? parentName : parentName + "_" + node.Name;

            var section = iniFile["model_FBX: " + name];
            section["ACTIVE"] = node.Active ? "1" : "0";
            section["PRIORITY"] = "0";

            section = iniFile["model_FBX: " + name + "_" + node.Name];
            section["ACTIVE"] = "1";
            section["PRIORITY"] = "0";
            section["VISIBLE"] = node.IsVisible ? "1" : "0";
            section["TRANSPARENT"] = node.IsTransparent ? "1" : "0";
            section["CAST_SHADOWS"] = node.CastShadows ? "1" : "0";
            section["LOD_IN"] = node.LodIn.ToString(CultureInfo.InvariantCulture);
            section["LOD_OUT"] = node.LodOut.ToString(CultureInfo.InvariantCulture);
            section["RENDERABLE"] = node.IsRenderable ? "1" : "0";
        }
    }
}
