using System.Collections.Generic;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Objects {
    public static class RenderableListExtension {
        public static IEnumerable<Kn5RenderableObject> GetByNameAll(this RenderableList list, string name) {
            foreach (var obj in list) {
                var r = obj as Kn5RenderableObject;
                if (r?.OriginalNode.Name == name) {
                    yield return r;
                }

                var l = obj as RenderableList;
                if (l == null) continue;
                foreach (var o in GetByNameAll(l, name)) {
                    yield return o;
                }
            }
        }

        [CanBeNull]
        public static Kn5RenderableObject GetByName(this RenderableList list, string name) {
            return list.GetByNameAll(name).FirstOrDefault();
        }

        public static IEnumerable<Kn5RenderableList> GetAllDummiesByName(this RenderableList list, string name) {
            foreach (var obj in list) {
                var r = obj as Kn5RenderableList;
                if (r?.OriginalNode.Name == name) {
                    yield return r;
                }

                var l = obj as RenderableList;
                if (l == null) continue;
                foreach (var o in GetAllDummiesByName(l, name)) {
                    yield return o;
                }
            }
        }

        [CanBeNull]
        public static Kn5RenderableList GetDummyByName(this RenderableList list, string name) {
            return list.GetAllDummiesByName(name).FirstOrDefault();
        }
    }

    public class Kn5RenderableList : RenderableList {
        public readonly Kn5Node OriginalNode;

        public Kn5RenderableList(Kn5Node node)
                : base(Kn5RenderableObject.FlipByX ? node.Transform.ToMatrixFixX() : node.Transform.ToMatrix(),
                        node.Children.Select(Kn5Converter.Convert)) {
            OriginalNode = node;
            if (IsEnabled && (!OriginalNode.Active || OriginalNode.Name == "COCKPIT_LR" || OriginalNode.Name == "STEER_LR" ||
                    OriginalNode.Name == "CINTURE_ON" || OriginalNode.Name.StartsWith("DAMAGE_GLASS"))) {
                IsEnabled = false;
            }
        }
    }
}
