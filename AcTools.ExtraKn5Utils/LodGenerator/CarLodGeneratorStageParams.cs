using System.Collections.Generic;
using System.IO;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    [JsonObject(MemberSerialization.OptIn)]
    public class CarLodGeneratorStageParams : IWithId {
        [JsonIgnore]
        public string Id { get; }

        [JsonIgnore]
        public JObject DefinitionsData { get; }

        public Dictionary<string, string> UserDefined { get; }

        public CarLodGeneratorStageParams(string id, string filename, JObject definitionsFilename, Dictionary<string, string> userDefined) {
            Id = id;
            DefinitionsData = definitionsFilename;
            UserDefined = userDefined;
            JsonConvert.PopulateObject(File.ReadAllText(filename), this);
        }

        public void Refresh(string filename) {
            KeepTemporaryFiles = false;
            ApplyWeldingFix = false;
            JsonConvert.PopulateObject(File.ReadAllText(filename), this);
        }

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("keepTemporaryFiles")]
        public bool KeepTemporaryFiles;

        [JsonProperty("applyWeldingFix")]
        public bool ApplyWeldingFix;

        [JsonProperty("separatePriorityGroups")]
        public bool SeparatePriorityGroups;

        [JsonProperty("modelName", Required = Required.DisallowNull), NotNull]
        public string ModelNamePath = string.Empty;

        [JsonProperty("config", Required = Required.DisallowNull), NotNull]
        public string ConfigSectionFormat = string.Empty;

        [JsonObject(MemberSerialization.OptIn)]
        public class TrianglesRecommendedParameters {
            [JsonProperty("min")]
            public int RecommendedMin;

            [JsonProperty("max")]
            public int RecommendedMax;
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class TrianglesParameters {
            [JsonProperty("default")]
            public int Count;

            [JsonProperty("recommended"), CanBeNull]
            public TrianglesRecommendedParameters RecommendedParameters;
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class RenameParameters {
            [JsonProperty("element"), CanBeNull]
            public string OldName;

            [JsonProperty("renameTo"), CanBeNull]
            public string NewName;
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class InlineGenerationParameters {
            [JsonProperty("source"), CanBeNull]
            public string Source;

            [JsonProperty("destination"), CanBeNull]
            public string Destination;

            [JsonProperty("description"), CanBeNull]
            public string Description;
        }

        [JsonProperty("triangles", Required = Required.DisallowNull)]
        public TrianglesParameters Triangles;

        [JsonProperty("inlineNodeGeneration"), CanBeNull]
        public InlineGenerationParameters InlineGeneration;

        [JsonProperty("rename"), CanBeNull]
        public RenameParameters[] Rename;

        [JsonProperty("mergeExceptions"), CanBeNull]
        public string[] MergeExceptions;

        [JsonProperty("mergeParents"), CanBeNull]
        public string[] MergeParents;

        [JsonProperty("mergeAsBlack"), CanBeNull]
        public string[] MergeAsBlack;

        [JsonProperty("elementsToRemove"), CanBeNull]
        public string[] ElementsToRemove;

        [JsonProperty("emptyNodesToKeep"), CanBeNull]
        public string[] EmptyNodesToKeep;

        [JsonProperty("convertUv2"), CanBeNull]
        public string[] ConvertUv2;

        public class ElementsPriority {
            [JsonProperty("elements")]
            public string Filter;

            [JsonProperty("priority")]
            public double Priority;
        }

        [JsonProperty("elementsPriorities"), CanBeNull]
        public ElementsPriority[] ElementsPriorities;

        [JsonProperty("offsetsAlongNormal"), CanBeNull]
        public ElementsPriority[] OffsetsAlongNormal;
    }
}