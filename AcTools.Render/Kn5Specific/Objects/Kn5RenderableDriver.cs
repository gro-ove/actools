using AcTools.Kn5File;
using AcTools.KnhFile;
using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public class Kn5RenderableDriver : Kn5RenderableSkinnable {
        public Kn5RenderableDriver(Kn5 kn5, Matrix matrix, string overridesDirectory, bool asyncTexturesLoading = true,
                bool asyncOverrideTexturesLoading = false, bool allowSkinnedObjects = false)
                : base(kn5, matrix, overridesDirectory, asyncTexturesLoading, asyncOverrideTexturesLoading, allowSkinnedObjects) {}

        private void AlignNodes(KnhEntry entry, Matrix matrix) {
            var dummy = GetDummyByName(entry.Name);
            if (dummy != null) {
                dummy.LocalMatrix = entry.Transformation.ToMatrix();
            } else {
                matrix = entry.Transformation.ToMatrix() * matrix;
            }

            foreach (var child in entry.Children) {
                AlignNodes(child, matrix);
            }
        }

        public void AlignNodes(Knh node) {
            AlignNodes(node.RootEntry, Matrix.Identity);
        }
    }
}