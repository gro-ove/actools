using System;
using AcTools.Kn5File;
using SlimDX;

namespace AcTools.ExtraKn5Utils.Kn5Utils {
    public static class Kn5NodeUtils {
        public static Matrix GetMatrix(this Kn5Node node) {
            return node.Transform.ToMatrix();
        }

        public static Matrix CalculateTransformRelativeToParent(this Kn5Node child, Kn5Node root) {
            if (!TraverseDown(root, Matrix.Identity, out var ret)) {
                throw new Exception("Failed to traverse down");
            }
            return ret;

            bool TraverseDown(Kn5Node node, Matrix m, out Matrix r) {
                foreach (var c in node.Children) {
                    if (c == child) {
                        r = m;
                        return true;
                    }
                    if (c.Transform != null) {
                        if (TraverseDown(c, c.GetMatrix() * m, out r)) return true;
                    }
                }
                r = new Matrix();
                return false;
            }
        }
    }
}