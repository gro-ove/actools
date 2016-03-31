using System;
using AcTools.Kn5File;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Objects;

namespace AcTools.Render.Kn5Specific.Utils {
    public static class Kn5Converter {
        public static IRenderableObject Convert(Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node);

                case Kn5NodeClass.Mesh:
                case Kn5NodeClass.SkinnedMesh:
                    return new Kn5RenderableObject(node);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
