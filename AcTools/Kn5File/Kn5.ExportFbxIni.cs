using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
                    section.Set("VAR_" + i + "_FLOAT2", material.ShaderProperties[i].ValueB);
                    section.Set("VAR_" + i + "_FLOAT3", material.ShaderProperties[i].ValueC);
                    section.Set("VAR_" + i + "_FLOAT4", material.ShaderProperties[i].ValueD);
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
    }
}
