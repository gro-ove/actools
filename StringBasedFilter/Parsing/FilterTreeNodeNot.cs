namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeNot : FilterTreeNode {
        private readonly FilterTreeNode _a;

        public FilterTreeNodeNot(FilterTreeNode a) {
            _a = a;
        }

        public override string ToString() {
            return "{ ! " + _a + " }";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            return !_a.Test(tester, obj);
        }

        public static string EscapeDataString(string s) {
            return s
                    .Replace(@"\", @"\\")
                    .Replace(@"!", @"\!")
                    .Replace(@"^", @"\^")
                    .Replace(@"(", @"\(")
                    .Replace(@")", @"\)")
                    .Replace(@",", @"\,")
                    .Replace(@"&", @"\&")
                    .Replace(@"|", @"\|");
        }
    }
}