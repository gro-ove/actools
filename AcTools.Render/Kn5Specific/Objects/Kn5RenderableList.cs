using System;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;

namespace AcTools.Render.Kn5Specific.Objects {
    public sealed class Kn5RenderableList : RenderableList {
        public readonly Kn5Node OriginalNode;

        public Kn5RenderableList(Kn5Node node)
                : this(node, Kn5RenderableFile.Convert) { }

        public Kn5RenderableList(Kn5Node node, Func<Kn5Node, IRenderableObject> convert)
                : base(node.Name, Kn5RenderableObject.FlipByX ? node.Transform.ToMatrixFixX() : node.Transform.ToMatrix(),
                        node.Children.Select(convert)) {
            OriginalNode = node;
            if (IsEnabled && (!OriginalNode.Active || OriginalNode.Name == "CINTURE_ON" || OriginalNode.Name.StartsWith("DAMAGE_GLASS"))) {
                IsEnabled = false;
            }
        }
    }
}
