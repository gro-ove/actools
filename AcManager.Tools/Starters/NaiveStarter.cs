using System.Diagnostics;
using AcManager.Tools.Managers;

namespace AcManager.Tools.Starters {
    public class NaiveStarter : BaseStarter {
        public override void Run() {
            LauncherProcess = Process.Start(new ProcessStartInfo {
                FileName = AcsFilename,
                WorkingDirectory = AcRootDirectory.Instance.RequireValue
            });
        }
    }
}