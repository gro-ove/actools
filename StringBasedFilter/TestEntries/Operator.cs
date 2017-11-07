namespace StringBasedFilter.TestEntries {
    public enum Operator {
        Less = '<',
        LessEqual = '≤',
        More = '>',
        MoreEqual = '≥',
        Equal = '='
    }

    public static class OperatorExtension {
        public static string OperatorToString(this Operator op) {
            return ((char)op).ToString();
        }
    }
}