using System.IO;
using System.Linq;
using System.Windows.Forms;
using AcManager.Tools.Starters;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Miscellaneous {
    public static class SteamAchievementsReader {
        public static bool Handle(string[] args) {
            var logFilename = args.FirstOrDefault(x => x.StartsWith(@"--log="))?.Substring(6);
            var outFilename = args.FirstOrDefault(x => x.StartsWith(@"--out="))?.Substring(6);
            if (logFilename != null) {
                Logging.Initialize(logFilename, false);
            }
            if (SteamStarter.Initialize(MainExecutingFile.Directory, true)) {
                var data = args.Contains(@"--stats")
                        ? JsonConvert.SerializeObject(SteamStarter.GetAchievementStats(), Formatting.Indented)
                        : JsonConvert.SerializeObject(SteamStarter.GetAchievements(), Formatting.Indented);
                if (outFilename != null) {
                    File.WriteAllText(outFilename, data);
                } else {
                    MessageBox.Show(data);
                }
            }
            return true;
        }
    }
}