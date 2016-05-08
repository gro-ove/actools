using System;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;

namespace AcTools.Render.Kn5Specific.Utils {
    public static class Kn5Converter {
        public static IRenderableObject Convert(Kn5Node node, DeviceContextHolder holder) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node, holder);

                case Kn5NodeClass.Mesh:
                case Kn5NodeClass.SkinnedMesh:
                    return new Kn5RenderableObject(node, holder);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
