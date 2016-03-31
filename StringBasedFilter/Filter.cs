using System.Linq;
using StringBasedFilter.Parsing;

namespace StringBasedFilter {
    /// <summary>
    /// Typed version with fixed ITester.
    /// </summary>
    public class Filter<T> : Filter, IFilter<T> {
        private readonly ITester<T> _tester;

        public Filter(ITester<T> tester, string filter) : base(filter) {
            _tester = tester;
        }

        public bool Test(T obj) {
            return Test(_tester, obj);
        }

        /// <summary>
        /// Checks if filter depends on specific property. Especially useful for NotifyPropertyChanged
        /// stuff.
        /// </summary>
        /// <param name="propertyName">Name of property</param>
        /// <returns>True if filtering depends</returns>
        public bool IsAffectedBy(string propertyName) {
            return IsAffectedBy(_tester, propertyName);
        }
    }

    /// <summary>
    /// Untyped version with variable ITester.
    /// </summary>
    public class Filter : IFilter {
        public static IFilter<T> Create<T>(ITester<T> tester, string filter) {
            return new Filter<T>(tester, filter);
        }

        private readonly string[] _keys;
        private readonly FilterTreeNode _testTree;
        private string[] _properties;

        internal Filter(string filter) {
            _testTree = ParseTree(filter, out _keys);
        }

        internal Filter(FilterTreeNode tree) {
            _testTree = tree;
            _keys = new string[0];
        }

        public override string ToString() {
            return _testTree.ToString();
        }

        public void ResetPropeties() {
            _properties = null;
        }

        public bool IsAffectedBy<T>(ITester<T> tester, string propertyName) {
            if (_properties == null) {
                _properties = _keys.Select(tester.ParameterFromKey).ToArray();
            }

            return _properties.Contains(propertyName);
        }

        public bool Test<T>(ITester<T> tester, T obj) {
            return _testTree.Test(tester, obj);
        }

        private static FilterTreeNode ParseTree(string filter, out string[] properies) {
            return new FilterParser().Parse(filter, out properies);
        }
    }
}
