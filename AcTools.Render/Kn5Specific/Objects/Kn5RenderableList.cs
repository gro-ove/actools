using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Camera;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Utils;

namespace AcTools.Render.Kn5Specific.Objects {
    public class Kn5RenderableList : RenderableList {
        public readonly Kn5Node OriginalNode;

        public Kn5RenderableList(Kn5Node node)
            : base(node.Transform.ToMatrixFixX(), node.Children.Select(Kn5Converter.Convert)) {
            OriginalNode = node;
        }

        public override void Draw(DeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!OriginalNode.Active || OriginalNode.Name == "COCKPIT_LR" || OriginalNode.Name == "STEER_LR" ||
                OriginalNode.Name == "CINTURE_ON") return;
            base.Draw(contextHolder, camera, mode);
        }
    }
}
