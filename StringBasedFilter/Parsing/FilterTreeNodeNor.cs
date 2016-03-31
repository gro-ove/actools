namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeNor : FilterTreeNode {
        private readonly FilterTreeNode _a, _b;

        public FilterTreeNodeNor(FilterTreeNode a, FilterTreeNode b) {
            _a = a;
            _b = b;
        }

        public override string ToString() {
            return "{ " + _a + " ^ " + _b + " }";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            var a = _a.Test(tester, obj);
            var b = _b.Test(tester, obj);
            return a && !b || !a && b;
        }
    }
}