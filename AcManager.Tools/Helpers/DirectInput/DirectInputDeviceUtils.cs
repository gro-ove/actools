using System.Text.RegularExpressions;

namespace AcManager.Tools.Helpers.DirectInput {
    public static class DirectInputDeviceUtils {
        public static bool IsController(string deviceName) {
            // return false; // fix for Ben Dover?
            return Regex.IsMatch(deviceName, @"^Controller \((.+)\)$");
        }

        public static string GetXboxControllerGuid() {
            return @"028E045E-0000-0000-0000-504944564944";
        }
    }
}