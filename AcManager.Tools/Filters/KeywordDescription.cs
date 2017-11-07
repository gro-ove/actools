namespace AcManager.Tools.Filters {
    public class KeywordDescription {
        public string Key { get; }
        public string Description { get; }
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
    }
}