using System.Diagnostics;
using System.IO;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils;

namespace AcManager.Tools.Starters {
    public class NaiveStarter : StarterBase {
        protected override string AcsName {
            get {
                var acRoot = AcRootDirectory.Instance.Value;
                return acRoot != null && File.Exists(Path.Combine(acRoot, "acs_pro.exe")) ? @"acs_pro.exe" : base.AcsName;
            }
        }

        public override void Run() {
            RaisePreviewRunEvent(AcsFilename);
            GameProcess = Process.Start(new ProcessStartInfo {
                FileName = AcsFilename,
                WorkingDirectory = AcRootDirectory.Instance.RequireValue
            });
            if (GameProcess != null && OptionTrackProcess) {
                ChildProcessTracker.AddProcess(GameProcess);
            }
        }
    }
}