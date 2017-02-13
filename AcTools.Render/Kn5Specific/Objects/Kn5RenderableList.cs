using System;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public sealed class Kn5RenderableList : RenderableList {
        public readonly Kn5Node OriginalNode;

        private readonly string _dirNode;
        private bool _dirTargetSet;
        private RenderableList _dirTarget;

        public Kn5RenderableList(Kn5Node node, Func<Kn5Node, IRenderableObject> convert)
                : base(node.Name, Kn5RenderableObject.FlipByX ? node.Transform.ToMatrixFixX() : node.Transform.ToMatrix(),
                        node.Children.Select(convert)) {
            OriginalNode = node;
            if (IsEnabled && (!OriginalNode.Active || OriginalNode.Name == "CINTURE_ON" || OriginalNode.Name.StartsWith("DAMAGE_GLASS"))) {
                IsEnabled = false;
            }

            if (node.Name.StartsWith("DIR_")) {
                _dirNode = node.Name.Substring(4);
            }
        }

        public override void Draw(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (_dirNode != null && !_dirTargetSet) {
                _dirTargetSet = true;
                _dirTarget = contextHolder.Get<IKn5Model>().GetDummyByName(_dirNode);
                _dirTarget?.LookAt(this);
            }

            base.Draw(contextHolder, camera, mode, filter);
        }

        public Matrix ModelMatrixInverted { get; internal set; }

        public Matrix RelativeToModel => Matrix * ModelMatrixInverted;
    }
}
