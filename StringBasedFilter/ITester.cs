using JetBrains.Annotations;

namespace StringBasedFilter {
    public interface ITester<in T> {
        /// <summary>
        /// Only changeable values!
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        string ParameterFromKey([CanBeNull] string key);

        bool Test([NotNull] T obj, [CanBeNull] string key, [NotNull] ITestEntry value);
    }

    public interface IParentTester<in T> : ITester<T> {
        bool TestChild([NotNull] T obj, [CanBeNull] string key, [NotNull] IFilter filter);
    }
}
