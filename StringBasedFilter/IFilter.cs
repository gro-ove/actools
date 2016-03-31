using JetBrains.Annotations;

namespace StringBasedFilter {
    public interface IFilter {
        bool Test<T>([NotNull]ITester<T> tester, [NotNull]T obj);

        bool IsAffectedBy<T>([NotNull]ITester<T> tester, [NotNull]string propertyName);
    }

    public interface IFilter<in T> {
        bool Test([NotNull]T obj);

        bool IsAffectedBy([NotNull]string propertyName);
    }
}