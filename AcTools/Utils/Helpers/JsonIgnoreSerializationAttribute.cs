using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AcTools.Utils.Helpers {
    public class JsonIgnoreSerializationAttribute : Attribute {
        private class JsonPropertiesResolver : DefaultContractResolver {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType) {
                return objectType.GetProperties()
                        .Where(pi => !Attribute.IsDefined(pi, typeof(JsonIgnoreSerializationAttribute)))
                        .ToList<MemberInfo>();
            }
        }

        public static string SerializeConsidering(object arg) {
            return JsonConvert.SerializeObject(arg, new JsonSerializerSettings { ContractResolver = new JsonPropertiesResolver() });
        }
    }
}