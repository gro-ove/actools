namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeChild : FilterTreeNode {
        private readonly string _key;
        private readonly IFilter _filter;

        public FilterTreeNodeChild(string key, FilterTreeNode childNode, FilterParams filterParams) {
            _key = key;
            _filter = new Filter(childNode);
        }
        public override string ToString() {
            return "{ [" + _key + "]( " + _filter + ") }";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            var p = tester as IParentTester<T>;
            return p != null && p.TestChild(obj, _key, _filter);
        }
    }
}