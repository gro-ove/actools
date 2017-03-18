namespace AcTools {
    public static class AcToolsInformation {
        private static string _name;

        public static string Name => _name ?? (_name = $"AcTools {typeof(AcToolsInformation).Assembly.GetName().Version}");
    }
}
