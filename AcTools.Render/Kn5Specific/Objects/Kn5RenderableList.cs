using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Utils;

namespace AcTools.Render.Kn5Specific.Objects {
    public class Kn5RenderableList : RenderableList {
        public readonly Kn5Node OriginalNode;

        public Kn5RenderableList(Kn5Node node, DeviceContextHolder holder)
                : base(Kn5RenderableObject.FlipByX ? node.Transform.ToMatrixFixX() : node.Transform.ToMatrix(),
                        node.Children.Select(x => Kn5Converter.Convert(x, holder))) {
            OriginalNode = node;
            if (IsEnabled && (!OriginalNode.Active || OriginalNode.Name == "COCKPIT_LR" || OriginalNode.Name == "STEER_LR" ||
                    OriginalNode.Name == "CINTURE_ON" || OriginalNode.Name.StartsWith("DAMAGE_GLASS"))) {
                IsEnabled = false;
            }
        }
    }
}
