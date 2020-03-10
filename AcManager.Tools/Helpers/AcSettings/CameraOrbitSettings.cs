namespace AcManager.Tools.Helpers.AcSettings {
    public class CameraOrbitSettings : IniSettings {
        internal CameraOrbitSettings() : base(@"camera_onboard_free", systemConfig: true) { }

        private bool _sphericalCoordinates;

        public bool SphericalCoordinates {
            get => _sphericalCoordinates;
            set => Apply(value, ref _sphericalCoordinates);
        }

        protected override void LoadFromIni() {
            SphericalCoordinates = Ini["CAMERA_SETTINGS"].GetBool("SPHERICAL_COORDS", true);
        }

        protected override void SetToIni() {
            Ini["CAMERA_SETTINGS"].Set("SPHERICAL_COORDS", SphericalCoordinates);
        }
    }
}