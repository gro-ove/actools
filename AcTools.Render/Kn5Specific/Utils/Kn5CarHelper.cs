using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Utils {
    public class Kn5CarHelper {
        public readonly string Directory;
        public readonly DataWrapper Data;

        public Kn5CarHelper(string kn5Filename) {
            Directory = Path.GetDirectoryName(kn5Filename);
            Data = DataWrapper.FromFile(Directory);
        }

        private IRenderableObject LoadWheelAmbientShadow(Kn5RenderableList main, string nodeName, string textureName) {
            var node = main.GetDummyByName(nodeName);
            if (node == null) return null;

            var wheel = node.Matrix.GetTranslationVector();
            wheel.Y = 0.01f;

            var filename = Path.Combine(Directory, textureName);
            return new AmbientShadow(filename, Matrix.Scaling(0.3f, 1.0f, 0.3f) * Matrix.RotationY(MathF.PI) *
                    Matrix.Translation(wheel));
        }
        
        private IRenderableObject LoadBodyAmbientShadow() {
            var iniFile = Data.GetIniFile("ambient_shadows.ini");
            var ambientBodyShadowSize = new Vector3(
                    (float)iniFile["SETTINGS"].GetDouble("WIDTH", 1d), 1.0f,
                    (float)iniFile["SETTINGS"].GetDouble("LENGTH", 1d));

            var filename = Path.Combine(Directory, "body_shadow.png");
            return new AmbientShadow(filename, Matrix.Scaling(ambientBodyShadowSize) * Matrix.RotationY(MathF.PI) *
                    Matrix.Translation(0f, 0.01f, 0f));
        }

        public IEnumerable<IRenderableObject> LoadAmbientShadows(Kn5RenderableList node) {
            if (Data.IsEmpty) return new IRenderableObject[0];
            return new[] {
                LoadBodyAmbientShadow(),
                LoadWheelAmbientShadow(node, "WHEEL_LF", "tyre_0_shadow.png"),
                LoadWheelAmbientShadow(node, "WHEEL_RF", "tyre_1_shadow.png"),
                LoadWheelAmbientShadow(node, "WHEEL_LR", "tyre_2_shadow.png"),
                LoadWheelAmbientShadow(node, "WHEEL_RR", "tyre_3_shadow.png")
            }.Where(x => x != null);
        }

        public void AdjustPosition(Kn5RenderableList node) {
            node.UpdateBoundingBox();
            node.LocalMatrix = Matrix.Translation(0, -node.BoundingBox?.Minimum.Y ?? 0f, 0) * node.LocalMatrix;
        }

        public void LoadMirrors(Kn5RenderableList node) {
            if (Data.IsEmpty) return;
            foreach (var obj in from section in Data.GetIniFile("mirrors.ini").GetSections("MIRROR")
                                select node.GetByName(section.Get("NAME"))) {
                obj?.SwitchToMirror();
            }
        }
    }
}