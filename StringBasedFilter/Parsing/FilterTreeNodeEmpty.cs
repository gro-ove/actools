namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeEmpty : FilterTreeNode {
        public override string ToString() {
            return "{}";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            return true;
        }
    }
}