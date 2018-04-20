using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5SpecificSpecial {
    public abstract class UtilsRendererBase : BaseRenderer {
        [NotNull]
        protected static IEnumerable<IKn5RenderableObject> Flatten(RenderableList root, Func<IRenderableObject, bool> filter = null) {
            return root
                    .SelectManyRecursive(x => x is Kn5RenderableList list && list.IsEnabled ? (filter?.Invoke(list) == false ? null : list) : null)
                    .OfType<IKn5RenderableObject>()
                    .Where(x => x.IsEnabled && filter?.Invoke(x) != false);
        }

        [NotNull]
        protected static IEnumerable<IKn5RenderableObject> Flatten(Kn5 kn5, RenderableList root, [CanBeNull] string textureName,
                [CanBeNull] string objectPath) {
            var split = Lazier.Create(() => objectPath?.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));

            bool TestObjectPath(IKn5RenderableObject obj) {
                var s = split.Value;
                if (s == null || s.Length < 1) return true;
                if (s[s.Length - 1] != obj.OriginalNode.Name) return false;
                return kn5.GetObjectPath(obj.OriginalNode) == objectPath;
            }

            return Flatten(root, x => {
                if (!(x is IKn5RenderableObject k)) return true;
                if (!TestObjectPath(k)) return false;
                if (textureName == null) return true;
                var material = kn5.GetMaterial(k.OriginalNode.MaterialId);
                return material != null && material.TextureMappings.Where(y => y.Name != "txDetail" && y.Name != "txNormalDetail")
                                                   .Any(m => m.Texture == textureName);
            });
        }
    }
}