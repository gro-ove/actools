using System;
using System.Collections.Generic;
using AcTools.Kn5File;
using AcTools.Numerics;
using JetBrains.Annotations;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5NodeUtils {
        public static Mat4x4 CalculateTransformRelativeToParent(this Kn5Node child, Kn5Node root) {
            var mat = new Mat4x4();
            if (!TraverseDown(root, Mat4x4.Identity, ref mat)) {
                throw new Exception("Failed to traverse down");
            }
            return mat;

            bool TraverseDown(Kn5Node node, Mat4x4 m, ref Mat4x4 r) {
                foreach (var c in node.Children) {
                    if (c == child) {
                        r = m;
                        return true;
                    }
                    if (c.NodeClass == Kn5NodeClass.Base) {
                        if (TraverseDown(c, c.Transform * m, ref r)) return true;
                    }
                }
                return false;
            }
        }

        [CanBeNull]
        public static Kn5Node FindFirst(this Kn5Node parent, string name) {
            foreach (var child in parent.Children) {
                if (child.Name == name) return child;
                var ret = child.FindFirst(name);
                if (ret != null) {
                    return ret;
                }
            }
            return null;
        }

        public static IEnumerable<Kn5Node> AllChildren([CanBeNull] this Kn5Node parent) {
            var queue = new Queue<Kn5Node>();
            if (parent != null) {
                queue.Enqueue(parent);
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
}