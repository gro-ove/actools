using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.ServerPreset {
    public static class WrapperContentObjectExtension {
        public static void LoadFrom([CanBeNull] this IEnumerable<WrapperContentObject> enumerable, [CanBeNull] JToken obj, string childrenKey = null) {
            if (enumerable == null) return;
            foreach (var o in enumerable) {
                o.LoadFrom(obj?[o.AcObject.Id], childrenKey);
            }
        }

        public static void SaveTo([CanBeNull] this IList<WrapperContentObject> list, [NotNull] JObject obj, string key, string childrenKey = null) {
            if (list == null) return;

            var o = new JObject();
            foreach (var i in list) {
                var j = new JObject();
                i.SaveTo(j, childrenKey);
                if (j.Count > 0) {
                    o[i.AcObject.Id] = j;
                    obj[key] = o;
                }
            }
        }
    }
}