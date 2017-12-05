using JetBrains.Annotations;

namespace AcManager.Tools.Filters {
    public class KeywordDescription {
        public string Key { get; }
        public string Description { get; }

        [CanBeNull]
        public string Unit { get; }

        public KeywordType Type { get; }
        public KeywordPriority Priority { get; }
        public string[] AlternativeKeys { get; }

        public KeywordDescription(string key, string description, KeywordType type, KeywordPriority priority, params string[] alternativeKeys) {
            Key = key;
            Description = description;
            Type = type;
            Priority = priority;
            AlternativeKeys = alternativeKeys;
        }

        public KeywordDescription(string key, string description, string unit, KeywordType type, KeywordPriority priority, params string[] alternativeKeys) {
            Key = key;
            Description = description;
            Unit = unit;
            Type = type;
            Priority = priority;
            AlternativeKeys = alternativeKeys;
        }
    }
}