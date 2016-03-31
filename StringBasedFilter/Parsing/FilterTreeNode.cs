namespace StringBasedFilter.Parsing {
    internal abstract class FilterTreeNode {
        public abstract bool Test<T>(ITester<T> tester, T obj);
    }
}