using System.Text.RegularExpressions;

namespace AcTools.Processes {
    /// <summary>
    /// Made for AI limitations feature of CM. Not sure if it’s gonna be around for long.
    /// </summary>
    public class FakeCarIds {
        public static bool IsFake(string carId, out string actualCarId) {
            if (carId.StartsWith("__cm_tmp_")) {
                var match = Regex.Match(carId, @"^__cm_tmp_([\w+._-]+)_[a-f0-9]{2,8}$");
                if (match.Success) {
                    actualCarId = match.Groups[1].Value;
                    return true;
                }
            }

            actualCarId = null;
            return false;
        }
    }
}