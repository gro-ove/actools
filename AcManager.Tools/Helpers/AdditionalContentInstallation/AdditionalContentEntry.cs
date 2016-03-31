namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public enum AdditionalContentType {
        Car, CarSkin, Track, Showroom, Font
    }

    public class AdditionalContentEntry {
        public string Id { get; internal set; }

        public AdditionalContentType Type { get; internal set; }

        public string Name { get; internal set; }

        public string Version { get; internal set; }

        public string Path { get; internal set; }
    }
}
