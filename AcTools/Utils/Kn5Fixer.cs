using AcTools.Kn5File;
using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.DataFile;

namespace AcTools.Utils {
    public static class Kn5Fixer {
        private static bool FixLrHrNodes_HasChild(Kn5Node node, Kn5Node child) {
            if (node.NodeClass != Kn5NodeClass.Base) return false;
            return node.Children.Contains(child) || node.Children.Any(subNode => FixLrHrNodes_HasChild(subNode, child));
        }

        private static bool FixLrHrNodes_HasParentWithName(Kn5Node rootNode, Kn5Node node, string name) {
            if (rootNode.NodeClass != Kn5NodeClass.Base) return false;

            foreach (var subNode in rootNode.Children) {
                if (subNode.Name == name) {
                    return FixLrHrNodes_HasChild(subNode, node);
                } else if (FixLrHrNodes_HasParentWithName(subNode, node, name)){
                    return true;
                }
            }

            return false;
        }

        public static bool FixLrHrNodes(string acRoot, string carName) {
            var kn5File = FileUtils.GetMainCarFilename(acRoot, carName);

            var kn5 = Kn5.FromFile(kn5File);
            var fixedNodes = 0;
            
            foreach (var node in kn5.Nodes) {
                if (node.NodeClass != Kn5NodeClass.Base) continue;

                if (!node.Active && (
                        node.Name == "STEER_HR" || 
                        node.Name == "COCKPIT_HR" ||
                        node.Name == "STEER_LR" && FixLrHrNodes_HasParentWithName(kn5.RootNode, node, "COCKPIT_LR")
                    )) {
                    fixedNodes++;
                    node.Active = true;
                } else if (node.Active && (
                        node.Name == "STEER_LR" ||
                        node.Name == "COCKPIT_LR" ||
                        node.Name.StartsWith("DAMAGE_GLASS")
                    )) {
                    fixedNodes++;
                    node.Active = false;
                }
            }

            if (fixedNodes == 0){
                return false;
            }

            FileUtils.Recycle(kn5File);
            kn5.SaveAll(kn5File);
            return true;
        }

        public static bool FixBlurredWheels(string acRoot, string carName) {
            var kn5File = FileUtils.GetMainCarFilename(acRoot, carName);

            var kn5 = Kn5.FromFile(kn5File);
            var wheels = 0;

            string[] normalRims;
            string[] blurredRims;
            try {
                var normalRimsList = new List<string>();
                var blurredRimsList = new List<string>();
                foreach (var section in new IniFile(FileUtils.GetCarDirectory(acRoot, carName), "blurred_objects.ini").Values) {
                    if (Math.Abs(section.GetDouble("MIN_SPEED", 0d)) < 0.001) {
                        normalRimsList.Add(section.GetPossiblyEmpty("NAME"));
                    } else {
                        blurredRimsList.Add(section.GetPossiblyEmpty("NAME"));
                    }
                }
                    
                normalRims = normalRimsList.ToArray();
                blurredRims = blurredRimsList.ToArray();
            } catch (Exception) {
                normalRims = new []{ "RIM_LF", "RIM_RF", "RIM_LR", "RIM_RR" };
                blurredRims = new []{ "RIM_BLUR_LF", "RIM_BLUR_RF", "RIM_BLUR_LR", "RIM_BLUR_RR" };
            }
            
            foreach (var node in kn5.Nodes.Where(node => node.NodeClass == Kn5NodeClass.Base)) {
                if (!node.Active && normalRims.Contains(node.Name)) {
                    node.Active = true;
                    wheels++;
                } else if (node.Active && blurredRims.Contains(node.Name)){
                    node.Active = false;
                    wheels++;
                }
            }

            if (wheels == 0){
                return false;
            }

            FileUtils.Recycle(kn5File);
            kn5.SaveAll(kn5File);
            return true;
        }

        public static bool FixSuspension(string acRoot, string carName) {
            var kn5File = FileUtils.GetMainCarFilename(acRoot, carName);

            var kn5 = Kn5.FromFile(kn5File);
            var added = 0;

            foreach (var name in new []{ "SUSP_LF", "SUSP_RF", "SUSP_LR", "SUSP_RR" }.Where(name => kn5.FirstByName(name) == null)) {
                kn5.RootNode.Children.Add(Kn5Node.CreateBaseNode(name));
                added++;
            }

            if (added == 0){
                return false;
            }

            FileUtils.Recycle(kn5File);
            kn5.SaveAll(kn5File);
            return true;
        }

        public static string FixSuspensionWrapper(string acRoot, string carName) {
            try {
                return FixSuspension(acRoot, carName) ? null : "Nothing to fix";
            } catch (Exception e) {
                return e.ToString();
            }
        }
    }
}
