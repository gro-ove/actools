using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcTools.NeuralTyres.Data {
    [JsonObject(MemberSerialization.OptIn)]
    public class NeuralTyresSource {
        [CanBeNull, JsonProperty("car")]
        public string CarId { get; protected set; }

        [CanBeNull, JsonProperty("name")]
        public string Name { get; protected set; }

        [CanBeNull, JsonProperty("shortName")]
        public string ShortName { get; protected set; }

        protected NeuralTyresSource() { }

        [JsonConstructor]
        public NeuralTyresSource(string car, string name, string shortName) {
            CarId = car;
            Name = name;
            ShortName = shortName;
        }
    }
}