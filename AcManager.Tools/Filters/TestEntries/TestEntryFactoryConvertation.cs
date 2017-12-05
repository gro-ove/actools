using JetBrains.Annotations;

namespace AcManager.Tools.Filters.TestEntries {
    public delegate bool TestEntryFactoryConvertation([CanBeNull] string value, out double parsed);
    public delegate double TestEntryFactoryPostfixMultiplier([CanBeNull] string postfix);
}