using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base.Objects;
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
}