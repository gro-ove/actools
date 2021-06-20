using JetBrains.Annotations;
using StringBasedFilter.Parsing;

namespace StringBasedFilter {
    public class FilterFactory<T> {
        public delegate bool FilterTest(T obj, string key, ITestEntry entry);

        private readonly SimpleTester _tester;
        private readonly FilterParams _filterParams;

        public FilterFactory([NotNull] FilterTest testCallback, [CanBeNull] FilterParams filterParams) {
            _tester = new SimpleTester(testCallback);
            _filterParams = filterParams;
        }

        public IFilter<T> Create([NotNull] string filter) {
            return Filter.Create(_tester, filter, _filterParams);
        }

        private class SimpleTester : ITester<T> {
            private readonly FilterTest _callback;

            public SimpleTester(FilterTest callback) {
                _callback = callback;
            }

            string ITester<T>.ParameterFromKey(string key) {
                return null;
            }

            bool ITester<T>.Test(T obj, string key, ITestEntry value) {
                return _callback(obj, key, value);
            }
        }
    }

    public static class FilterFactory {
        public static FilterFactory<T> Create<T>([NotNull] FilterFactory<T>.FilterTest testCallback, FilterParams filterParams = null) {
            return new FilterFactory<T>(testCallback, filterParams);
        }
    }
}