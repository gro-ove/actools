using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.ExtraKn5Utils.Kn5Utils;
using AcTools.ExtraKn5Utils.KsAnimUtils;
using AcTools.Kn5File;
using AcTools.Utils.Helpers;
using SlimDX;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public class CarLodGeneratorMergeRules {
        private int _lodIndex;
        private string[] _notAllowedMeshes;
        private string[] _nodesToMergeWithin;
        private string[] _nodesToRemove;

        public List<string> MaterialsToIncreasePriority;
        public List<string> MaterialsToNotJoin;
        public List<string> MaterialsToKeep;
        public List<string> MaterialsToRemove;

        public CarLodGeneratorMergeRules(string carDirectory, DataWrapper data, int lodIndex) {
            _lodIndex = lodIndex;
            _notAllowedMeshes = CollectMergeExceptions().NonNull().ToArray();
            _nodesToMergeWithin = CollectNodesToMergeWithin().NonNull().ToArray();
            _nodesToRemove = CollectNodesToRemove().NonNull().ToArray();

            IEnumerable<string> CollectMergeExceptions() {
                if (lodIndex <= 2) {
                    foreach (var section in data.GetIniFile("lights.ini").GetSections("BRAKE")) {
                        yield return section.GetNonEmpty("NAME");
                    }
                    foreach (var section in data.GetIniFile("lights.ini").GetSections("LIGHT")) {
                        yield return section.GetNonEmpty("NAME");
                    }

                    if (lodIndex <= 1) {
                        foreach (var section in data.GetIniFile("mirrors.ini").GetSections("MIRROR")) {
                            yield return section.GetNonEmpty("NAME");
                        }
                        var brakesVisual = data.GetIniFile("brakes.ini")["DISCS_GRAPHICS"];
                        yield return brakesVisual.GetNonEmpty("DISC_LF");
                        yield return brakesVisual.GetNonEmpty("DISC_RF");
                        yield return brakesVisual.GetNonEmpty("DISC_LR");
                        yield return brakesVisual.GetNonEmpty("DISC_RR");
                    }
                }
            }

            IEnumerable<string> CollectNodesToMergeWithin() {
                if (lodIndex == 1) {
                    foreach (var section in data.GetIniFile("blurred_objects.ini").GetSections("OBJECT")) {
                        yield return section.GetNonEmpty("NAME");
                    }
                }
                if (lodIndex <= 1) {
                    yield return "COCKPIT_HR";
                }
                if (lodIndex != 3) {
                    yield return "WHEEL_LF";
                    yield return "WHEEL_RF";
                    yield return "WHEEL_LR";
                    yield return "WHEEL_RR";
                }

                var lightsAnimation = Path.Combine(carDirectory, "animations", "lights.ksanim");
                if (File.Exists(lightsAnimation)) {
                    var animFile = KsAnimFile.KsAnim.FromFile(lightsAnimation);
                    foreach (var entry in animFile.Entries.Where(x => !x.Value.IsStatic())){
                        yield return entry.Key;
                    }
                }
            }

            IEnumerable<string> CollectNodesToRemove() {
                if (lodIndex > 1) {
                    foreach (var section in data.GetIniFile("blurred_objects.ini").GetSections("OBJECT").Where(x => x.GetDouble("MIN_SPEED", 0d) > 0d)) {
                        yield return section.GetNonEmpty("NAME");
                    }
                    foreach (var section in data.GetIniFile("mirrors.ini").GetSections("MIRROR")) {
                        yield return section.GetNonEmpty("NAME");
                    }
                }

                if (_lodIndex == 3) {
                    foreach (var section in data.GetIniFile("lights.ini").GetSections("BRAKE")) {
                        yield return section.GetNonEmpty("NAME");
                    }
                    foreach (var section in data.GetIniFile("lights.ini").GetSections("LIGHT")) {
                        yield return section.GetNonEmpty("NAME");
                    }

                    var brakesVisual = data.GetIniFile("brakes.ini")["DISCS_GRAPHICS"];
                    yield return brakesVisual.GetNonEmpty("DISC_LF");
                    yield return brakesVisual.GetNonEmpty("DISC_RF");
                    yield return brakesVisual.GetNonEmpty("DISC_LR");
                    yield return brakesVisual.GetNonEmpty("DISC_RR");

                    yield return "SHIFT_HD";
                    yield return "SHIFT_LD";
                    yield return "STEER_HR";
                    yield return "STEER_LR";
                    yield return "CINTURE_ON";
                    yield return "CINTURE_OFF";
                }

                yield return "COCKPIT_LR";
            }
        }

        public double CalculateReductionPriority(IKn5 kn5, Kn5Node node) {
            if (_lodIndex == 0) return 1d;
            if (node.Name == "COCKPIT_HR") return _lodIndex == 1 ? 0.2 : 0.1;
            if (node.Name.StartsWith("SUSP_")) return 0.2;
            if (node.NodeClass == Kn5NodeClass.Mesh) {
                var material = kn5.GetMaterial(node.MaterialId);
                if (material == null || MaterialsToNotJoin != null && !MaterialsToNotJoin.Contains(material.Name)) {
                    return 0.2;
                }
                if (MaterialsToIncreasePriority != null && MaterialsToIncreasePriority.Contains(material.Name)){
                    return 1.2;
                }
                if (material.ShaderName == "ksTyres") {
                    return _lodIndex <= 2 ? 0.6 : 0.4;
                }
                if (material.ShaderName == "ksBrakeDisc") {
                    return _lodIndex == 1 ? 0.4 : 0.2;
                }
            }
            return 1d;
        }

        public bool CanSkipNodeCompletely(IKn5 kn5, Kn5Node node) {
            if (node.NodeClass == Kn5NodeClass.SkinnedMesh
                    || _nodesToRemove.Contains(node.Name) || _lodIndex == 3 && !node.Active
                    || node.Name.StartsWith("ARROW_")
                    || _lodIndex != 1 && node.Name.StartsWith("DAMAGE_GLASS_")) {
                return true;
            }

            if (node.NodeClass == Kn5NodeClass.Mesh) {
                if (MaterialsToKeep != null || MaterialsToRemove != null) {
                    var material = kn5.GetMaterial(node.MaterialId);
                    if (material == null || MaterialsToKeep?.Contains(material.Name) == false || MaterialsToRemove?.Contains(material.Name) == true) return true;
                }

                if (_lodIndex == 0) {
                    if (node.IsTransparent || !node.IsRenderable || !node.IsVisible) return true;

                    var material = kn5.GetMaterial(node.MaterialId);
                    if (material?.BlendMode != Kn5MaterialBlendMode.Opaque) return true;
                }

                if (_lodIndex == 3) {
                    if (node.IsTransparent || !node.IsRenderable || !node.IsVisible) return true;

                    if (MaterialsToKeep != null || MaterialsToRemove != null) {
                        var material = kn5.GetMaterial(node.MaterialId);
                        if (material?.BlendMode != Kn5MaterialBlendMode.Opaque || material.AlphaTested) return true;
                    }
                }

                return false;
            }

            return _lodIndex == 3 && node.Name.StartsWith("SUSP_");
        }

        public bool CanMergeMesh(Kn5Node node) {
            if (!node.IsRenderable || !node.IsVisible || !node.Active) return false;
            return _notAllowedMeshes.IndexOf(node.Name) == -1 && !node.Name.StartsWith("DAMAGE_");
        }

        public bool CanMergeInsideNode(Kn5Node node) {
            return true;
        }

        public bool CanRemoveEmptyNode(Kn5Node node) {
            return !node.Name.StartsWith("WHEEL_") && !node.Name.StartsWith("SUSP_");
        }

        public bool IsNodeMergeRoot(Kn5Node node) {
            if (_nodesToMergeWithin.Contains(node.Name)) return true;
            return _lodIndex <= 2 && node.Name.StartsWith("WHEEL_");
        }

        public int MergeGroup(IKn5 kn5, Kn5Node node, double priority) {
            if (MaterialsToNotJoin != null) {
                var material = kn5.GetMaterial(node.MaterialId);
                if (material == null || !MaterialsToNotJoin.Contains(material.Name)) {
                    node.MaterialId = uint.MaxValue;
                    return priority.GetHashCode();
                }
            }
            return ((int)node.MaterialId * 397 | (node.IsTransparent ? 1 << 31 : 0) | (node.CastShadows ? 1 << 30 : 0)) ^ priority.GetHashCode();
        }

        public int GroupOrder(IKn5 kn5, IEnumerable<Tuple<Kn5Node, double, Matrix>> node, Dictionary<Kn5Node, int> nodeIndices) {
            var isTransparent = false;
            var isBlending = false;
            var maxIndex = 0;
            foreach (var n in node) {
                isTransparent |= n.Item1.IsTransparent;
                isBlending = kn5.GetMaterial(n.Item1.MaterialId)?.BlendMode == Kn5MaterialBlendMode.AlphaBlend;
                maxIndex = Math.Max(maxIndex, nodeIndices[n.Item1]);
            }
            return (isTransparent ? 1 << 31 : 0) | (isBlending ? 1 << 30 : 0) | maxIndex;
        }

        public void FinalizeKn5(IKn5 kn5) {
            if (MaterialsToNotJoin != null) {
                var newMaterials = kn5.Materials.Values.Where(x => MaterialsToNotJoin.Contains(x.Name))
                        .Append(Kn5MaterialUtils.Create("__lodD_black")).ToDictionary(x => x.Name, x => x);
                foreach (var node in kn5.Nodes) {
                    if (node.NodeClass != Kn5NodeClass.Base) {
                        node.MaterialId = newMaterials.IndexOf(kn5.GetMaterial(node.MaterialId)?.Name) ?? newMaterials.IndexOf("__lodD_black") ?? 0U;
                        if (node.MaterialId >= newMaterials.Count) node.MaterialId = (uint)newMaterials.FindIndex(m => m.Key == "__lodD_black");
                    }
                }
                kn5.Materials = newMaterials;
            }
        }
    }
}