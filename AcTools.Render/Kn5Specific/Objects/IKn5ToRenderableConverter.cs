using System;
using AcTools.Kn5File;
using AcTools.Render.Base.Objects;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Objects {
    public interface IKn5ToRenderableConverter {
        [NotNull]
        IRenderableObject Convert([NotNull] Kn5Node node);
    }

    public class Kn5ToRenderableSimpleConverter : IKn5ToRenderableConverter {
        public static Kn5ToRenderableSimpleConverter Instance { get; } = new Kn5ToRenderableSimpleConverter();

        public IRenderableObject Convert(Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node, this);

                case Kn5NodeClass.Mesh:
                    return new Kn5RenderableObject(node);

                case Kn5NodeClass.SkinnedMesh:
                    return new Kn5RenderableObject(node);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public class Kn5ToRenderableSkinnedConverter : IKn5ToRenderableConverter {
        public static Kn5ToRenderableSkinnedConverter Instance { get; } = new Kn5ToRenderableSkinnedConverter();

        public IRenderableObject Convert(Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node, this);

                case Kn5NodeClass.Mesh:
                    return new Kn5RenderableObject(node);

                case Kn5NodeClass.SkinnedMesh:
                    return new Kn5SkinnedObject(node);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}