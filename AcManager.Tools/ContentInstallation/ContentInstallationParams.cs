namespace AcManager.Tools.ContentInstallation {
    public class ContentInstallationParams {
        public static readonly ContentInstallationParams Default = new ContentInstallationParams();

        public bool AllowExecutables { get; set; }
        public string CarId { get; set; }
        public string FallbackId { get; set; }
        public string Checksum { get; set; }
        public string DisplayName { get; set; }
        public string InformationUrl { get; set; }
        public string DisplayVersion { get; set; }
    }
}