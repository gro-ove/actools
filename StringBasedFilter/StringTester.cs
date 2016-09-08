namespace StringBasedFilter {
    public class StringTester : ITester<string> {
        public static readonly StringTester Instance = new StringTester();

        public string ParameterFromKey(string left) {
            return null;
        }

        public bool Test(string obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(string.Join(", ", obj));

                case "l":
                case "len":
                case "length":
                    return value.Test(obj.Length);

                case "empty":
                    return value.Test(obj.Length == 0);
            }

            return false;
        }
    }
}