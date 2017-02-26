using System;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public sealed class Kn5RenderableList : RenderableList {
        public readonly Kn5Node OriginalNode;

        private readonly string _dirNode;
        private bool _dirTargetSet;
        private RenderableList _dirTarget;
        
        public bool HighlightDummy { get; set; }
        private readonly DebugLinesObject _lines = new DebugLinesObject(Matrix.Identity, new[] {
            new InputLayouts.VerticePC(new Vector3(0f, 0f, 0f), new Color4(0, 1, 0)),
            new InputLayouts.VerticePC(new Vector3(0f, 0.02f, 0f), new Color4(0, 1, 0)),
            new InputLayouts.VerticePC(new Vector3(0f, 0f, 0f), new Color4(1, 0, 0)),
            new InputLayouts.VerticePC(new Vector3(0.02f, 0f, 0f), new Color4(1, 0, 0)),
            new InputLayouts.VerticePC(new Vector3(0f, 0f, 0f), new Color4(0, 0, 1)),
            new InputLayouts.VerticePC(new Vector3(0f, 0f, 0.02f), new Color4(0, 0, 1)),
        });

        public Kn5RenderableList(Kn5Node node, Func<Kn5Node, IRenderableObject> convert)
                : base(node.Name, node.Transform.ToMatrix(), node.Children.Count == 0 ? new IRenderableObject[0] : node.Children.Select(convert)) {
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

                var model = contextHolder.TryToGet<IKn5Model>();
                if (model != null) {
                    _dirTarget = model.GetDummyByName(_dirNode);
                    _dirTarget?.LookAt(this);
                }
            }

            base.Draw(contextHolder, camera, mode, filter);
            
            if (HighlightDummy && mode == SpecialRenderMode.SimpleTransparent) {
                _lines.ParentMatrix = Matrix;
                _lines.Draw(contextHolder, camera, SpecialRenderMode.Simple);
            }
        }

        public Matrix ModelMatrixInverted { get; internal set; }

        public Matrix RelativeToModel => Matrix * ModelMatrixInverted;
    }
}
