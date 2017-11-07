namespace StringBasedFilter.Parsing {
    internal class FilterTreeNodeNot : FilterTreeNode {
        internal readonly FilterTreeNode A;

        public FilterTreeNodeNot(FilterTreeNode a) {
            A = a;
        }

        public override string ToString() {
            return "{ ! " + A + " }";
        }

        public override bool Test<T>(ITester<T> tester, T obj) {
            return !A.Test(tester, obj);
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