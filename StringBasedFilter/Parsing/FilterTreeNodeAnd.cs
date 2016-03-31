namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeAnd : FilterTreeNode {
        private readonly FilterTreeNode _a, _b;

        public FilterTreeNodeAnd(FilterTreeNode a, FilterTreeNode b) {
            _a = a;
            _b = b;
        }

        public override string ToString() {
            return "{ " + _a + " && " + _b + " }";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            return _a.Test(tester, obj) && _b.Test(tester, obj);
        }
    }
}