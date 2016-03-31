namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeOr : FilterTreeNode {
        private readonly FilterTreeNode _a, _b;

        public FilterTreeNodeOr(FilterTreeNode a, FilterTreeNode b) {
            _a = a;
            _b = b;
        }

        public override string ToString() {
            return "{ " + _a + " || " + _b + " }";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            return _a.Test(tester, obj) || _b.Test(tester, obj);
        }
    }
}