using JetBrains.Annotations;

namespace StringBasedFilter.TestEntries {
    public interface ITestEntryFactory {
        [CanBeNull]
        ITestEntry Create(Operator op, [NotNull] string value);
    }
}